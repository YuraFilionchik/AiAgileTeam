using System;
using System.Collections.Generic;

namespace AiAgileTeam.Models;

public class ChatSession
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = "New Session";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<ChatMessage> Messages { get; set; } = new();
    public AppSettings SettingsSnapshot { get; set; } = new();
}
