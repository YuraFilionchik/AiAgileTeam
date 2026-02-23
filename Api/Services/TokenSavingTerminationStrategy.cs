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
/// Стратегия завершения, проверяющая наличие токена [DONE] только в последнем сообщении от Project Manager.
/// </summary>
public class TokenSavingTerminationStrategy : TerminationStrategy
{
    protected override Task<bool> ShouldAgentTerminateAsync(
        Agent agent, 
        IReadOnlyList<ChatMessageContent> history, 
        CancellationToken cancellationToken = default)
    {
        var lastMessage = history.LastOrDefault();
        
        // Завершаем только если сейчас отработал PM, и именно в его текущем сообщении есть [DONE]
        // Это предотвращает преждевременное завершение при наличии [DONE] в старой истории
        bool isDone = lastMessage != null && 
                      lastMessage.AuthorName == "Project Manager" && 
                      lastMessage.Content != null && 
                      lastMessage.Content.Contains("[DONE]", StringComparison.OrdinalIgnoreCase);

        return Task.FromResult(isDone);
    }
}
