using System.Net.Http;

namespace AiAgileTeam.Services;

public sealed class ApiHealthService
{
    private readonly IHttpClientFactory _httpClientFactory;

    public ApiHealthService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public bool IsApiAvailable { get; private set; }

    public DateTimeOffset? LastCheckedAt { get; private set; }

    public event Action? StatusChanged;

    /// <summary>
    /// Checks whether the API server is reachable.
    /// </summary>
    public async Task CheckAsync(CancellationToken cancellationToken = default)
    {
        var isAvailable = false;

        try
        {
            using var httpClient = _httpClientFactory.CreateClient("ApiClient");
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/aiteam/session");
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            isAvailable = true;
        }
        catch (HttpRequestException)
        {
            isAvailable = false;
        }
        catch (TaskCanceledException)
        {
            isAvailable = false;
        }

        var hasStateChanged = IsApiAvailable != isAvailable;
        IsApiAvailable = isAvailable;
        LastCheckedAt = DateTimeOffset.Now;

        if (hasStateChanged)
        {
            StatusChanged?.Invoke();
        }
    }
}
