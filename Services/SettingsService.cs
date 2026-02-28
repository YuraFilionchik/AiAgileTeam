using System.Text.Json;
using AiAgileTeam.Models;
using Blazored.LocalStorage;
using Microsoft.Extensions.Configuration;

namespace AiAgileTeam.Services;

public class SettingsService
{
    private readonly ILocalStorageService _localStorage;
    private readonly IConfiguration _configuration;
    private const string SettingsKey = "ai_team_settings";
    
    // Lists of human names used for generation
    private static readonly string[] MaleNames = { "Alexander", "Dmitry", "Maxim", "Sergey", "Andrew", "Alexey", "Artem", "Ilya", "Kirill", "Mikhail", "Nikita", "Egor", "Ivan", "Vladimir", "Pavel" };
    private static readonly string[] FemaleNames = { "Maria", "Anna", "Elena", "Olga", "Natalia", "Irina", "Tatyana", "Ekaterina", "Svetlana", "Anastasia", "Yulia", "Aleksandra", "Victoria", "Daria", "Polina" };
    
        private static readonly Dictionary<string, string[]> RoleSpecificNames = new()
        {
            { "Project Manager", new[] { "Alexander", "Dmitry", "Sergey", "Mikhail", "Elena", "Olga" } },
            { "Product Owner", new[] { "Maria", "Anna", "Natalia", "Andrew", "Alexey", "Irina" } },
            { "Architect", new[] { "Dmitry", "Maxim", "Artem", "Ilya", "Ekaterina", "Svetlana" } },
            { "Developer", new[] { "Kirill", "Nikita", "Egor", "Ivan", "Anastasia", "Yulia" } },
            { "QA", new[] { "Tatyana", "Victoria", "Daria", "Pavel", "Vladimir", "Polina" } },
            { "Scrum Master", new[] { "Aleksandra", "Irina", "Mikhail", "Sergey", "Elena", "Andrew" } }
        };
    
    private static readonly HashSet<string> _usedNames = new();
    private static readonly Random _random = new();

    private static readonly HashSet<string> LegacyRoleNames =
    [
        "Project Manager",
        "Product Owner",
        "Architect",
        "Developer",
        "QA Engineer",
        "Scrum Master",
        "QA"
    ];

    public SettingsService(ILocalStorageService localStorage, IConfiguration configuration)
    {
        _localStorage = localStorage;
        _configuration = configuration;
    }

    public async Task<AppSettings> LoadSettingsAsync()
    {
        var settings = await _localStorage.GetItemAsync<AppSettings>(SettingsKey);
        if (settings == null)
        {
            settings = new AppSettings();
            _configuration.GetSection("AppSettings").Bind(settings);

            if (settings.Agents == null || !settings.Agents.Any())
            {
                settings.Agents = GetDefaultAgents();
            }
        }
        else
        {
            // Migrate from old format if needed
            MigrateSettings(settings);
            
            // Ensure Project Manager is always selected and mandatory
            var pm = settings.Agents?.FirstOrDefault(a => a.Role == "Project Manager" || a.Name == "Project Manager");
            if (settings.Agents != null && pm != null)
            {
                pm.IsMandatory = true;
                pm.IsSelected = true;
            }
            
            // Ensure all agents have display names
            foreach (var agent in settings.Agents ?? new List<AgentConfig>())
            {
                if (string.IsNullOrEmpty(agent.DisplayName))
                {
                    agent.DisplayName = GetRandomNameForRole(agent.Role);
                }
            }
            
            await SaveSettingsAsync(settings);
        }
        return settings;
    }

