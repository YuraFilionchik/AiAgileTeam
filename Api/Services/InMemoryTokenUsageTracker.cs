using System.Collections.Concurrent;
using AiAgileTeam.Models;

namespace AiAgileTeam.Services;

public sealed class InMemoryTokenUsageTracker : ITokenUsageTracker
{
    private readonly ConcurrentDictionary<string, ConcurrentBag<TokenUsageRecord>> _data = new();

    public event EventHandler<TokenUsageUpdatedEventArgs>? UsageUpdated;

    public void Record(TokenUsageRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        _data.AddOrUpdate(
            record.ExecutionId,
            _ => new ConcurrentBag<TokenUsageRecord>([record]),
            (_, bag) =>
            {
                bag.Add(record);
                return bag;
            });

        UsageUpdated?.Invoke(this, new TokenUsageUpdatedEventArgs(record));
    }

    public TokenUsageSnapshot GetSnapshot(string executionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executionId);

        if (!_data.TryGetValue(executionId, out var records) || records.IsEmpty)
        {
            return new TokenUsageSnapshot(0, 0, 0m, new Dictionary<string, TokenUsageRecord>());
        }

        var materialized = records.ToArray();
        var totalPrompt = materialized.Sum(r => r.PromptTokens);
        var totalCompletion = materialized.Sum(r => r.CompletionTokens);
        var totalCost = materialized.Sum(r => r.EstimatedCost ?? 0m);

        var byAgent = materialized
            .GroupBy(r => r.AgentName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var latest = group.OrderByDescending(r => r.Timestamp).First();
                    return latest with
                    {
                        PromptTokens = group.Sum(r => r.PromptTokens),
                        CompletionTokens = group.Sum(r => r.CompletionTokens),
                        EstimatedCost = group.Sum(r => r.EstimatedCost ?? 0m),
                        ImageTokens = group.Sum(r => r.ImageTokens),
                        MediaCost = group.Sum(r => r.MediaCost)
                    };
                },
                StringComparer.OrdinalIgnoreCase);

        return new TokenUsageSnapshot(totalPrompt, totalCompletion, totalCost, byAgent);
    }

    public void Clear(string executionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
        _data.TryRemove(executionId, out _);
    }
}
