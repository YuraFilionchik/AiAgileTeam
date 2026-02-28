namespace AiAgileTeam.Services;

public sealed class MediaStorageOptions
{
    public string Provider { get; init; } = "InMemory";

    public string? ConnectionString { get; init; }

    public string ContainerName { get; init; } = "agent-media";
}
