using System.ComponentModel.DataAnnotations;

namespace AiAgileTeam.Models;

public class SessionRequest
{
    public string Query { get; set; } = "";
    
    public bool Clarify { get; set; } = false;

    [Required]
    public AppSettings Settings { get; set; } = new();

    public List<ChatMessageDto> History { get; set; } = new();

    public MediaContent? AttachedMedia { get; set; }
    
    public string? ServerSessionId { get; set; }
}

public class ChatMessageDto
{
    public string Author { get; set; } = "";
    public string Content { get; set; } = "";
    public bool IsUser { get; set; }
    public string? MediaUrl { get; set; }
    public string? MediaMimeType { get; set; }
}

public class StreamingMessageDto
{
    public string Author { get; set; } = "";
    public string ContentPiece { get; set; } = "";
    public bool IsComplete { get; set; }
    public string? ServerSessionId { get; set; }
    public string? MediaUrl { get; set; }
    public string? MediaMimeType { get; set; }
}

public class ReportRequest
{
    public string Title { get; set; } = "";
    public List<ChatMessageDto> Messages { get; set; } = new();
}

public class MediaUploadRequest
{
    [Required]
    public string ExecutionId { get; set; } = "";

    [Required]
    public byte[] Bytes { get; set; } = [];

    [Required]
    public string MimeType { get; set; } = "";

    public string? FileName { get; set; }
}

public class MediaUploadResponse
{
    public string Url { get; set; } = "";
    public string MimeType { get; set; } = "";
    public string? FileName { get; set; }
}
