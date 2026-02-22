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
        else
        {
            var scrumMaster = settings.Agents?.FirstOrDefault(a => a.Name == "Scrum Master");
            if (scrumMaster != null && !scrumMaster.IsMandatory)
            {
                scrumMaster.IsMandatory = true;
                scrumMaster.IsSelected = true;
                await SaveSettingsAsync(settings);
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
            new AgentConfig { Name = "Scrum Master", Role = "Scrum Master", SystemPrompt = "You are a Scrum Master and the orchestrator of the team. Guide the project from start to finish. Ask other experts for their input when needed. Compile their feedback into a final comprehensive plan. When the plan is ready and the discussion is fully complete, conclude with the exact text '[DONE]'.", IsMandatory = true },
            new AgentConfig { Name = "Product Owner", Role = "Product Owner", SystemPrompt = "You are a Product Owner. Maximize the value of the product by clarifying business requirements and user needs. Give direct, concise advice when asked by the Scrum Master." },
            new AgentConfig { Name = "Architect", Role = "Architect", SystemPrompt = "You are a Software Architect. Design robust and scalable solutions. When the Scrum Master asks for your input, present technical designs and evaluate architectural trade-offs." },
            new AgentConfig { Name = "Developer", Role = "Developer", SystemPrompt = "You are a Senior Developer. Write clean, efficient code. Provide technical implementation details and coding strategies when requested by the Scrum Master." },
            new AgentConfig { Name = "QA Engineer", Role = "QA", SystemPrompt = "You are a QA Engineer. Ensure quality through rigorous testing. Identify potential bugs, edge cases, and testing strategies when prompted by the Scrum Master." }
        };
    }
}
