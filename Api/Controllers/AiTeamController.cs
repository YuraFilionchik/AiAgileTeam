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

    public AiTeamController(AiTeamService teamService, SessionStore sessionStore)
    {
        _teamService = teamService;
        _sessionStore = sessionStore;
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
            ProviderConfig clarifyProvider = request.Settings.Mode == "global" ? request.Settings.Global : request.Settings.Agents.FirstOrDefault()?.ProviderSettings ?? new ProviderConfig();
            
            var cBuilder = Kernel.CreateBuilder();
            cBuilder.Services.AddSingleton(_teamService.CreateChatService(clarifyProvider));
            
            clarificationAgent = new ChatCompletionAgent
            {
                Name = "Clarification Agent",
                Instructions = "You are a Requirements Analyst. Your goal is to refine the user's initial request into a clear project brief.\n\n" +
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
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1, Unit.Inch);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(11));

                page.Header().PaddingBottom(10).Text(request.Title).SemiBold().FontSize(20).FontColor(Colors.Blue.Medium);

                page.Content().Column(column =>
                {
                    column.Spacing(10);

                    foreach (var message in request.Messages)
                    {
                        column.Item().PaddingVertical(5).Column(msgCol =>
                        {
                            msgCol.Item().Text(message.Author).Bold().FontSize(10).FontColor(message.IsUser ? Colors.Green.Medium : Colors.Grey.Darken2);
                            msgCol.Item().Text(message.Content);
                        });
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
             ProviderConfig clarifyProvider = request.Settings.Mode == "global" ? request.Settings.Global : request.Settings.Agents.FirstOrDefault()?.ProviderSettings ?? new ProviderConfig();
            
            var cBuilder = Kernel.CreateBuilder();
            cBuilder.Services.AddSingleton(_teamService.CreateChatService(clarifyProvider));
            
            var clarificationAgent = new ChatCompletionAgent
            {
                Name = "Clarification Agent",
                Instructions = "You are a Requirements Analyst. Your goal is to refine the user's initial request into a clear project brief.\n\n" +
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
                        Author = agent.Name, // Author is always the clarification agent
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
            yield return new StreamingMessageDto { Author = agent.Name, ContentPiece = "", IsComplete = true, ServerSessionId = serverSessionId };
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

            foreach (var agentConfig in agentsToRun)
            {
                var agent = _teamService.CreateAgent(agentConfig, settings);
                skAgents.Add(agent);
                groupChat.AddAgent(agent);
                if (agent.Name == "Project Manager") { projectManager = agent; }
            }

            if (projectManager != null)
            {
                var agentNames = string.Join(", ", skAgents.Select(a => $"'{a.Name}'"));
                groupChat.ExecutionSettings = new()
                {
                    SelectionStrategy = new KernelFunctionSelectionStrategy(
                        KernelFunctionFactory.CreateFromPrompt(
                            $$$"""
                            You are the moderator deciding who speaks next in a software development team discussion.
                            Your goal is to ensure a logical flow from requirements to design and finally to planning.
                            
                            ## Conversation so far:
                            {{$history}}
                            
                            ## Orchestration Logic:
                            1. **Initialization**: If the conversation just started, choose 'Project Manager'.
                            2. **Specialist Input**: After Project Manager sets the stage, call specialists in this preferred order:
                               - 'Product Owner' for requirements and user stories.
                               - 'Architect' for high-level design and tech stack.
                               - 'Developer' for implementation details.
                               - 'QA Engineer' for testing and quality.
                               - 'Scrum Master' for agile process and roadmap.
                            3. **Synthesis**: After each major specialist contribution, you may return to 'Project Manager' to summarize or ask for another specialist's input.
                            4. **Finalization**: If all relevant areas are covered, choose 'Project Manager' to provide the final SRS document.
                            5. **Termination**: If 'Project Manager' has already provided the final plan and said '[DONE]', choose 'Project Manager' to officially end.

                            ## Constraints:
                            - Do NOT let the same specialist speak twice in a row.
                            - Ensure all participants get a chance to contribute if their expertise is needed for the user's task.
                            
                            ## Available participants:
                            {{{agentNames}}}
                            
                            Respond with ONLY the exact name. No quotes, no explanation.
                            """
                        ),
                        projectManager.Kernel)
                    {
                        ResultParser = (result) => {
                            var val = result.GetValue<string>();
                            if (val == null) return "Project Manager";
                            // Trim quotes, whitespace, and common punctuation
                            return val.Trim('"', '\'', ' ', '\n', '\r', '.', ',', ';', ':').Trim();
                        },
                        HistoryVariableName = "history"
                    },
                    TerminationStrategy = new KernelFunctionTerminationStrategy(
                        KernelFunctionFactory.CreateFromPrompt(
                            $$$"""
                            Review the conversation below and determine if the team has successfully completed the software planning task.
                            
                            ## Conversation:
                            {{$history}}
                            
                            ## Termination Criteria (Must meet all):
                            1. All requested aspects (requirements, architecture, implementation, QA) have been discussed by relevant experts.
                            2. The 'Project Manager' has delivered a final, structured summary or SRS document.
                            3. The 'Project Manager' has explicitly concluded with '[DONE]'.
                            4. If the conversation is clearly looping or no new information is being added, you may terminate.
                            
                            Respond with ONLY 'yes' if complete, or 'no' otherwise.
                            """
                        ),
                        projectManager.Kernel)
                    {
                        ResultParser = (result) => {
                            var val = result.GetValue<string>();
                            if (val == null) return false;
                            var trimmed = val.Trim('"', '\'', ' ', '\n', '\r', '.', ',', ';', ':');
                            return (trimmed?.Contains("yes", StringComparison.OrdinalIgnoreCase) ?? false) || 
                                   (trimmed?.Contains("true", StringComparison.OrdinalIgnoreCase) ?? false);
                        },
                        MaximumIterations = agentsToRun.Count * 2 + 2, // Sane limit based on team size
                        HistoryVariableName = "history"
                    }
                };
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
                    
                    // Only switch author if the new author is non-null and different from current
                    if (firstChunk || (!string.IsNullOrEmpty(message.AuthorName) && message.AuthorName != currentAuthor))
                    {
                        if (!firstChunk && !string.IsNullOrEmpty(message.AuthorName))
                        {
                            yield return new StreamingMessageDto { Author = currentAuthor, ContentPiece = "", IsComplete = true, ServerSessionId = serverSessionId };
                        }
                        
                        if (!string.IsNullOrEmpty(message.AuthorName))
                        {
                            currentAuthor = message.AuthorName;
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

                    if (!string.IsNullOrEmpty(message.Content))
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
