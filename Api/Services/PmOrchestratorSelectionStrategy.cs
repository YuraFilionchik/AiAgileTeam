using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.ChatCompletion;

namespace AiAgileTeam.Services;

/// <summary>
/// Selection strategy where the Project Manager LLM decides which agent speaks next
/// based on a compressed conversation context. Includes anti-loop guards and @mention support.
/// </summary>
public sealed class PmOrchestratorSelectionStrategy : SelectionStrategy
{
    private readonly IChatCompletionService _pmLlm;
    private readonly ChatContextCompressor _compressor;

    /// <summary>Maximum times the same agent can speak consecutively before forcing rotation.</summary>
    private const int MaxConsecutiveSameAgent = 2;

    /// <summary>Maximum LLM retries when PM returns an unparseable response.</summary>
    private const int MaxParseRetries = 2;

    private string? _lastSelectedRole;
    private int _consecutiveCount;

    private static readonly string OrchestratorSystemPrompt =
        """
        You are the orchestrator of a software team discussion.
        Based on the discussion context, decide which team member should speak NEXT.

        Available roles: {ROLES}

        Rules:
        1. Each expert should speak at least once before the discussion ends.
        2. Do not select the same expert more than twice in a row.
        3. If all experts have contributed and the discussion is complete, respond with exactly: DONE
        4. Otherwise respond with ONLY the role name (e.g. "Architect" or "QA Engineer"). Nothing else.

        Respond with a single line: either the exact role name or DONE.
        """;

    public PmOrchestratorSelectionStrategy(
        IChatCompletionService pmLlm,
        ChatContextCompressor compressor)
    {
        ArgumentNullException.ThrowIfNull(pmLlm);
        ArgumentNullException.ThrowIfNull(compressor);

        _pmLlm = pmLlm;
        _compressor = compressor;
    }

    /// <summary>Resets internal state for a new discussion round.</summary>
    public void Reset()
    {
        _lastSelectedRole = null;
        _consecutiveCount = 0;
        _compressor.Reset();
    }

    protected override async Task<Agent> SelectAgentAsync(
        IReadOnlyList<Agent> agents,
        IReadOnlyList<ChatMessageContent> history,
        CancellationToken cancellationToken = default)
    {
        var lastMessage = history.LastOrDefault();

        // 1. User @mention — direct handoff without LLM call
        if (lastMessage is not null && lastMessage.Role == AuthorRole.User)
        {
            var mentioned = TryFindMentionedAgent(agents, lastMessage.Content);
            if (mentioned is not null)
            {
                TrackSelection(GetAgentRole(mentioned));
                return mentioned;
            }

            // User interrupted — PM should re-evaluate, so give floor to PM first
            var pm = FindAgentByRole(agents, "Project Manager");
            if (pm is not null)
            {
                TrackSelection("Project Manager");
                return pm;
            }
        }

        // 2. Compress history
        var compressed = await _compressor.CompressAsync(history, cancellationToken);
        string contextText = ChatContextCompressor.FormatForPrompt(compressed);

        // 3. Build role list (excluding PM — PM is the orchestrator, not a discussion participant here)
        var nonPmAgents = agents.Where(a => GetAgentRole(a) != "Project Manager").ToList();
        var allRoles = nonPmAgents.Select(GetAgentRole).Distinct().ToList();
        string rolesStr = string.Join(", ", allRoles);

        // 4. Ask PM LLM to pick next speaker
        string? chosenRole = null;
        for (int attempt = 0; attempt <= MaxParseRetries; attempt++)
        {
            chosenRole = await AskPmForNextSpeakerAsync(
                contextText, rolesStr, cancellationToken);

            if (chosenRole is not null)
                break;
        }

        // 5. Handle DONE — hand back to PM agent for final synthesis
        if (string.Equals(chosenRole, "DONE", StringComparison.OrdinalIgnoreCase))
        {
            var pm = FindAgentByRole(agents, "Project Manager")
                     ?? throw new InvalidOperationException("Project Manager agent not found");
            TrackSelection("Project Manager");
            return pm;
        }

        // 6. Anti-loop guard
        if (chosenRole is not null
            && chosenRole == _lastSelectedRole
            && _consecutiveCount >= MaxConsecutiveSameAgent)
        {
            // Force a different agent
            var alternative = allRoles.FirstOrDefault(r => r != _lastSelectedRole);
            if (alternative is not null)
            {
                chosenRole = alternative;
            }
        }

        // 7. Resolve agent by role
        if (chosenRole is not null)
        {
            var agent = FindAgentByRole(agents, chosenRole);
            if (agent is not null)
            {
                TrackSelection(chosenRole);
                return agent;
            }
        }

        // 8. Fallback: round-robin through non-PM agents that haven't spoken recently
        var fallback = PickFallbackAgent(agents, history);
        TrackSelection(GetAgentRole(fallback));
        return fallback;
    }

