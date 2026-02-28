namespace AiAgileTeam.Models;

public record MediaContent(byte[] Bytes, string MimeType, string? FileName = null);

public record ImageAnalysisResult(string Description, int Width, int Height);

public record MediaAttachment(MediaContent Content, string Url, string MimeType);
