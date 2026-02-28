using AiAgileTeam.Models;

namespace AiAgileTeam.Services.Orchestration;

/// <summary>
/// Defines a strategy for running team discussion orchestration.
/// </summary>
public interface IOrchestrationStrategy
{
    /// <summary>
    /// Runs a discussion phase and streams messages to the client.
    /// </summary>
    IAsyncEnumerable<StreamingMessageDto> RunDiscussionAsync(
        AppSettings settings,
        SessionData sessionData,
        string serverSessionId,
        string userQuery,
        CancellationToken cancellationToken);
}
