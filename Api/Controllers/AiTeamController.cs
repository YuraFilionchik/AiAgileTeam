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
[Route("api/[controller]")]
public class AiTeamController : ControllerBase
{
    private readonly AiTeamService _teamService;

    public AiTeamController(AiTeamService teamService)
    {
        _teamService = teamService;
    }

    [HttpPost("session")]
    public async IAsyncEnumerable<StreamingMessageDto> StartSession([FromBody] SessionRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var groupChat = new AgentGroupChat();
        
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
            
            await foreach (var content in ProcessAgentResponseAsync(clarificationAgent, groupChat, cancellationToken))
            {
                yield return content;
            }
        }
        else
        {
            groupChat.AddChatMessage(new Microsoft.SemanticKernel.ChatMessageContent(AuthorRole.User, request.Query));
            await foreach (var content in RunTeamDiscussionAsync(request.Settings, groupChat, cancellationToken))
            {
                yield return content;
            }
        }
    }

    [HttpPost("message")]
    public async IAsyncEnumerable<StreamingMessageDto> SendMessage([FromBody] SessionRequest request, [FromQuery] bool isClarificationPhase, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var groupChat = new AgentGroupChat();
        
        // Reconstruct chat history in a real scenario we'd pass history, for MVP MVP we will just pass the query as the single context 
        // Note: For a true group chat history we'd need to pass ChatMessage list from frontend and add them all:
        /*
        foreach(var msg in request.History) {
            groupChat.AddChatMessage(new Microsoft.SemanticKernel.ChatMessageContent(msg.IsUser ? AuthorRole.User : AuthorRole.Assistant, msg.Content));
        }
        */
        // MVP: Just add User query
        groupChat.AddChatMessage(new Microsoft.SemanticKernel.ChatMessageContent(AuthorRole.User, request.Query));

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

            await foreach (var content in ProcessAgentResponseAsync(clarificationAgent, groupChat, cancellationToken))
            {
                yield return content;
            }
        }
        else
        {
            await foreach (var content in RunTeamDiscussionAsync(request.Settings, groupChat, cancellationToken))
            {
                yield return content;
            }
        }
    }

    private async IAsyncEnumerable<StreamingMessageDto> ProcessAgentResponseAsync(ChatCompletionAgent agent, AgentGroupChat groupChat, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        string currentContent = "";
        
        await foreach (var message in groupChat.InvokeAsync(agent).WithCancellation(cancellationToken))
        {
            currentContent += message.Content;
            yield return new StreamingMessageDto
            {
                Author = agent.Name,
                ContentPiece = message.Content,
                IsComplete = false
            };
        }
        
        yield return new StreamingMessageDto { Author = agent.Name, ContentPiece = "", IsComplete = true };
    }

    private async IAsyncEnumerable<StreamingMessageDto> RunTeamDiscussionAsync(AppSettings settings, AgentGroupChat groupChat, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var agentsToRun = settings.Agents.Where(a => a.IsSelected).ToList();
        
        foreach(var agentConfig in agentsToRun)
        {
            var agent = _teamService.CreateAgent(agentConfig, settings);
            
            await foreach(var message in groupChat.InvokeAsync(agent).WithCancellation(cancellationToken))
            {
                yield return new StreamingMessageDto
                {
                    Author = agent.Name,
                    ContentPiece = message.Content,
                    IsComplete = false
                };
            }
            yield return new StreamingMessageDto { Author = agent.Name, ContentPiece = "", IsComplete = true };
        }
    }
}