    private async Task<string?> AskPmForNextSpeakerAsync(
        string contextText,
        string availableRoles,
        CancellationToken cancellationToken)
    {
        var prompt = OrchestratorSystemPrompt.Replace("{ROLES}", availableRoles);

        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(prompt);
        chatHistory.AddUserMessage(contextText);

        try
        {
            var result = await _pmLlm.GetChatMessageContentAsync(
                chatHistory,
                executionSettings: new PromptExecutionSettings
                {
                    ExtensionData = new Dictionary<string, object>
                    {
                        ["max_tokens"] = 30,
                        ["temperature"] = 0.1
                    }
                },
                cancellationToken: cancellationToken);

            var response = result.Content?.Trim();
            if (string.IsNullOrWhiteSpace(response))
                return null;

            // Check for DONE
            if (response.Contains("DONE", StringComparison.OrdinalIgnoreCase))
                return "DONE";

            // Try to find a known role in the response
            foreach (var role in availableRoles.Split(',', StringSplitOptions.TrimEntries))
            {
                if (response.Contains(role, StringComparison.OrdinalIgnoreCase))
                    return role;
            }

            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.WriteLine($"[PmOrchestrator] LLM call failed: {ex.Message}");
            return null;
        }
    }

    private void TrackSelection(string role)
    {
        if (role == _lastSelectedRole)
        {
            _consecutiveCount++;
        }
        else
        {
            _lastSelectedRole = role;
            _consecutiveCount = 1;
        }
    }

    private static Agent PickFallbackAgent(
        IReadOnlyList<Agent> agents,
        IReadOnlyList<ChatMessageContent> history)
    {
        // Find agents who haven't spoken yet
        var spokenRoles = history
            .Where(m => m.Role == AuthorRole.Assistant && !string.IsNullOrEmpty(m.AuthorName))
            .Select(m => m.AuthorName!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var unspoken = agents.FirstOrDefault(a =>
            GetAgentRole(a) != "Project Manager"
            && !spokenRoles.Contains(a.Name ?? ""));

        if (unspoken is not null)
            return unspoken;

        // Everyone has spoken — give floor to PM for synthesis
        return FindAgentByRole(agents, "Project Manager")
               ?? agents[0];
    }

    private static Agent? TryFindMentionedAgent(
        IReadOnlyList<Agent> agents, string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return null;

        foreach (var agent in agents)
        {
            if (content.Contains($"@{agent.Name}", StringComparison.OrdinalIgnoreCase))
                return agent;
        }

        foreach (var agent in agents)
        {
            var role = GetAgentRole(agent);
            if (content.Contains($"@{role}", StringComparison.OrdinalIgnoreCase))
                return agent;
        }

        return null;
    }

    private static Agent? FindAgentByRole(IReadOnlyList<Agent> agents, string role)
    {
        return agents.FirstOrDefault(a =>
            string.Equals(GetAgentRole(a), role, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Extracts agent role from instructions formatted as:
    /// "Your name is {DisplayName}. You are a {Role}. {SystemPrompt}"
    /// </summary>
    private static string GetAgentRole(Agent agent)
    {
        var instructions = agent.Instructions ?? "";
        var match = System.Text.RegularExpressions.Regex.Match(
            instructions, @"You are a ([^.]+)\.");
        return match.Success ? match.Groups[1].Value.Trim() : "Unknown";
    }
}
