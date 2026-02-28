using AiAgileTeam.Models;

namespace AiAgileTeam.Services;

public interface ITokenUsageTracker
{
    event EventHandler<TokenUsageUpdatedEventArgs>? UsageUpdated;

    void Record(TokenUsageRecord record);

    TokenUsageSnapshot GetSnapshot(string executionId);

    void Clear(string executionId);
}
