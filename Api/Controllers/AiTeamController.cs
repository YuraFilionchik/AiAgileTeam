#pragma warning disable SKEXP0110, SKEXP0070, SKEXP0001
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.ChatCompletion;
using AiAgileTeam.Models;
using AiAgileTeam.Services;
using AiAgileTeam.Services.Orchestration;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AiAgileTeam.Api.Controllers;

[ApiController]
[Route("api/aiteam")]
public class AiTeamController : ControllerBase
{
    private readonly AiTeamService _teamService;
    private readonly SessionStore _sessionStore;
    private readonly MarkdownService _markdownService;
    private readonly OrchestrationStrategyFactory _orchestrationStrategyFactory;
    private readonly ITokenUsageTracker _tokenUsageTracker;
    private readonly ITokenUsageContextAccessor _tokenUsageContextAccessor;
    private readonly IMediaStorageService _mediaStorageService;
    private readonly IAgentOrchestrator _agentOrchestrator;

    public AiTeamController(
        AiTeamService teamService,
        SessionStore sessionStore,
        MarkdownService markdownService,
        OrchestrationStrategyFactory orchestrationStrategyFactory,
        ITokenUsageTracker tokenUsageTracker,
        ITokenUsageContextAccessor tokenUsageContextAccessor,
        IMediaStorageService mediaStorageService,
        IAgentOrchestrator agentOrchestrator)
    {
        _teamService = teamService;
        _sessionStore = sessionStore;
        _markdownService = markdownService;
        _orchestrationStrategyFactory = orchestrationStrategyFactory;
        _tokenUsageTracker = tokenUsageTracker;
        _tokenUsageContextAccessor = tokenUsageContextAccessor;
        _mediaStorageService = mediaStorageService;
        _agentOrchestrator = agentOrchestrator;
    }

    /// <summary>
    /// Returns API health status.
    /// </summary>
    [HttpGet("health")]
    public IActionResult GetHealth()
    {
        return Ok(new { status = "ok" });
    }

    [HttpGet("token-usage/{executionId}")]
    public ActionResult<TokenUsageSnapshot> GetTokenUsage(string executionId)
    {
        if (string.IsNullOrWhiteSpace(executionId))
        {
            return BadRequest("Execution id is required.");
        }

        return Ok(_tokenUsageTracker.GetSnapshot(executionId));
    }

