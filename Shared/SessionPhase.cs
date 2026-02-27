namespace AiAgileTeam.Models;

/// <summary>
/// Represents a discrete phase/step in the session workflow shown in the summary panel.
/// </summary>
public record SessionPhase
{
    public string Title { get; init; } = "";
    public string? Description { get; init; }
    public SessionPhaseStatus Status { get; set; } = SessionPhaseStatus.Pending;
}

public enum SessionPhaseStatus
{
    Pending,
    InProgress,
    Completed
}
