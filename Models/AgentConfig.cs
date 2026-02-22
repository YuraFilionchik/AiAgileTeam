namespace AiAgileTeam.Models;

public class AgentConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";
    public string Role { get; set; } = "";
    public string SystemPrompt { get; set; } = "";
    public ProviderConfig ProviderSettings { get; set; } = new();
    public bool IsSelected { get; set; } = true;
}
