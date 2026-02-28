namespace AiAgileTeam.Services;

public interface ITokenUsageContextAccessor
{
    TokenUsageContext? Current { get; set; }
}

public sealed class AsyncLocalTokenUsageContextAccessor : ITokenUsageContextAccessor
{
    private static readonly AsyncLocal<TokenUsageContext?> Holder = new();

    public TokenUsageContext? Current
    {
        get => Holder.Value;
        set => Holder.Value = value;
    }
}

public sealed record TokenUsageContext(string ExecutionId, string Step);
