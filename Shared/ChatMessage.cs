namespace AiAgileTeam.Models;

public class ChatMessage
{
    public string Author { get; set; } = "";
    public string Content { get; set; } = "";
    public bool IsUser { get; set; }
    public string? MediaUrl { get; set; }
    public string? MediaMimeType { get; set; }
    public bool HasImage => !string.IsNullOrWhiteSpace(MediaUrl) &&
                            !string.IsNullOrWhiteSpace(MediaMimeType) &&
                            MediaMimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
}
