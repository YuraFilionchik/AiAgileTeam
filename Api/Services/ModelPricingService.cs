using Microsoft.Extensions.Options;
using System.Text.Json;
using AiAgileTeam.Models;

namespace AiAgileTeam.Services;

public sealed class GeminiPricingService : IModelPricingService
{
    private readonly IReadOnlyDictionary<string, ModelPricingEntry> _pricing;
    private readonly IReadOnlyDictionary<string, GeminiModelPricing> _geminiPricing;
    private const decimal Thousand = 1_000m;
    private const decimal Million = 1_000_000m;
    private const long TierBoundary = 200_000;

    public GeminiPricingService(
        IOptions<ModelPricingOptions> options,
        IOptions<GeminiPricingConfig> geminiOptions)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(geminiOptions);

        _pricing = options.Value.Models;
        _geminiPricing = geminiOptions.Value.Models;
    }

    public decimal CalculateCost(
        string provider,
        string model,
        int inputTokens,
        int outputTokens,
        string? inputType = "text",
        long? contextLength = null,
        bool useCaching = false,
        long? cachedTokens = null,
        decimal? storageHours = null)
    {
        if (string.Equals(provider, "GoogleGemini", StringComparison.OrdinalIgnoreCase))
        {
            return CalculateGeminiCost(model, inputTokens, outputTokens, inputType, contextLength, useCaching, cachedTokens, storageHours);
        }

        return CalculateCost(model, inputTokens, outputTokens);
    }

    public decimal CalculateCost(string modelId, int promptTokens, int completionTokens)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return 0m;
        }

        if (!_pricing.TryGetValue(modelId, out var configuredPrice)
            && !_pricing.TryGetValue("default", out configuredPrice))
        {
            return 0m;
        }

        var promptCost = (promptTokens / Thousand) * configuredPrice.PromptPer1K;
        var completionCost = (completionTokens / Thousand) * configuredPrice.CompletionPer1K;
        return promptCost + completionCost;
    }

    private decimal CalculateGeminiCost(
        string model,
        int inputTokens,
        int outputTokens,
        string? inputType,
        long? contextLength,
        bool useCaching,
        long? cachedTokens,
        decimal? storageHours)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            return 0m;
        }

        if (!_geminiPricing.TryGetValue(model, out var pricing))
        {
            return 0m;
        }

        decimal cost = 0m;

        var inputPricePerM = GetTieredPrice(pricing.Input, inputType, contextLength);
        cost += (inputTokens / Million) * inputPricePerM;

        var outputPricePerM = GetOutputPrice(pricing.Output, contextLength);
        cost += (outputTokens / Million) * outputPricePerM;

        if (useCaching && pricing.ContextCaching is not null && cachedTokens.HasValue)
        {
            var writePrice = GetTieredPrice(pricing.ContextCaching.Write, inputType, contextLength);
            cost += (cachedTokens.Value / Million) * writePrice;

            if (storageHours is > 0)
            {
                cost += (cachedTokens.Value / Million)
                    * pricing.ContextCaching.StoragePerHourPerMillion
                    * storageHours.Value;
            }
        }

        return Math.Round(cost, 6);
    }

    private static decimal GetTieredPrice(Dictionary<string, decimal> prices, string? inputType, long? contextLength)
    {
        if (prices.Count == 0)
        {
            return 0m;
        }

        if (TryGetByNormalizedKey(prices, inputType, out var typedPrice))
        {
            return typedPrice;
        }

        var isOver200k = contextLength >= TierBoundary;

        if (TryGetTierPrice(prices, isOver200k, out var tierPrice))
        {
            return tierPrice;
        }

        return prices.Values.FirstOrDefault();
    }

    private static decimal GetOutputPrice(object outputConfig, long? contextLength)
    {
        if (outputConfig is decimal fixedPrice)
        {
            return fixedPrice;
        }

        if (outputConfig is JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Number && element.TryGetDecimal(out var number))
            {
                return number;
            }

            if (element.ValueKind == JsonValueKind.Object)
            {
                var dictionary = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
                foreach (var property in element.EnumerateObject())
                {
                    if (property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetDecimal(out var value))
                    {
                        dictionary[property.Name] = value;
                    }
                }

                if (TryGetTierPrice(dictionary, contextLength >= TierBoundary, out var tierPrice))
                {
                    return tierPrice;
                }

                return dictionary.Values.FirstOrDefault();
            }
        }

        if (outputConfig is Dictionary<string, decimal> tiers)
        {
            return TryGetTierPrice(tiers, contextLength >= TierBoundary, out var tierPrice)
                ? tierPrice
                : tiers.Values.FirstOrDefault();
        }

        if (outputConfig is IReadOnlyDictionary<string, decimal> readOnlyTiers)
        {
            var asDictionary = readOnlyTiers.ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);
            return TryGetTierPrice(asDictionary, contextLength >= TierBoundary, out var tierPrice)
                ? tierPrice
                : asDictionary.Values.FirstOrDefault();
        }

        return 0m;
    }

    private static bool TryGetByNormalizedKey(
        IReadOnlyDictionary<string, decimal> prices,
        string? key,
        out decimal value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            value = 0m;
            return false;
        }

        var normalizedTarget = NormalizeKey(key);
        var fallbackTargets = normalizedTarget switch
        {
            "text" or "image" or "video" => new[] { normalizedTarget, "textimagevideo" },
            _ => new[] { normalizedTarget }
        };

        foreach (var priceEntry in prices)
        {
            if (!fallbackTargets.Contains(NormalizeKey(priceEntry.Key), StringComparer.Ordinal))
            {
                continue;
            }

            value = priceEntry.Value;
            return true;
        }

        value = 0m;
        return false;
    }

    private static bool TryGetTierPrice(
        IReadOnlyDictionary<string, decimal> prices,
        bool over200k,
        out decimal value)
    {
        var candidateKeys = over200k
            ? new[] { ">200k", "over200k", "promptsover200k" }
            : new[] { "≤200k", "<=200k", "upTo200k", "promptsupto200k" };

        foreach (var candidate in candidateKeys)
        {
            if (TryGetByNormalizedKey(prices, candidate, out value))
            {
                return true;
            }
        }

        value = 0m;
        return false;
    }

    private static string NormalizeKey(string value)
    {
        var chars = value
            .ToLowerInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray();

        return new string(chars);
    }
}

public sealed class ModelPricingOptions
{
    public Dictionary<string, ModelPricingEntry> Models { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class ModelPricingEntry
{
    public decimal PromptPer1K { get; init; }

    public decimal CompletionPer1K { get; init; }
}
