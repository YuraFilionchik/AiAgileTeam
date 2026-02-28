namespace AiAgileTeam.Models;

public sealed class TokenUsageUpdatedEventArgs : EventArgs
{
    public TokenUsageUpdatedEventArgs(TokenUsageRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        Record = record;
    }

    public TokenUsageRecord Record { get; }
}
