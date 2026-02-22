namespace AiAgileTeam.Models;

public class ChatMessage
{
    public string Author { get; set; } = "";
    public string Content { get; set; } = "";
    public bool IsUser { get; set; }
}
