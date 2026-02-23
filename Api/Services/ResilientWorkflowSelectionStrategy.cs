using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AiAgileTeam.Services;

/// <summary>
/// Стратегия выбора агента с поддержкой прерываний от пользователя и гибкой маршрутизацией.
/// </summary>
public class ResilientWorkflowSelectionStrategy : SelectionStrategy
{
    private readonly string[] _workflowOrder = new[]
    {
        "Project Manager",
        "Product Owner",
        "Architect",
        "Developer",
        "QA Engineer",
        "Scrum Master",
        "Project Manager"
    };

    private int _currentStep = 0;

    /// <summary>
    /// Сбрасывает стратегию в начальное состояние. Должен вызываться перед началом новой сессии.
    /// </summary>
    public void Reset()
    {
        _currentStep = 0;
        Console.WriteLine("[ResilientWorkflow] Reset() called, _currentStep set to 0");
    }

    protected override Task<Agent> SelectAgentAsync(
        IReadOnlyList<Agent> agents,
        IReadOnlyList<ChatMessageContent> history,
        CancellationToken cancellationToken = default)
    {
        var lastMessage = history.LastOrDefault();

        Console.WriteLine($"[ResilientWorkflow] SelectAgentAsync called. History count: {history.Count}, _currentStep: {_currentStep}");
        Console.WriteLine($"[ResilientWorkflow] Available agents: {string.Join(", ", agents.Select(a => $"{a.Name} ({GetAgentRole(a)})"))}");
        if (lastMessage != null)
        {
            Console.WriteLine($"[ResilientWorkflow] Last message: Role={lastMessage.Role}, Author={lastMessage.AuthorName ?? "N/A"}");
        }
        else
        {
            Console.WriteLine($"[ResilientWorkflow] No history messages yet");
        }

        // 1. РЕАКЦИЯ НА ПРЕРЫВАНИЕ ПОЛЬЗОВАТЕЛЕМ
        // Если последнее сообщение в истории от человека, обрабатываем как изменение требований
        if (lastMessage != null && lastMessage.Role == AuthorRole.User)
        {
            // Проверяем наличие прямого упоминания (@AgentName)
            var mentionedAgent = TryFindMentionedAgent(agents, lastMessage.Content);

            if (mentionedAgent != null)
            {
                Console.WriteLine($"[ResilientWorkflow] User mentioned @{mentionedAgent.Name}, selecting directly");
                // Передаем слово упомянутому специалисту, не сбрасывая _currentStep
                return Task.FromResult(mentionedAgent);
            }

            // Сбрасываем счетчик. Команда должна переоценить план с учетом новых данных.
            _currentStep = 0;
            Console.WriteLine($"[ResilientWorkflow] User interruption detected, resetting to step 0 (PM)");

            // Берем PM'а, увеличиваем шаг и передаем ему слово
            string firstAgentRole = _workflowOrder[_currentStep];
            _currentStep++;
            return Task.FromResult(GetAgentByRole(agents, firstAgentRole));
        }

        // 2. ЗАЩИТА ОТ БЕСКОНЕЧНОГО ЦИКЛА (если стратегия завершения не сработала)
        if (_currentStep >= _workflowOrder.Length)
        {
            Console.WriteLine($"[ResilientWorkflow] Max steps reached, fallback to PM");
            // Зацикливаем на PM'е, чтобы он выдал [DONE]
            return Task.FromResult(GetAgentByRole(agents, "Project Manager"));
        }

        // 3. СТАНДАРТНЫЙ ПОТОК (никто не перебивал)
        string nextAgentRole = _workflowOrder[_currentStep];
        Console.WriteLine($"[ResilientWorkflow] Selecting agent: {nextAgentRole} (step {_currentStep})");
        _currentStep++;

        return Task.FromResult(GetAgentByRole(agents, nextAgentRole));
    }

    /// <summary>
    /// Ищет прямое упоминание агента в тексте сообщения (@AgentName или @Role).
    /// </summary>
    private static Agent? TryFindMentionedAgent(IReadOnlyList<Agent> agents, string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        // Сначала ищем по имени агента (@Дмитрий, @Александр и т.д.)
        foreach (var agent in agents)
        {
            if (content.Contains($"@{agent.Name}", StringComparison.OrdinalIgnoreCase))
            {
                return agent;
            }
        }

        // Если не найдено, ищем по роли (@Project Manager, @Architect и т.д.)
        foreach (var agent in agents)
        {
            var role = GetAgentRole(agent);
            if (content.Contains($"@{role}", StringComparison.OrdinalIgnoreCase))
            {
                return agent;
            }
        }

        return null;
    }

    /// <summary>
    /// Извлекает роль агента из его инструкций.
    /// Формат инструкций: "Your name is {DisplayName}. You are a {Role}. {SystemPrompt}"
    /// </summary>
    private static string GetAgentRole(Agent agent)
    {
        var instructions = agent.Instructions ?? "";
        var match = System.Text.RegularExpressions.Regex.Match(instructions, @"You are a ([^.]+)\.");
        if (match.Success)
        {
            return match.Groups[1].Value.Trim();
        }
        return "Unknown";
    }

    /// <summary>
    /// Вспомогательный метод для поиска агента по его роли.
    /// </summary>
    private static Agent GetAgentByRole(IReadOnlyList<Agent> agents, string role)
    {
        var agent = agents.FirstOrDefault(a => GetAgentRole(a) == role);

        if (agent == null)
        {
            Console.WriteLine($"[ResilientWorkflow] GetAgentByRole: Agent with role '{role}' not found. Available: {string.Join(", ", agents.Select(a => $"{a.Name} ({GetAgentRole(a)})"))}");

            // Fallback на Project Manager
            agent = agents.FirstOrDefault(a => GetAgentRole(a) == "Project Manager");

            if (agent == null)
            {
                // Если и PM не найден, берём первого доступного
                agent = agents.FirstOrDefault();
                Console.WriteLine($"[ResilientWorkflow] GetAgentByRole: PM not found either, using fallback: {agent?.Name ?? "NONE"}");
            }
        }

        return agent ?? throw new InvalidOperationException("No agents available");
    }
}
