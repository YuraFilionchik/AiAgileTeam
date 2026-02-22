using System.ComponentModel.DataAnnotations;

namespace AiAgileTeam.Models;

public class SessionRequest
{
    [Required]
    public string Query { get; set; } = "";
    
    public bool Clarify { get; set; } = false;

    [Required]
    public AppSettings Settings { get; set; } = new();

    public List<ChatMessageDto> History { get; set; } = new();
    
    public string? ServerSessionId { get; set; }
}

public class ChatMessageDto
{
    public string Author { get; set; } = "";
    public string Content { get; set; } = "";
    public bool IsUser { get; set; }
}

public class StreamingMessageDto
{
    public string Author { get; set; } = "";
    public string ContentPiece { get; set; } = "";
    public bool IsComplete { get; set; }
    public string? ServerSessionId { get; set; }
}
