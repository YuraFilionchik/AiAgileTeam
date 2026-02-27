using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.ChatCompletion;

namespace AiAgileTeam.Services;

/// <summary>
/// Robust termination strategy that detects:
/// 1. [DONE] token from Project Manager (normal completion)
/// 2. Repetition loops (same agent selected too many times in a row)
/// 3. Stale content (last N messages are near-identical)
/// MaximumIterations (inherited) serves as the hard safety net.
/// </summary>
public sealed class SafeTerminationStrategy : TerminationStrategy
{
    /// <summary>How many consecutive turns by the same agent trigger a forced stop.</summary>
    private const int MaxConsecutiveSameAgent = 3;

    /// <summary>How many recent messages to check for staleness.</summary>
    private const int StalenessWindow = 4;

    /// <summary>Similarity ratio (0-1) above which two messages are considered identical.</summary>
    private const double StalenessThreshold = 0.85;

    protected override Task<bool> ShouldAgentTerminateAsync(
        Agent agent,
        IReadOnlyList<ChatMessageContent> history,
        CancellationToken cancellationToken = default)
    {
        // 1. Normal completion: PM said [DONE]
        var lastMessage = history.LastOrDefault();
        if (lastMessage is not null
            && string.Equals(lastMessage.AuthorName, "Project Manager", StringComparison.OrdinalIgnoreCase)
            && lastMessage.Content is not null
            && lastMessage.Content.Contains("[DONE]", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("[SafeTermination] PM said [DONE] — terminating.");
            return Task.FromResult(true);
        }

        // 2. Loop detection: same author N times in a row
        if (history.Count >= MaxConsecutiveSameAgent)
        {
            var tail = history
                .Skip(history.Count - MaxConsecutiveSameAgent)
                .ToList();

            bool allSameAuthor = tail
                .All(m => string.Equals(m.AuthorName, tail[0].AuthorName, StringComparison.OrdinalIgnoreCase));

            if (allSameAuthor && !string.IsNullOrEmpty(tail[0].AuthorName))
            {
                Console.WriteLine(
                    $"[SafeTermination] Loop detected: {tail[0].AuthorName} spoke {MaxConsecutiveSameAgent} times in a row — terminating.");
                return Task.FromResult(true);
            }
        }

        // 3. Staleness detection: last N messages are near-identical
        if (history.Count >= StalenessWindow)
        {
            var recentContents = history
                .Skip(history.Count - StalenessWindow)
                .Where(m => m.Role == AuthorRole.Assistant && !string.IsNullOrWhiteSpace(m.Content))
                .Select(m => m.Content!)
                .ToList();

            if (recentContents.Count >= StalenessWindow - 1 && AreAllSimilar(recentContents))
            {
                Console.WriteLine("[SafeTermination] Stale content detected — terminating.");
                return Task.FromResult(true);
            }
        }

        return Task.FromResult(false);
    }

    /// <summary>
    /// Checks whether all strings in the list are similar to the first one
    /// using a simple Jaccard similarity on word sets.
    /// </summary>
    private static bool AreAllSimilar(IReadOnlyList<string> texts)
    {
        if (texts.Count < 2)
            return false;

        var baseWords = Tokenize(texts[0]);
        for (int i = 1; i < texts.Count; i++)
        {
            var otherWords = Tokenize(texts[i]);
            double similarity = JaccardSimilarity(baseWords, otherWords);
            if (similarity < StalenessThreshold)
                return false;
        }

        return true;
    }

    private static HashSet<string> Tokenize(string text)
    {
        return text
            .Split([' ', '\n', '\r', '\t', '.', ',', ';', ':', '!', '?'],
                   StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.ToLowerInvariant())
            .ToHashSet();
    }

    private static double JaccardSimilarity(HashSet<string> a, HashSet<string> b)
    {
        if (a.Count == 0 && b.Count == 0)
            return 1.0;

        int intersection = a.Count(w => b.Contains(w));
        int union = a.Count + b.Count - intersection;
        return union == 0 ? 1.0 : (double)intersection / union;
    }
}
