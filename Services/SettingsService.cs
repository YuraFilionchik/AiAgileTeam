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
    
    // Списки человеческих имён для генерации
    private static readonly string[] MaleNames = { "Александр", "Дмитрий", "Максим", "Сергей", "Андрей", "Алексей", "Артём", "Илья", "Кирилл", "Михаил", "Никита", "Егор", "Иван", "Владимир", "Павел" };
    private static readonly string[] FemaleNames = { "Мария", "Анна", "Елена", "Ольга", "Наталья", "Ирина", "Татьяна", "Екатерина", "Светлана", "Анастасия", "Юлия", "Александра", "Виктория", "Дарья", "Полина" };
    
    private static readonly Dictionary<string, string[]> RoleSpecificNames = new()
    {
        { "Project Manager", new[] { "Александр", "Дмитрий", "Сергей", "Михаил", "Елена", "Ольга" } },
        { "Product Owner", new[] { "Мария", "Анна", "Наталья", "Андрей", "Алексей", "Ирина" } },
        { "Architect", new[] { "Дмитрий", "Максим", "Артём", "Илья", "Екатерина", "Светлана" } },
        { "Developer", new[] { "Кирилл", "Никита", "Егор", "Иван", "Анастасия", "Юлия" } },
        { "QA", new[] { "Татьяна", "Виктория", "Дарья", "Павел", "Владимир", "Полина" } },
        { "Scrum Master", new[] { "Александра", "Ирина", "Михаил", "Сергей", "Елена", "Андрей" } }
    };
    
    private static readonly HashSet<string> _usedNames = new();
    private static readonly Random _random = new();

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
                if (agent.Name == "Project Manager" || agent.Name == "Product Owner" || 
                    agent.Name == "Architect" || agent.Name == "Developer" || 
                    agent.Name == "QA Engineer" || agent.Name == "Scrum Master" || agent.Name == "QA")
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
        }
    }

    public async Task SaveSettingsAsync(AppSettings settings)
    {
        await _localStorage.SetItemAsync(SettingsKey, settings);
    }
    
    /// <summary>
    /// Получить случайное имя для указанной роли
    /// </summary>
    public static string GetRandomNameForRole(string role)
    {
        // Normalize role name
        var normalizedRole = role?.Trim() ?? "";
        if (normalizedRole.Equals("QA Engineer", StringComparison.OrdinalIgnoreCase))
        {
            normalizedRole = "QA";
        }
        
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
            new AgentConfig {
                DisplayName = GetRandomNameForRole("Project Manager"),
                Role = "Project Manager",
                SystemPrompt = "You are a Senior Project Manager and Orchestrator. Your mission is to deliver a professional Software Requirements Specification (SRS) and a Roadmap.\n\n" +
                               "Responsibilities:\n" +
                               "1. Initiate the discussion by setting the agenda and defining the project scope based on the user's query.\n" +
                               "2. Call specific experts (Architect, Product Owner, etc.) to address relevant parts of the plan.\n" +
                               "3. Moderate the discussion, ensure the team stays on track and resolves conflicts.\n" +
                               "4. Final Synthesis: Once all experts have contributed, combine their input into a final, structured Markdown document (SRS).\n" +
                               "The document MUST include: \n" +
                               "   - Project Overview\n" +
                               "   - Business Requirements (from PO)\n" +
                               "   - Technical Architecture & Stack (from Architect)\n" +
                               "   - Implementation Details (from Developer)\n" +
                               "   - Quality Assurance Plan (from QA)\n" +
                               "   - Project Roadmap & Risks (from Scrum Master)\n" +
                               "5. Conclude with exactly '[DONE]' after the final document is presented.\n\n" +
                               "Interaction Style: Professional, leadership-oriented, focused on deliverables.",
                IsMandatory = true,
                IsSelected = true
            },
            new AgentConfig {
                DisplayName = GetRandomNameForRole("Product Owner"),
                Role = "Product Owner",
                SystemPrompt = "You are a Product Owner. Your focus is on maximizing product value and defining 'the what'.\n\n" +
                               "Responsibilities:\n" +
                               "1. Define high-level business requirements and user personas.\n" +
                               "2. Create a prioritized backlog of features/user stories.\n" +
                               "3. Define acceptance criteria for the main features.\n" +
                               "Deliverables: Business value proposition, User Stories, Feature List.",
                IsSelected = true
            },
            new AgentConfig {
                DisplayName = GetRandomNameForRole("Architect"),
                Role = "Architect",
                SystemPrompt = "You are a Software Architect. Your focus is on 'the how' at a high level.\n\n" +
                               "Responsibilities:\n" +
                               "1. Propose the technology stack (backend, frontend, database, etc.).\n" +
                               "2. Describe the system architecture (microservices, monolith, layers).\n" +
                               "3. Identify key technical risks and scalability strategies.\n" +
                               "Deliverables: Tech Stack, Component Diagrams (described in text/Mermaid), Infrastructure overview.",
                IsSelected = true
            },
            new AgentConfig {
                DisplayName = GetRandomNameForRole("Developer"),
                Role = "Developer",
                SystemPrompt = "You are a Senior Software Engineer. Your focus is on technical implementation and feasibility.\n\n" +
                               "Responsibilities:\n" +
                               "1. Provide implementation details for complex features.\n" +
                               "2. Suggest database schema (key tables/entities).\n" +
                               "3. Advise on security best practices and API design.\n" +
                               "Deliverables: Data Schema, Implementation Strategy, Critical Algorithms description.",
                IsSelected = true
            },
            new AgentConfig {
                DisplayName = GetRandomNameForRole("QA"),
                Role = "QA",
                SystemPrompt = "You are a Lead QA Engineer. Your focus is on quality and reliability.\n\n" +
                               "Responsibilities:\n" +
                               "1. Define the testing strategy (Unit, Integration, E2E).\n" +
                               "2. Identify potential edge cases and security vulnerabilities.\n" +
                               "3. Suggest quality metrics and CI/CD quality gates.\n" +
                               "Deliverables: Test Plan, List of Edge Cases, Quality Assurance Strategy.",
                IsSelected = true
            },
            new AgentConfig {
                DisplayName = GetRandomNameForRole("Scrum Master"),
                Role = "Scrum Master",
                SystemPrompt = "You are a Scrum Master. Your focus is on the Agile process and project execution.\n\n" +
                               "Responsibilities:\n" +
                               "1. Define the sprint structure and ceremony cadence.\n" +
                               "2. Estimate project timelines and identify delivery risks.\n" +
                               "3. Suggest team composition and communication protocols.\n" +
                               "Deliverables: Sprint Roadmap, Risk Matrix, Team Workflow definition.",
                IsSelected = true
            }
        };
    }
}
