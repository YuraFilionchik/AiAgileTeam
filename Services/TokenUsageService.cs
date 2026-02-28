using System.Net.Http.Json;
using System.Text.Json;
using AiAgileTeam.Models;

namespace AiAgileTeam.Services;

public sealed class TokenUsageService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _httpClientFactory;

    public TokenUsageService(IHttpClientFactory httpClientFactory)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        _httpClientFactory = httpClientFactory;
    }

    public async Task<TokenUsageSnapshot> GetSnapshotAsync(string executionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(executionId))
        {
            return Empty();
        }

        try
        {
            using var httpClient = _httpClientFactory.CreateClient("ApiClient");
            var snapshot = await httpClient.GetFromJsonAsync<TokenUsageSnapshot>($"api/aiteam/token-usage/{executionId}", JsonOptions, cancellationToken);
            return snapshot ?? Empty();
        }
        catch (HttpRequestException)
        {
            return Empty();
        }
        catch (TaskCanceledException)
        {
            return Empty();
        }
    }

    public static TokenUsageSnapshot Empty() => new(0, 0, 0m, new Dictionary<string, TokenUsageRecord>());
}
