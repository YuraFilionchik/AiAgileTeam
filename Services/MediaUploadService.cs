using System.Net.Http.Json;
using AiAgileTeam.Models;

namespace AiAgileTeam.Services;

public sealed class MediaUploadService
{
    private readonly IHttpClientFactory _httpClientFactory;

    public MediaUploadService(IHttpClientFactory httpClientFactory)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        _httpClientFactory = httpClientFactory;
    }

    public async Task<MediaUploadResponse> UploadAsync(MediaContent content, string executionId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(content.MimeType);
        ArgumentException.ThrowIfNullOrWhiteSpace(executionId);

        using var httpClient = _httpClientFactory.CreateClient("ApiClient");
        var response = await httpClient.PostAsJsonAsync("api/aiteam/media/upload", new MediaUploadRequest
        {
            ExecutionId = executionId,
            Bytes = content.Bytes,
            MimeType = content.MimeType,
            FileName = content.FileName
        }, cancellationToken);

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<MediaUploadResponse>(cancellationToken);
        if (payload is null)
        {
            throw new InvalidOperationException("Media upload response payload is empty.");
        }

        return payload;
    }
}
