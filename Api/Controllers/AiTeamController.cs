#pragma warning disable SKEXP0110, SKEXP0070, SKEXP0001
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.ChatCompletion;
using AiAgileTeam.Models;
using AiAgileTeam.Services;
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

    public AiTeamController(AiTeamService teamService, SessionStore sessionStore, MarkdownService markdownService)
    {
        _teamService = teamService;
        _sessionStore = sessionStore;
        _markdownService = markdownService;
    }

    /// <summary>
    /// Returns API health status.
    /// </summary>
    [HttpGet("health")]
    public IActionResult GetHealth()
    {
        return Ok(new { status = "ok" });
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
        var sessionData = _sessionStore.GetOrCreate(serverSessionId);
        var groupChat = sessionData.GroupChat;
        
        if (request.History != null)
        {
            foreach (var msg in request.History)
            {
                var role = msg.IsUser ? AuthorRole.User : AuthorRole.Assistant;
                var chatMsg = new Microsoft.SemanticKernel.ChatMessageContent(role, msg.Content);
                chatMsg.AuthorName = msg.Author;
                groupChat.AddChatMessage(chatMsg);
            }
        }
        
        bool clarificationPhase = request.Clarify;
        ChatCompletionAgent? clarificationAgent = null;

        if (clarificationPhase)
        {
            var (apiConfig, model) = GetClarificationConfig(request.Settings);
            
            var cBuilder = Kernel.CreateBuilder();
            cBuilder.Services.AddSingleton(_teamService.CreateChatService(apiConfig, model));
            
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
            groupChat.AddChatMessage(new Microsoft.SemanticKernel.ChatMessageContent(AuthorRole.User, request.Query));
            await foreach (var content in RunTeamDiscussionAsync(request.Settings, sessionData, serverSessionId, cancellationToken))
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
        var sessionData = _sessionStore.GetOrCreate(serverSessionId);
        var groupChat = sessionData.GroupChat;
        
        // Recover history if session is not configured and history is provided
        if (!sessionData.IsConfigured && request.History != null)
        {
            foreach(var msg in request.History) 
            {
                var role = msg.IsUser ? AuthorRole.User : AuthorRole.Assistant;
                var chatMsg = new Microsoft.SemanticKernel.ChatMessageContent(role, msg.Content);
                chatMsg.AuthorName = msg.Author;
                groupChat.AddChatMessage(chatMsg);
            }
        }
        
        if (!string.IsNullOrWhiteSpace(request.Query))
        {
            groupChat.AddChatMessage(new Microsoft.SemanticKernel.ChatMessageContent(AuthorRole.User, request.Query));
        }

        if (isClarificationPhase)
        {
            var (apiConfig, model) = GetClarificationConfig(request.Settings);
            
            var cBuilder = Kernel.CreateBuilder();
            cBuilder.Services.AddSingleton(_teamService.CreateChatService(apiConfig, model));
            
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
            await foreach (var content in RunTeamDiscussionAsync(request.Settings, sessionData, serverSessionId, cancellationToken))
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
                    currentContent += message.Content;
                    yield return new StreamingMessageDto
                    {
                        Author = agent.Name ?? "Agent", // Author is always the clarification agent
                        ContentPiece = message.Content ?? "",
                        IsComplete = false,
                        ServerSessionId = serverSessionId
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

    private async IAsyncEnumerable<StreamingMessageDto> RunTeamDiscussionAsync(AppSettings settings, SessionData sessionData, string serverSessionId, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var groupChat = sessionData.GroupChat;

        if (!sessionData.IsConfigured)
        {
            var agentsToRun = settings.Agents.Where(a => a.IsSelected).ToList();
            var skAgents = new List<ChatCompletionAgent>();
            ChatCompletionAgent? projectManager = null;

            Console.WriteLine($"[AiTeamController] Creating agents. Total selected: {agentsToRun.Count}");
            foreach (var agentConfig in agentsToRun)
            {
                Console.WriteLine($"[AiTeamController]   - {agentConfig.DisplayName} ({agentConfig.Role}) IsSelected={agentConfig.IsSelected}, IsMandatory={agentConfig.IsMandatory}");
                var agent = _teamService.CreateAgent(agentConfig, settings);
                skAgents.Add(agent);
                groupChat.AddAgent(agent);
                if (agentConfig.Role == "Project Manager") { projectManager = agent; }
            }

            if (projectManager != null)
            {
                // Resolve PM's LLM for orchestration decisions and context compression
                var pmConfig = agentsToRun.First(a => a.Role == "Project Manager");
                ApiConfig pmApiConfig = settings.ApiKeyMode == "global"
                    ? settings.GlobalApi
                    : pmConfig.ApiSettings ?? settings.GlobalApi;
                var pmLlm = _teamService.CreateChatService(pmApiConfig, pmConfig.ModelSettings.Model);

                sessionData.PmLlm = pmLlm;
                sessionData.Compressor = new ChatContextCompressor(pmLlm, tailSize: 4);

                var selectionStrategy = new PmOrchestratorSelectionStrategy(pmLlm, sessionData.Compressor);

                groupChat.ExecutionSettings = new()
                {
                    SelectionStrategy = selectionStrategy,
                    TerminationStrategy = new SafeTerminationStrategy()
                    {
                        MaximumIterations = agentsToRun.Count * 3 + 3
                    }
                };
                Console.WriteLine($"[AiTeamController] PM-driven orchestration configured. Agents count: {agentsToRun.Count}");
            }
            else
            {
                Console.WriteLine($"[AiTeamController] WARNING: Project Manager not found in selected agents!");
            }
            sessionData.IsConfigured = true;
        }

        string currentAuthor = "";
        bool firstChunk = true;
        Exception? caughtException = null;

        IAsyncEnumerator<StreamingChatMessageContent>? enumerator = null;
        try
        {
            enumerator = groupChat.InvokeStreamingAsync().GetAsyncEnumerator(cancellationToken);
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

                    Console.WriteLine($"[AiTeamController] SK stream chunk: AuthorName='{message.AuthorName}', Content='{message.Content?.Substring(0, Math.Min(30, message.Content?.Length ?? 0))}', HasContent={!string.IsNullOrEmpty(message.Content)}");

                    // Detect author change - this means previous agent finished their turn
                    bool authorChanged = !firstChunk && !string.IsNullOrEmpty(message.AuthorName) && message.AuthorName != currentAuthor;

                    if (authorChanged)
                    {
                        Console.WriteLine($"[AiTeamController] Author changed from '{currentAuthor}' to '{message.AuthorName}'");
                    }

                    // Only switch author if the new author is non-null and different from current
                    // AND if we have already received some content from the previous author
                    if (firstChunk || (!string.IsNullOrEmpty(message.AuthorName) && message.AuthorName != currentAuthor))
                    {
                        // Signal completion of previous agent's message BEFORE switching
                        // But only if the previous agent actually said something
                        if (!firstChunk && !string.IsNullOrEmpty(currentAuthor))
                        {
                            Console.WriteLine($"[AiTeamController] Sending IsComplete=true for '{currentAuthor}'");
                            yield return new StreamingMessageDto { Author = currentAuthor, ContentPiece = "", IsComplete = true, ServerSessionId = serverSessionId };
                        }

                        if (!string.IsNullOrEmpty(message.AuthorName))
                        {
                            currentAuthor = message.AuthorName;
                            Console.WriteLine($"[AiTeamController] Now processing: '{currentAuthor}'");
                        }
                        else if (firstChunk)
                        {
                            currentAuthor = "Unknown";
                        }

                        if (firstChunk || !string.IsNullOrEmpty(message.AuthorName))
                        {
                            yield return new StreamingMessageDto { Author = "System", ContentPiece = $"\r\n*[Project Manager gives floor to {currentAuthor}...]*\r\n\r\n", IsComplete = true, ServerSessionId = serverSessionId };
                        }

                        firstChunk = false;
                    }

                    // Send content only if this is not a system floor-change message
                    // and if content is actually present
                    if (!string.IsNullOrEmpty(message.Content) && !message.Content.Trim().All(char.IsWhiteSpace))
                    {
                        yield return new StreamingMessageDto
                        {
                            Author = currentAuthor,
                            ContentPiece = message.Content,
                            IsComplete = false,
                            ServerSessionId = serverSessionId
                        };
                    }
                }
            }
        }

        if (enumerator != null)
        {
            await enumerator.DisposeAsync();
        }

        if (caughtException != null)
        {
            var hint = "";
            try
            {
                var msg = caughtException.Message ?? string.Empty;
                if (msg.Contains("404") || msg.Contains("Not Found", StringComparison.OrdinalIgnoreCase) || msg.Contains("Response status code does not indicate success", StringComparison.OrdinalIgnoreCase))
                {
                    hint = "\r\nHint: Provider returned 404 Not Found. Check provider settings (ApiKey, Endpoint) and the model name — the model may be unavailable for your account or the name is incorrect.\r\n";
                }
            }
            catch { }

            Console.WriteLine($"[ERROR] Discussion exception: {caughtException}");
            yield return new StreamingMessageDto { Author = "System", ContentPiece = $"\r\n⚠️ Discussion error: {caughtException.Message}\r\n{hint}", IsComplete = true, ServerSessionId = serverSessionId };
        }
        else if (!firstChunk)
        {
            yield return new StreamingMessageDto { Author = currentAuthor, ContentPiece = "", IsComplete = true, ServerSessionId = serverSessionId };
        }
    }
}
