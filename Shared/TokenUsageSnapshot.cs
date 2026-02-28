namespace AiAgileTeam.Models;

public record TokenUsageSnapshot(
    int TotalPrompt,
    int TotalCompletion,
    decimal TotalCost,
    IReadOnlyDictionary<string, TokenUsageRecord> ByAgent);
