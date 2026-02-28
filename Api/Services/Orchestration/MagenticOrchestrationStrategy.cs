using System.Runtime.CompilerServices;
using System.Threading.Channels;
using AiAgileTeam.Models;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Magentic;
using Microsoft.SemanticKernel.Agents.Orchestration;
using Microsoft.SemanticKernel.Agents.Runtime.InProcess;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace AiAgileTeam.Services.Orchestration;

/// <summary>
/// Runs team discussion using Semantic Kernel Magentic orchestration.
/// </summary>
public sealed class MagenticOrchestrationStrategy : IOrchestrationStrategy
{
    private const int FinalResultTimeoutSeconds = 300;
    private readonly AiTeamService _teamService;

    public MagenticOrchestrationStrategy(AiTeamService teamService)
    {
        ArgumentNullException.ThrowIfNull(teamService);
        _teamService = teamService;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<StreamingMessageDto> RunDiscussionAsync(
        AppSettings settings,
        SessionData sessionData,
        string serverSessionId,
        string userQuery,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(sessionData);
        ArgumentException.ThrowIfNullOrWhiteSpace(serverSessionId);

        var selectedAgents = settings.Agents.Where(a => a.IsSelected).ToList();
        if (selectedAgents.Count == 0)
        {
            throw new InvalidOperationException("At least one selected agent is required for Magentic orchestration.");
        }

        var pmConfig = selectedAgents.FirstOrDefault(a => a.Role == "Project Manager");
        if (pmConfig is null)
        {
            throw new InvalidOperationException("Project Manager agent is required for Magentic orchestration.");
        }

        var agents = new List<Agent>(selectedAgents.Count);
        foreach (var agentConfig in selectedAgents)
        {
            agents.Add(_teamService.CreateAgent(agentConfig, settings, serverSessionId));
        }

        ApiConfig pmApiConfig = settings.ApiKeyMode == "global"
            ? settings.GlobalApi
            : pmConfig.ApiSettings ?? settings.GlobalApi;
        IChatCompletionService managerLlm = _teamService.CreateChatService(pmApiConfig, pmConfig.ModelSettings.Model, "Project Manager", serverSessionId, "Orchestration");

        var manager = new StandardMagenticManager(
            managerLlm,
            new OpenAIPromptExecutionSettings { Temperature = 0.1f })
        {
            MaximumInvocationCount = selectedAgents.Count * 3 + 3
        };

        var streamChannel = Channel.CreateUnbounded<StreamingMessageDto>();
        sessionData.IsConfigured = true;
        sessionData.OrchestrationMode = OrchestrationMode.Magentic;

        ValueTask OnResponseAsync(ChatMessageContent message)
        {
            var imageUrl = TryExtractImageUrl(message);
            if (string.IsNullOrWhiteSpace(message.Content) && string.IsNullOrWhiteSpace(imageUrl))
            {
                return ValueTask.CompletedTask;
            }

            streamChannel.Writer.TryWrite(new StreamingMessageDto
            {
                Author = string.IsNullOrWhiteSpace(message.AuthorName) ? "Agent" : message.AuthorName,
                ContentPiece = message.Content ?? string.Empty,
                IsComplete = true,
                ServerSessionId = serverSessionId,
                MediaUrl = imageUrl,
                MediaMimeType = !string.IsNullOrWhiteSpace(imageUrl) ? "image/*" : null
            });

            return ValueTask.CompletedTask;
        }

        var orchestration = new MagenticOrchestration(manager, [.. agents])
        {
            ResponseCallback = OnResponseAsync
        };

        var runtime = new InProcessRuntime();
        await runtime.StartAsync();

        var task = string.IsNullOrWhiteSpace(userQuery)
            ? "Continue the team discussion using the existing context and produce a final synthesis."
            : userQuery;

        var orchestrationTask = Task.Run(async () =>
        {
            try
            {
                OrchestrationResult<string> result = await orchestration.InvokeAsync(task, runtime);
                string final = await result.GetValueAsync(TimeSpan.FromSeconds(FinalResultTimeoutSeconds), cancellationToken);

                if (!string.IsNullOrWhiteSpace(final))
                {
                    var finalContent = final.Contains("[DONE]", StringComparison.OrdinalIgnoreCase)
                        ? final
                        : $"{final}\n\n[DONE]";

                    streamChannel.Writer.TryWrite(new StreamingMessageDto
                    {
                        Author = "Project Manager",
                        ContentPiece = finalContent,
                        IsComplete = true,
                        ServerSessionId = serverSessionId
                    });
                }
            }
            finally
            {
                streamChannel.Writer.TryComplete();
                await runtime.RunUntilIdleAsync();
            }
        }, cancellationToken);

        await foreach (var message in streamChannel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return message;
        }

        await orchestrationTask;
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
