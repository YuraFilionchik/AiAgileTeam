namespace AiAgileTeam.Models;

public sealed class GeminiPricingConfig
{
    public string Currency { get; set; } = "USD";

    public bool PerMillionTokens { get; set; } = true;

    public Dictionary<string, GeminiModelPricing> Models { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class GeminiModelPricing
{
    public Dictionary<string, decimal> Input { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public object Output { get; set; } = 0m;

    public GeminiCachingPricing? ContextCaching { get; set; }

    public string? Notes { get; set; }
}

public sealed class GeminiCachingPricing
{
    public Dictionary<string, decimal> Write { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public decimal StoragePerHourPerMillion { get; set; }
}
