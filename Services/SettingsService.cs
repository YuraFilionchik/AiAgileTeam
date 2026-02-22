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
        return settings;
    }

    public async Task SaveSettingsAsync(AppSettings settings)
    {
        await _localStorage.SetItemAsync(SettingsKey, settings);
    }

    private List<AgentConfig> GetDefaultAgents()
    {
        return new List<AgentConfig>
        {
            new AgentConfig { Name = "Scrum Master", Role = "Scrum Master", SystemPrompt = "You are a Scrum Master. Ensure the team follows agile practices." },
            new AgentConfig { Name = "Product Owner", Role = "Product Owner", SystemPrompt = "You are a Product Owner. Maximize the value of the product." },
            new AgentConfig { Name = "Architect", Role = "Architect", SystemPrompt = "You are a Software Architect. Design robust and scalable solutions." },
            new AgentConfig { Name = "Developer", Role = "Developer", SystemPrompt = "You are a Senior Developer. Write clean, efficient code." },
            new AgentConfig { Name = "QA Engineer", Role = "QA", SystemPrompt = "You are a QA Engineer. Ensure quality through rigorous testing." }
        };
    }
}
