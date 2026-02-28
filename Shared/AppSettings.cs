namespace AiAgileTeam.Models;

public class AppSettings
{
    public string ApiKeyMode { get; set; } = "global"; // global | per-agent
    public OrchestrationMode OrchestrationMode { get; set; } = OrchestrationMode.GroupChat;
    public ApiConfig GlobalApi { get; set; } = new();
    public List<AgentConfig> Agents { get; set; } = new();
    
    // Legacy properties for migration
    public string? Mode { get; set; } // deprecated, use ApiKeyMode
    public ProviderConfig? Global { get; set; } // deprecated, use GlobalApi
}

public class ApiConfig
{
    public string Provider { get; set; } = "OpenAI"; // OpenAI | AzureOpenAI | GoogleGemini
    public string ApiKey { get; set; } = "";
    public string Endpoint { get; set; } = ""; // только для Azure
}

public class ModelConfig
{
    public string Model { get; set; } = "";
    public int MaxTokensPerResponse { get; set; } = 1000;
    public int MaxRoundsPerSession { get; set; } = 3;
}

public class ProviderConfig
{
    public string Provider { get; set; } = "OpenAI"; // OpenAI | AzureOpenAI | GoogleGemini
    public string ApiKey { get; set; } = "";
    public string Endpoint { get; set; } = "";       // только для Azure
    public string Model { get; set; } = "";
}
