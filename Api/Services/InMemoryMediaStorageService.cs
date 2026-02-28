using AiAgileTeam.Models;

namespace AiAgileTeam.Services;

public sealed class InMemoryMediaStorageService : IMediaStorageService
{
    public Task<string> UploadAsync(MediaContent content, string executionId)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(content.MimeType);
        ArgumentException.ThrowIfNullOrWhiteSpace(executionId);

        if (content.Bytes.Length == 0)
        {
            throw new ArgumentException("Media content cannot be empty.", nameof(content));
        }

        var dataUrl = $"data:{content.MimeType};base64,{Convert.ToBase64String(content.Bytes)}";
        return Task.FromResult(dataUrl);
    }
}
