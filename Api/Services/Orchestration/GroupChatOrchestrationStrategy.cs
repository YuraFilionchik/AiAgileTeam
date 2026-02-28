#pragma warning disable SKEXP0110, SKEXP0070, SKEXP0001
using System.Runtime.CompilerServices;
using AiAgileTeam.Models;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;

namespace AiAgileTeam.Services.Orchestration;

/// <summary>
/// Runs team discussion with AgentGroupChat and custom PM-driven strategies.
/// </summary>
public sealed class GroupChatOrchestrationStrategy : IOrchestrationStrategy
{
    private readonly AiTeamService _teamService;

    public GroupChatOrchestrationStrategy(AiTeamService teamService)
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

        var groupChat = sessionData.GroupChat;

        if (!sessionData.IsConfigured)
        {
            var agentsToRun = settings.Agents.Where(a => a.IsSelected).ToList();
            ChatCompletionAgent? projectManager = null;

            // Build the active team roster so PM knows exactly who is available
            var nonPmMembers = agentsToRun
                .Where(a => a.Role != "Project Manager")
                .Select(a => $"- {a.Role} ({a.DisplayName})")
                .ToList();
            var teamRosterNote = nonPmMembers.Count > 0
                ? $"\n\nIMPORTANT — ACTIVE TEAM MEMBERS FOR THIS SESSION (only these experts are available, do NOT reference anyone else):\n{string.Join("\n", nonPmMembers)}\n" +
                  "Adapt the final SRS document structure to include ONLY sections from the experts listed above."
                : "";

            Console.WriteLine($"[GroupChatOrchestration] Creating agents. Total selected: {agentsToRun.Count}");
            foreach (var agentConfig in agentsToRun)
            {
                Console.WriteLine($"[GroupChatOrchestration]   - {agentConfig.DisplayName} ({agentConfig.Role}) IsSelected={agentConfig.IsSelected}, IsMandatory={agentConfig.IsMandatory}");

                // Inject dynamic team roster into PM's prompt before agent creation (Instructions is init-only)
                if (agentConfig.Role == "Project Manager" && !string.IsNullOrEmpty(teamRosterNote))
                {
                    agentConfig.SystemPrompt += teamRosterNote;
                }

                var agent = _teamService.CreateAgent(agentConfig, settings, serverSessionId);

                groupChat.AddAgent(agent);
                if (agentConfig.Role == "Project Manager")
                {
                    projectManager = agent;
                }
            }

            if (projectManager != null)
            {
                var pmConfig = agentsToRun.First(a => a.Role == "Project Manager");
                ApiConfig pmApiConfig = settings.ApiKeyMode == "global"
                    ? settings.GlobalApi
                    : pmConfig.ApiSettings ?? settings.GlobalApi;
                var pmLlm = _teamService.CreateChatService(pmApiConfig, pmConfig.ModelSettings.Model, "Project Manager", serverSessionId, "Orchestration");

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
                Console.WriteLine($"[GroupChatOrchestration] PM-driven orchestration configured. Agents count: {agentsToRun.Count}");
            }
            else
            {
                Console.WriteLine("[GroupChatOrchestration] WARNING: Project Manager not found in selected agents!");
            }

            sessionData.IsConfigured = true;
            sessionData.OrchestrationMode = OrchestrationMode.GroupChat;
        }

        Exception? caughtException = null;
        IAsyncEnumerator<ChatMessageContent>? enumerator = null;

        try
        {
            enumerator = groupChat.InvokeAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            caughtException = ex;
        }

        if (caughtException is null && enumerator is not null)
        {
            bool hasMore = true;
            while (hasMore)
            {
                ChatMessageContent? current = null;
                try
                {
                    hasMore = await enumerator.MoveNextAsync();
                    if (hasMore) current = enumerator.Current;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    hasMore = false;
                    caughtException = ex;
                }

                if (current is null) continue;

                var author = current.AuthorName ?? "Unknown";
                var hasTextContent = !string.IsNullOrWhiteSpace(current.Content);
                var mediaUrl = TryExtractImageUrl(current);

                if (!hasTextContent && string.IsNullOrWhiteSpace(mediaUrl))
                {
                    Console.WriteLine($"[GroupChatOrchestration] Skipping empty turn from '{author}'");
                    continue;
                }

                Console.WriteLine($"[GroupChatOrchestration] Agent '{author}' responded ({current.Content?.Length ?? 0} chars)");

                // Floor-change system message
                yield return new StreamingMessageDto
                {
                    Author = "System",
                    ContentPiece = $"\r\n*[Project Manager gives floor to {author}...]*\r\n\r\n",
                    IsComplete = true,
                    ServerSessionId = serverSessionId
                };

                // Agent content (IsComplete = false so the client adds it to the UI)
                yield return new StreamingMessageDto
                {
                    Author = author,
                    ContentPiece = hasTextContent ? current.Content! : string.Empty,
                    IsComplete = false,
                    ServerSessionId = serverSessionId,
                    MediaUrl = mediaUrl,
                    MediaMimeType = !string.IsNullOrWhiteSpace(mediaUrl) ? "image/*" : null
                };

                // Turn-completion signal (IsComplete = true, empty content — triggers client finalization)
                yield return new StreamingMessageDto
                {
                    Author = author,
                    ContentPiece = string.Empty,
                    IsComplete = true,
                    ServerSessionId = serverSessionId
                };
            }

            await enumerator.DisposeAsync();
        }

        if (caughtException != null)
        {
            var hint = string.Empty;
            try
            {
                var msg = caughtException.Message ?? string.Empty;
                if (msg.Contains("404") || msg.Contains("Not Found", StringComparison.OrdinalIgnoreCase) || msg.Contains("Response status code does not indicate success", StringComparison.OrdinalIgnoreCase))
                {
                    hint = "\r\nHint: Provider returned 404 Not Found. Check provider settings (ApiKey, Endpoint) and the model name — the model may be unavailable for your account or the name is incorrect.\r\n";
                }
            }
            catch
            {
                // no-op
            }

            Console.WriteLine($"[ERROR] Discussion exception: {caughtException}");
            yield return new StreamingMessageDto
            {
                Author = "System",
                ContentPiece = $"\r\n⚠️ Discussion error: {caughtException.Message}\r\n{hint}",
                IsComplete = true,
                ServerSessionId = serverSessionId
            };
        }
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
