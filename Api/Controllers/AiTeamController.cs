#pragma warning disable SKEXP0110, SKEXP0070, SKEXP0001
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.ChatCompletion;
using AiAgileTeam.Models;
using AiAgileTeam.Services;

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
                Instructions = "You are a Clarification Agent. Ask the user a maximum of 3 clarifying questions one by one. After receiving the answers, provide a summary and suggest a refined query. Say [READY] when you have collected enough info.",
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
                Instructions = "You are a Clarification Agent. Ask the user a maximum of 3 clarifying questions one by one. After receiving the answers, provide a summary and suggest a refined query. Say [READY] when you have collected enough info.",
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
            ChatCompletionAgent? scrumMaster = null;

            foreach (var agentConfig in agentsToRun)
            {
                var agent = _teamService.CreateAgent(agentConfig, settings);
                skAgents.Add(agent);
                groupChat.AddAgent(agent);
                if (agent.Name == "Scrum Master") { scrumMaster = agent; }
            }

            if (scrumMaster != null)
            {
                var agentNames = string.Join(", ", skAgents.Select(a => $"'{a.Name}'"));
                groupChat.ExecutionSettings = new()
                {
                    SelectionStrategy = new KernelFunctionSelectionStrategy(
                        KernelFunctionFactory.CreateFromPrompt(
                            $$$"""
                            You are the moderator deciding who speaks next in a Scrum team discussion.
                            
                            ## Conversation so far:
                            {{$history}}
                            
                            ## Rules:
                            1. If the conversation just started or has no messages, choose 'Scrum Master'.
                            2. After Scrum Master sets the agenda, choose the most relevant specialist.
                            3. Do NOT let the same specialist speak twice in a row — rotate between experts.
                            4. After 2-3 specialists have spoken, return to 'Scrum Master' to synthesize.
                            5. If Scrum Master said '[DONE]', choose 'Scrum Master' to conclude.
                            
                            ## Available participants:
                            {{{agentNames}}}
                            
                            Respond with ONLY the exact name. No quotes, no explanation.
                            """
                        ),
                        scrumMaster.Kernel)
                    {
                        ResultParser = (result) => {
                            var val = result.GetValue<string>();
                            if (val == null) return "Scrum Master";
                            // Trim quotes, whitespace, and common punctuation
                            return val.Trim('"', '\'', ' ', '\n', '\r', '.', ',', ';', ':').Trim();
                        },
                        HistoryVariableName = "history"
                    },
                    TerminationStrategy = new KernelFunctionTerminationStrategy(
                        KernelFunctionFactory.CreateFromPrompt(
                            $$$"""
                            Review the conversation below and determine if the team discussion is complete.
                            
                            ## Conversation:
                            {{$history}}
                            
                            ## Completion criteria:
                            - The Scrum Master has presented a final, comprehensive plan OR the team has reached a consensus.
                            - The original user request is addressed.
                            - OR the conversation is clearly stalling or repeating itself.
                            - The Scrum Master has concluded with exactly '[DONE]'.
                            
                            IMPORTANT: If the plan is ready, even if '[DONE]' is missing but the context implies finality, respond 'yes'.
                            
                            Respond with ONLY 'yes' if complete, or 'no' otherwise.
                            """
                        ),
                        scrumMaster.Kernel)
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
                            yield return new StreamingMessageDto { Author = "System", ContentPiece = $"\r\n*[Scrum Master gives floor to {currentAuthor}...]*\r\n\r\n", IsComplete = true, ServerSessionId = serverSessionId };
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
