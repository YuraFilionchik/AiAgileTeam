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
            var pm = settings.Agents?.FirstOrDefault(a => a.Name == "Project Manager");
            if (pm != null && !pm.IsMandatory)
            {
                pm.IsMandatory = true;
                pm.IsSelected = true;
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
            new AgentConfig {
                Name = "Project Manager",
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
                IsMandatory = true
            },
            new AgentConfig {
                Name = "Product Owner",
                Role = "Product Owner",
                SystemPrompt = "You are a Product Owner. Your focus is on maximizing product value and defining 'the what'.\n\n" +
                               "Responsibilities:\n" +
                               "1. Define high-level business requirements and user personas.\n" +
                               "2. Create a prioritized backlog of features/user stories.\n" +
                               "3. Define acceptance criteria for the main features.\n" +
                               "Deliverables: Business value proposition, User Stories, Feature List."
            },
            new AgentConfig {
                Name = "Architect",
                Role = "Architect",
                SystemPrompt = "You are a Software Architect. Your focus is on 'the how' at a high level.\n\n" +
                               "Responsibilities:\n" +
                               "1. Propose the technology stack (backend, frontend, database, etc.).\n" +
                               "2. Describe the system architecture (microservices, monolith, layers).\n" +
                               "3. Identify key technical risks and scalability strategies.\n" +
                               "Deliverables: Tech Stack, Component Diagrams (described in text/Mermaid), Infrastructure overview."
            },
            new AgentConfig {
                Name = "Developer",
                Role = "Developer",
                SystemPrompt = "You are a Senior Software Engineer. Your focus is on technical implementation and feasibility.\n\n" +
                               "Responsibilities:\n" +
                               "1. Provide implementation details for complex features.\n" +
                               "2. Suggest database schema (key tables/entities).\n" +
                               "3. Advise on security best practices and API design.\n" +
                               "Deliverables: Data Schema, Implementation Strategy, Critical Algorithms description."
            },
            new AgentConfig {
                Name = "QA Engineer",
                Role = "QA",
                SystemPrompt = "You are a Lead QA Engineer. Your focus is on quality and reliability.\n\n" +
                               "Responsibilities:\n" +
                               "1. Define the testing strategy (Unit, Integration, E2E).\n" +
                               "2. Identify potential edge cases and security vulnerabilities.\n" +
                               "3. Suggest quality metrics and CI/CD quality gates.\n" +
                               "Deliverables: Test Plan, List of Edge Cases, Quality Assurance Strategy."
            },
            new AgentConfig {
                Name = "Scrum Master",
                Role = "Scrum Master",
                SystemPrompt = "You are a Scrum Master. Your focus is on the Agile process and project execution.\n\n" +
                               "Responsibilities:\n" +
                               "1. Define the sprint structure and ceremony cadence.\n" +
                               "2. Estimate project timelines and identify delivery risks.\n" +
                               "3. Suggest team composition and communication protocols.\n" +
                               "Deliverables: Sprint Roadmap, Risk Matrix, Team Workflow definition."
            }
        };
    }
}
