using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace AiAgileTeam.Services;

/// <summary>
/// Compresses chat history for agents: keeps the last N messages verbatim
/// and summarizes older messages into a single compact paragraph via LLM.
/// Caches the running summary so only new messages are re-summarized.
/// </summary>
public sealed class ChatContextCompressor
{
    private readonly IChatCompletionService _llm;

    /// <summary>Number of most-recent messages kept verbatim (not summarized).</summary>
    private readonly int _tailSize;

    /// <summary>Cached running summary of messages already processed.</summary>
    private string _runningSummary = "";

    /// <summary>Number of messages already covered by <see cref="_runningSummary"/>.</summary>
    private int _summarizedCount;

    private static readonly string SummarizeSystemPrompt =
        """
        You are a concise summarizer for a software project discussion.
        You receive the previous running summary (may be empty) and a batch of new messages.
        Produce ONE compact paragraph (max 300 words) that captures:
        - key decisions and agreements
        - open questions and unresolved issues
        - each participant's main contribution
        Do NOT add opinions. Do NOT use markdown headers. Write in the same language as the messages.
        """;

    public ChatContextCompressor(IChatCompletionService llm, int tailSize = 4)
    {
        ArgumentNullException.ThrowIfNull(llm);
        if (tailSize < 1) throw new ArgumentOutOfRangeException(nameof(tailSize));

        _llm = llm;
        _tailSize = tailSize;
    }

    /// <summary>
    /// Returns a compact representation of the full history suitable for agent context.
    /// </summary>
    public async Task<CompressedContext> CompressAsync(
        IReadOnlyList<ChatMessageContent> fullHistory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fullHistory);

        // Nothing to compress — return as-is
        if (fullHistory.Count <= _tailSize)
        {
            return new CompressedContext(
                Summary: "",
                RecentMessages: fullHistory.ToList());
        }

        int boundaryIndex = fullHistory.Count - _tailSize;

        // Only re-summarize if there are new messages beyond what we already covered
        if (boundaryIndex > _summarizedCount)
        {
            var newBatch = fullHistory
                .Skip(_summarizedCount)
                .Take(boundaryIndex - _summarizedCount)
                .ToList();

            _runningSummary = await SummarizeBatchAsync(
                _runningSummary, newBatch, cancellationToken);
            _summarizedCount = boundaryIndex;
        }

        var recentMessages = fullHistory.Skip(boundaryIndex).ToList();

        return new CompressedContext(
            Summary: _runningSummary,
            RecentMessages: recentMessages);
    }

    /// <summary>
    /// Formats compressed context as a single text block for injection into a prompt.
    /// </summary>
    public static string FormatForPrompt(CompressedContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(ctx.Summary))
        {
            parts.Add($"[Discussion summary so far]\n{ctx.Summary}");
        }

        if (ctx.RecentMessages.Count > 0)
        {
            parts.Add("[Recent messages]");
            foreach (var msg in ctx.RecentMessages)
            {
                string author = msg.AuthorName ?? msg.Role.ToString();
                parts.Add($"{author}: {msg.Content}");
            }
        }

        return string.Join("\n\n", parts);
    }

    /// <summary>Resets cached state. Call when starting a new discussion round.</summary>
    public void Reset()
    {
        _runningSummary = "";
        _summarizedCount = 0;
    }

    private async Task<string> SummarizeBatchAsync(
        string previousSummary,
        IReadOnlyList<ChatMessageContent> newMessages,
        CancellationToken cancellationToken)
    {
        var history = new ChatHistory();
        history.AddSystemMessage(SummarizeSystemPrompt);

        var userContent = new System.Text.StringBuilder();
        if (!string.IsNullOrWhiteSpace(previousSummary))
        {
            userContent.AppendLine("[Previous summary]");
            userContent.AppendLine(previousSummary);
            userContent.AppendLine();
        }

        userContent.AppendLine("[New messages to incorporate]");
        foreach (var msg in newMessages)
        {
            string author = msg.AuthorName ?? msg.Role.ToString();
            userContent.AppendLine($"{author}: {msg.Content}");
        }

        history.AddUserMessage(userContent.ToString());

        var result = await _llm.GetChatMessageContentAsync(
            history,
            executionSettings: new PromptExecutionSettings
            {
                ExtensionData = new Dictionary<string, object>
                {
                    ["max_tokens"] = 400,
                    ["temperature"] = 0.2
                }
            },
            cancellationToken: cancellationToken);

        return result.Content ?? previousSummary;
    }
}

/// <summary>
/// Holds a compressed view of the conversation: an LLM-generated summary of older messages
/// plus the most recent messages in full.
/// </summary>
public sealed record CompressedContext(
    string Summary,
    IReadOnlyList<ChatMessageContent> RecentMessages);
