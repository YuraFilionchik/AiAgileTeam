using AiAgileTeam.Models;

namespace AiAgileTeam.Services.Orchestration;

/// <summary>
/// Resolves orchestration strategy by selected orchestration mode.
/// </summary>
public sealed class OrchestrationStrategyFactory
{
    private readonly GroupChatOrchestrationStrategy _groupChatStrategy;
    private readonly MagenticOrchestrationStrategy _magenticStrategy;

    public OrchestrationStrategyFactory(
        GroupChatOrchestrationStrategy groupChatStrategy,
        MagenticOrchestrationStrategy magenticStrategy)
    {
        ArgumentNullException.ThrowIfNull(groupChatStrategy);
        ArgumentNullException.ThrowIfNull(magenticStrategy);

        _groupChatStrategy = groupChatStrategy;
        _magenticStrategy = magenticStrategy;
    }

    /// <summary>
    /// Resolves strategy implementation for requested orchestration mode.
    /// </summary>
    public IOrchestrationStrategy Resolve(OrchestrationMode mode)
    {
        return mode switch
        {
            OrchestrationMode.Magentic => _magenticStrategy,
            _ => _groupChatStrategy
        };
    }
}
