using AiAgileTeam.Models;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace AiAgileTeam.Services.Orchestration;

public sealed class AgentOrchestrator : IAgentOrchestrator
{
    private readonly SessionStore _sessionStore;
    private readonly IMediaStorageService _mediaStorageService;
    private readonly ITokenUsageContextAccessor _tokenUsageContextAccessor;

    public AgentOrchestrator(
        SessionStore sessionStore,
        IMediaStorageService mediaStorageService,
        ITokenUsageContextAccessor tokenUsageContextAccessor)
    {
        ArgumentNullException.ThrowIfNull(sessionStore);
        ArgumentNullException.ThrowIfNull(mediaStorageService);
        ArgumentNullException.ThrowIfNull(tokenUsageContextAccessor);

        _sessionStore = sessionStore;
        _mediaStorageService = mediaStorageService;
        _tokenUsageContextAccessor = tokenUsageContextAccessor;
    }

    public Task AddUserImageAsync(string executionId, MediaContent image)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
        ArgumentNullException.ThrowIfNull(image);

        var session = _sessionStore.GetOrCreate(executionId);
        session.GroupChat.AddChatMessage(new ChatMessageContent
        {
            Role = AuthorRole.User,
            Items =
            [
                new ImageContent(image.Bytes, image.MimeType)
            ]
        });

        return Task.CompletedTask;
    }

    public async Task SendAgentImageAsync(string agentName, MediaContent image)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentName);
        ArgumentNullException.ThrowIfNull(image);

        var executionId = _tokenUsageContextAccessor.Current?.ExecutionId;
        if (string.IsNullOrWhiteSpace(executionId))
        {
            throw new InvalidOperationException("Execution context is not available for agent media output.");
        }

        var imageUrl = await _mediaStorageService.UploadAsync(image, executionId);
        var session = _sessionStore.GetOrCreate(executionId);

        var imageContent = new ImageContent { Uri = new Uri(imageUrl, UriKind.RelativeOrAbsolute) };
        session.GroupChat.AddChatMessage(new ChatMessageContent
        {
            Role = AuthorRole.Assistant,
            AuthorName = agentName,
            Items =
            [
                imageContent
            ]
        });
    }
}
