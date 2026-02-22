namespace AiAgileTeam.Models;

public class AppSettings
{
    public string Mode { get; set; } = "global"; // global | per-agent
    public ProviderConfig Global { get; set; } = new();
    public List<AgentConfig> Agents { get; set; } = new();
}

public class ProviderConfig
{
    public string Provider { get; set; } = "OpenAI"; // OpenAI | AzureOpenAI | GoogleGemini
    public string ApiKey { get; set; } = "";
    public string Endpoint { get; set; } = "";       // только для Azure
    public string Model { get; set; } = "";
}