    private void MigrateSettings(AppSettings settings)
    {
        // Migrate Mode -> ApiKeyMode
        if (!string.IsNullOrEmpty(settings.Mode) && string.IsNullOrEmpty(settings.ApiKeyMode))
        {
            settings.ApiKeyMode = settings.Mode;
            settings.Mode = null;
        }
        
        // Migrate Global -> GlobalApi
        if (settings.Global != null && settings.GlobalApi == null)
        {
            settings.GlobalApi = new ApiConfig
            {
                Provider = settings.Global.Provider,
                ApiKey = settings.Global.ApiKey,
                Endpoint = settings.Global.Endpoint
            };
            settings.Global = null;
        }
        
        // Migrate AgentConfig
        foreach (var agent in settings.Agents)
        {
            // Migrate Name -> DisplayName (if DisplayName is empty)
            if (!string.IsNullOrEmpty(agent.Name) && string.IsNullOrEmpty(agent.DisplayName))
            {
                // If Name looks like a role (contains Manager, Owner, etc.), generate a human name
                if (LegacyRoleNames.Contains(agent.Name))
                {
                    // Role was stored in Name, move to Role if empty
                    if (string.IsNullOrEmpty(agent.Role))
                    {
                        agent.Role = agent.Name;
                    }
                    agent.DisplayName = GetRandomNameForRole(agent.Role);
                }
                else
                {
                    // Name looks like a human name
                    agent.DisplayName = agent.Name;
                }
                agent.Name = null;
            }
            
            // Ensure Role is set
            if (string.IsNullOrEmpty(agent.Role) && !string.IsNullOrEmpty(agent.Name))
            {
                agent.Role = agent.Name;
            }

            agent.Role = NormalizeRole(agent.Role);
            
            // Migrate ProviderSettings -> ApiSettings + ModelSettings
            if (agent.ProviderSettings != null && agent.ApiSettings == null)
            {
                agent.ApiSettings = new ApiConfig
                {
                    Provider = agent.ProviderSettings.Provider,
                    ApiKey = agent.ProviderSettings.ApiKey,
                    Endpoint = agent.ProviderSettings.Endpoint
                };
                
                if (agent.ModelSettings == null || string.IsNullOrEmpty(agent.ModelSettings.Model))
                {
                    agent.ModelSettings = new ModelConfig
                    {
                        Model = agent.ProviderSettings.Model,
                        MaxTokensPerResponse = agent.MaxTokensPerResponse > 0 ? agent.MaxTokensPerResponse : 1000,
                        MaxRoundsPerSession = agent.MaxRoundsPerSession > 0 ? agent.MaxRoundsPerSession : 3
                    };
                }
                
                agent.ProviderSettings = null;
            }
            
            // Ensure ModelSettings exists
            if (agent.ModelSettings == null)
            {
                agent.ModelSettings = new ModelConfig
                {
                    Model = "",
                    MaxTokensPerResponse = agent.MaxTokensPerResponse > 0 ? agent.MaxTokensPerResponse : 1000,
                    MaxRoundsPerSession = agent.MaxRoundsPerSession > 0 ? agent.MaxRoundsPerSession : 3
                };
            }

            var isBuiltInRole = BuiltInAgentPrompts.IsBuiltInRole(agent.Role);
            if (isBuiltInRole)
            {
                agent.IsBuiltIn = true;

                if (string.IsNullOrWhiteSpace(agent.SystemPrompt) && BuiltInAgentPrompts.TryGetPrompt(agent.Role, out var defaultPrompt))
                {
                    agent.SystemPrompt = defaultPrompt;
                }
            }
        }
    }

    public async Task SaveSettingsAsync(AppSettings settings)
    {
        await _localStorage.SetItemAsync(SettingsKey, settings);
    }
    
    /// <summary>
    /// Get a random display name for the specified role
    /// </summary>
    public static string GetRandomNameForRole(string role)
    {
        // Normalize role name
        var normalizedRole = NormalizeRole(role);
        
        // Try to get role-specific names
        if (RoleSpecificNames.TryGetValue(normalizedRole, out var roleNames))
        {
            // Try to find an unused name from role-specific list
            var availableNames = roleNames.Where(n => !_usedNames.Contains(n)).ToList();
            if (availableNames.Any())
            {
                var name = availableNames[_random.Next(availableNames.Count)];
                _usedNames.Add(name);
                return name;
            }
        }
        
        // Fallback: get any unused name from all names
        var allNames = MaleNames.Concat(FemaleNames).Where(n => !_usedNames.Contains(n)).ToList();
        if (allNames.Any())
        {
            var name = allNames[_random.Next(allNames.Count)];
            _usedNames.Add(name);
            return name;
        }
        
        // All names used, generate with suffix
        var baseName = MaleNames[_random.Next(MaleNames.Length)];
        return $"{baseName} {_random.Next(100)}";
    }

    private List<AgentConfig> GetDefaultAgents()
    {
        return new List<AgentConfig>
        {
            CreateBuiltInAgent("Project Manager", isMandatory: true),
            CreateBuiltInAgent("Product Owner"),
            CreateBuiltInAgent("Architect"),
            CreateBuiltInAgent("Developer"),
            CreateBuiltInAgent("QA"),
            CreateBuiltInAgent("Scrum Master")
        };
    }

    private static string NormalizeRole(string? role)
    {
        var normalizedRole = role?.Trim() ?? "";
        if (normalizedRole.Equals("QA Engineer", StringComparison.OrdinalIgnoreCase))
        {
            return "QA";
        }

        return normalizedRole;
    }

    private static AgentConfig CreateBuiltInAgent(string role, bool isMandatory = false)
    {
        if (!BuiltInAgentPrompts.TryGetPrompt(role, out var prompt))
        {
            throw new InvalidOperationException($"Default prompt for role '{role}' was not found.");
        }

        return new AgentConfig
        {
            DisplayName = GetRandomNameForRole(role),
            Role = role,
            SystemPrompt = prompt,
            IsMandatory = isMandatory,
            IsSelected = true,
            IsBuiltIn = true,
            UseDefaultPrompt = true
        };
    }
}