    /// <summary>
    /// Uploads media content and returns an access URL or data URI.
    /// </summary>
    [HttpPost("media/upload")]
    public async Task<ActionResult<MediaUploadResponse>> UploadMediaAsync([FromBody] MediaUploadRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.ExecutionId))
        {
            return BadRequest("Execution id is required.");
        }

        if (request.Bytes.Length == 0)
        {
            return BadRequest("Media payload is empty.");
        }

        if (string.IsNullOrWhiteSpace(request.MimeType))
        {
            return BadRequest("MIME type is required.");
        }

        var url = await _mediaStorageService.UploadAsync(
            new MediaContent(request.Bytes, request.MimeType, request.FileName),
            request.ExecutionId);

        return Ok(new MediaUploadResponse
        {
            Url = url,
            MimeType = request.MimeType,
            FileName = request.FileName
        });
    }

    /// <summary>
    /// Gets API config and model for clarification agent based on settings
    /// </summary>
    private (ApiConfig apiConfig, string model) GetClarificationConfig(AppSettings settings)
    {
        ApiConfig apiConfig;
        string model;
        
        if (settings.ApiKeyMode == "global")
        {
            apiConfig = settings.GlobalApi;
            // Use model from first selected agent or default
            model = settings.Agents.FirstOrDefault(a => a.IsSelected)?.ModelSettings?.Model ?? "gpt-4";
        }
        else
        {
            // Use first selected agent's settings
            var firstAgent = settings.Agents.FirstOrDefault(a => a.IsSelected);
            apiConfig = firstAgent?.ApiSettings ?? settings.GlobalApi;
            model = firstAgent?.ModelSettings?.Model ?? "gpt-4";
        }
        
        return (apiConfig, model);
    }

    [HttpPost("session")]
    public async IAsyncEnumerable<StreamingMessageDto> StartSession([FromBody] SessionRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        string serverSessionId = Guid.NewGuid().ToString();
        _tokenUsageTracker.Clear(serverSessionId);
        _tokenUsageContextAccessor.Current = new TokenUsageContext(serverSessionId, request.Clarify ? "Clarification" : "Discussion");
        var sessionData = _sessionStore.GetOrCreate(serverSessionId);
        var groupChat = sessionData.GroupChat;
        var useGroupChatSessionBuffer = request.Clarify || request.Settings.OrchestrationMode == OrchestrationMode.GroupChat;
        
        if (useGroupChatSessionBuffer && request.History != null)
        {
            foreach (var msg in request.History)
            {
                groupChat.AddChatMessage(BuildChatMessage(msg));
            }
        }

        if (request.AttachedMedia is not null)
        {
            await _agentOrchestrator.AddUserImageAsync(serverSessionId, request.AttachedMedia);
        }
        
        bool clarificationPhase = request.Clarify;
        ChatCompletionAgent? clarificationAgent = null;

        if (clarificationPhase)
        {
            var (apiConfig, model) = GetClarificationConfig(request.Settings);

            var cBuilder = Kernel.CreateBuilder();
            cBuilder.Services.AddSingleton(_teamService.CreateChatService(apiConfig, model, "Clarification Agent", serverSessionId, "Clarification"));

            clarificationAgent = new ChatCompletionAgent
            {
                Name = "Clarification Agent",
                Instructions = "Your name is Clarification Agent. You are a Requirements Analyst. Your goal is to refine the user's initial request into a clear project brief.\n\n" +
                               "1. Ask up to 3 targeted questions to clarify the project's purpose, target audience, and key technical constraints.\n" +
                               "2. Ask questions one by one. Do not overwhelm the user.\n" +
                               "3. Once you have enough information, provide a structured 'Project Brief' summary.\n" +
                               "4. End your final summary with the exact word [READY].",
                Kernel = cBuilder.Build()
            };

            groupChat.AddChatMessage(new Microsoft.SemanticKernel.ChatMessageContent(AuthorRole.User, request.Query));
            
            await foreach (var content in ProcessAgentResponseAsync(clarificationAgent, groupChat, serverSessionId, cancellationToken))
            {
                yield return content;
            }
        }
        else
        {
            if (request.Settings.OrchestrationMode == OrchestrationMode.GroupChat)
            {
                groupChat.AddChatMessage(new Microsoft.SemanticKernel.ChatMessageContent(AuthorRole.User, request.Query));
            }

            var strategy = _orchestrationStrategyFactory.Resolve(request.Settings.OrchestrationMode);
            await foreach (var content in strategy.RunDiscussionAsync(request.Settings, sessionData, serverSessionId, request.Query, cancellationToken))
            {
                yield return content;
            }
        }
    }

    [HttpPost("report")]
    public IActionResult DownloadReport([FromBody] ReportRequest request)
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

        // Server-side safety: strip system/floor-change messages and [DONE] markers
        var cleanMessages = request.Messages
            .Where(m => m.Author != "System"
                        && !string.IsNullOrWhiteSpace(m.Content)
                        && !m.Content.TrimStart().StartsWith("*[Project Manager gives floor", StringComparison.Ordinal))
            .Select(m => new ChatMessageDto
            {
                Author = m.Author,
                IsUser = m.IsUser,
                Content = m.Content
                    .Replace("[DONE]", "", StringComparison.OrdinalIgnoreCase)
                    .TrimEnd()
            })
            .Where(m => !string.IsNullOrWhiteSpace(m.Content))
            .ToList();

        var deduplicatedMessages = new List<ChatMessageDto>(cleanMessages.Count);
        string? previousAuthor = null;
        string? previousContent = null;

        foreach (var message in cleanMessages)
        {
            var normalizedContent = message.Content.Trim();
            var isDuplicate = string.Equals(previousAuthor, message.Author, StringComparison.Ordinal)
                              && string.Equals(previousContent, normalizedContent, StringComparison.Ordinal);

            if (isDuplicate)
            {
                continue;
            }

            deduplicatedMessages.Add(message);
            previousAuthor = message.Author;
            previousContent = normalizedContent;
        }

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1, Unit.Inch);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(11));

                page.Header().PaddingBottom(10).Column(header =>
                {
                    header.Item().Text(request.Title).SemiBold().FontSize(20).FontColor(Colors.Blue.Medium);
                    header.Item().PaddingTop(2).Text($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC").FontSize(8).FontColor(Colors.Grey.Medium);
                });

                page.Content().Column(column =>
                {
                    column.Spacing(12);

                    foreach (var message in deduplicatedMessages)
                    {
                        if (message.IsUser)
                        {
                            // Render user request as a highlighted brief section
                            column.Item()
                                .Background(Colors.Blue.Lighten5)
                                .Padding(10)
                                .Column(section =>
                                {
                                    section.Item()
                                        .PaddingBottom(4)
                                        .Text("Project Request")
                                        .Bold()
                                        .FontSize(12)
                                        .FontColor(Colors.Blue.Darken2);

                                    section.Item()
                                        .Element(c => _markdownService.RenderMarkdown(c, message.Content));
                                });
                        }
                        else
                        {
                            // Render PM synthesis as the main document body
                            column.Item().Column(section =>
                            {
                                section.Item()
                                    .PaddingBottom(4)
                                    .Text($"Prepared by: {message.Author}")
                                    .FontSize(9)
                                    .FontColor(Colors.Grey.Darken1);

                                section.Item()
                                    .Element(c => _markdownService.RenderMarkdown(c, message.Content));
                            });
                        }
                    }
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Page ");
                    x.CurrentPageNumber();
                });
            });
        });

        byte[] pdfBytes = document.GeneratePdf();
        return File(pdfBytes, "application/pdf", "AiAgileTeam_Report.pdf");
    }

    [HttpPost("message")]
    public async IAsyncEnumerable<StreamingMessageDto> SendMessage([FromBody] SessionRequest request, [FromQuery] bool isClarificationPhase, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        string serverSessionId = request.ServerSessionId ?? Guid.NewGuid().ToString();
        if (string.IsNullOrWhiteSpace(request.ServerSessionId))
        {
            _tokenUsageTracker.Clear(serverSessionId);
        }

        _tokenUsageContextAccessor.Current = new TokenUsageContext(serverSessionId, isClarificationPhase ? "Clarification" : "Discussion");
        var sessionData = _sessionStore.GetOrCreate(serverSessionId);
        var groupChat = sessionData.GroupChat;
        var useGroupChatSessionBuffer = isClarificationPhase || request.Settings.OrchestrationMode == OrchestrationMode.GroupChat;
        
        // Recover history if session is not configured and history is provided
        if (useGroupChatSessionBuffer && !sessionData.IsConfigured && request.History != null)
        {
            foreach(var msg in request.History) 
            {
                groupChat.AddChatMessage(BuildChatMessage(msg));
            }
        }

        if (request.AttachedMedia is not null)
        {
            await _agentOrchestrator.AddUserImageAsync(serverSessionId, request.AttachedMedia);
        }
        
        if (useGroupChatSessionBuffer && !string.IsNullOrWhiteSpace(request.Query))
        {
            groupChat.AddChatMessage(new Microsoft.SemanticKernel.ChatMessageContent(AuthorRole.User, request.Query));
        }

        if (isClarificationPhase)
        {
            var (apiConfig, model) = GetClarificationConfig(request.Settings);

            var cBuilder = Kernel.CreateBuilder();
            cBuilder.Services.AddSingleton(_teamService.CreateChatService(apiConfig, model, "Clarification Agent", serverSessionId, "Clarification"));

            var clarificationAgent = new ChatCompletionAgent
            {
                Name = "Clarification Agent",
                Instructions = "Your name is Clarification Agent. You are a Requirements Analyst. Your goal is to refine the user's initial request into a clear project brief.\n\n" +
                               "1. Ask up to 3 targeted questions to clarify the project's purpose, target audience, and key technical constraints.\n" +
                               "2. Ask questions one by one. Do not overwhelm the user.\n" +
                               "3. Once you have enough information, provide a structured 'Project Brief' summary.\n" +
                               "4. End your final summary with the exact word [READY].",
                Kernel = cBuilder.Build()
            };

            await foreach (var content in ProcessAgentResponseAsync(clarificationAgent, groupChat, serverSessionId, cancellationToken))
            {
                yield return content;
            }
        }
        else
        {
            var strategy = _orchestrationStrategyFactory.Resolve(request.Settings.OrchestrationMode);
            await foreach (var content in strategy.RunDiscussionAsync(request.Settings, sessionData, serverSessionId, request.Query, cancellationToken))
            {
                yield return content;
            }
        }
    }

    private async IAsyncEnumerable<StreamingMessageDto> ProcessAgentResponseAsync(ChatCompletionAgent agent, AgentGroupChat groupChat, string serverSessionId, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        string currentContent = "";
        Exception? caughtException = null;
        
        IAsyncEnumerator<Microsoft.SemanticKernel.ChatMessageContent>? enumerator = null;
        try
        {
            enumerator = groupChat.InvokeAsync(agent).GetAsyncEnumerator(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            caughtException = ex;
        }

        if (caughtException == null)
        {
            bool hasMore = true;
            while (hasMore)
            {
                try
                {
                    hasMore = await enumerator!.MoveNextAsync();
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    hasMore = false;
                    caughtException = ex;
                }

                if (hasMore)
                {
                    var message = enumerator!.Current;
                    var mediaUrl = TryExtractImageUrl(message);
                    currentContent += message.Content;
                    yield return new StreamingMessageDto
                    {
                        Author = agent.Name ?? "Agent", // Author is always the clarification agent
                        ContentPiece = message.Content ?? "",
                        IsComplete = false,
                        ServerSessionId = serverSessionId,
                        MediaUrl = mediaUrl,
                        MediaMimeType = !string.IsNullOrWhiteSpace(mediaUrl) ? "image/*" : null
                    };
                }
            }
        }

        if (enumerator != null)
        {
            await enumerator.DisposeAsync();
        }

        if (caughtException != null)
        {
            yield return new StreamingMessageDto { Author = "System", ContentPiece = $"\r\n⚠️ Error during processing: {caughtException.Message}\r\n", IsComplete = true, ServerSessionId = serverSessionId };
        }
        else
        {
            yield return new StreamingMessageDto { Author = agent.Name ?? "Agent", ContentPiece = "", IsComplete = true, ServerSessionId = serverSessionId };
        }
    }

    private static ChatMessageContent BuildChatMessage(ChatMessageDto dto)
    {
        var role = dto.IsUser ? AuthorRole.User : AuthorRole.Assistant;
        var hasImage = !string.IsNullOrWhiteSpace(dto.MediaUrl) && !string.IsNullOrWhiteSpace(dto.MediaMimeType);

        if (!hasImage)
        {
            return new ChatMessageContent(role, dto.Content)
            {
                AuthorName = dto.Author
            };
        }

        var items = new List<KernelContent>();
        if (!string.IsNullOrWhiteSpace(dto.Content))
        {
            items.Add(new TextContent(dto.Content));
        }

        items.Add(new ImageContent { Uri = new Uri(dto.MediaUrl!, UriKind.RelativeOrAbsolute) });

        var contentItems = new ChatMessageContentItemCollection();
        foreach (var item in items)
        {
            contentItems.Add(item);
        }

        return new ChatMessageContent
        {
            Role = role,
            AuthorName = dto.Author,
            Items = contentItems
        };
    }

    private static string? TryExtractImageUrl(ChatMessageContent message)
    {
        if (message.Items is null)
        {
            return null;
        }

        var imageItem = message.Items.FirstOrDefault(item => item.GetType().Name.Contains("ImageContent", StringComparison.Ordinal));
        if (imageItem is null)
        {
            return null;
        }

        var uriText = imageItem.GetType().GetProperty("Uri")?.GetValue(imageItem)?.ToString();
        if (!string.IsNullOrWhiteSpace(uriText))
        {
            return uriText;
        }

        var dataUri = imageItem.GetType().GetProperty("DataUri")?.GetValue(imageItem)?.ToString();
        return string.IsNullOrWhiteSpace(dataUri) ? null : dataUri;
    }

}
