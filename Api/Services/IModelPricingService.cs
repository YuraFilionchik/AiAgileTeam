namespace AiAgileTeam.Services;

public interface IModelPricingService
{
    decimal CalculateCost(
        string provider,
        string model,
        int inputTokens,
        int outputTokens,
        string? inputType = "text",
        long? contextLength = null,
        bool useCaching = false,
        long? cachedTokens = null,
        decimal? storageHours = null);

    decimal CalculateCost(string modelId, int promptTokens, int completionTokens);
}
