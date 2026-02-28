namespace AiAgileTeam.Models;

public record TokenUsageRecord(
    string ExecutionId,
    string AgentName,
    string Step,
    string ModelId,
    int PromptTokens,
    int CompletionTokens,
    DateTime Timestamp,
    decimal? EstimatedCost = null,
    int ImageTokens = 0,
    decimal MediaCost = 0m);
