namespace AiAgileTeam.Models;

public class AgentConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// Человеческое имя агента (например, "Александр", "Мария")
    /// </summary>
    public string DisplayName { get; set; } = "";
    
    /// <summary>
    /// Профессиональная роль/должность (например, "Project Manager", "Architect")
    /// </summary>
    public string Role { get; set; } = "";
    
    /// <summary>
    /// Системный промпт агента (без имени - оно добавляется автоматически)
    /// </summary>
    public string SystemPrompt { get; set; } = "";
    
    // API settings - used only when AppSettings.ApiKeyMode == "per-agent"
    public ApiConfig? ApiSettings { get; set; }
    
    // Model settings - always per-agent
    public ModelConfig ModelSettings { get; set; } = new();
    
    public bool IsSelected { get; set; } = true;
    public bool IsMandatory { get; set; } = false;
    
    // Legacy properties for migration
    public string? Name { get; set; } // deprecated - now uses DisplayName, kept for migration
    public ProviderConfig? ProviderSettings { get; set; } // deprecated, use ApiSettings + ModelSettings
    public int MaxTokensPerResponse { get; set; } = 1000; // deprecated, use ModelSettings.MaxTokensPerResponse
    public int MaxRoundsPerSession { get; set; } = 3; // deprecated, use ModelSettings.MaxRoundsPerSession
}
