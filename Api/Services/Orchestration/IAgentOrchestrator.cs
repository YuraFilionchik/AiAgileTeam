using AiAgileTeam.Models;

namespace AiAgileTeam.Services.Orchestration;

public interface IAgentOrchestrator
{
    Task AddUserImageAsync(string executionId, MediaContent image);

    Task SendAgentImageAsync(string agentName, MediaContent image);
}
