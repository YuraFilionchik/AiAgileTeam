using AiAgileTeam.Models;

namespace AiAgileTeam.Services;

public interface IMediaStorageService
{
    Task<string> UploadAsync(MediaContent content, string executionId);
}
