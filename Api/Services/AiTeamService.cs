using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using AiAgileTeam.Models;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;

namespace AiAgileTeam.Services;

public class AiTeamService
{
    private readonly HttpClient _httpClient;

    public AiTeamService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public IChatCompletionService CreateChatService(ProviderConfig config)
    {
        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton(_httpClient);

        switch (config.Provider)
        {
            case "OpenAI":
                builder.AddOpenAIChatCompletion(config.Model, config.ApiKey, httpClient: _httpClient);
                break;
            case "AzureOpenAI":
                builder.AddAzureOpenAIChatCompletion(config.Model, config.Endpoint, config.ApiKey, httpClient: _httpClient);
                break;
            case "GoogleGemini":
                #pragma warning disable SKEXP0070
                builder.AddGoogleAIGeminiChatCompletion(config.Model, config.ApiKey, httpClient: _httpClient);
                #pragma warning restore SKEXP0070
                break;
            default:
                throw new NotSupportedException($"Provider {config.Provider} is not supported.");
        }
        var kernel = builder.Build();
        return kernel.GetRequiredService<IChatCompletionService>();
    }

    public ChatCompletionAgent CreateAgent(AgentConfig agentConfig, AppSettings appSettings)
    {
        ProviderConfig providerConfig = appSettings.Mode == "global" ? appSettings.Global : agentConfig.ProviderSettings;
        var chatService = CreateChatService(providerConfig);
        var builderInternal = Kernel.CreateBuilder();
        builderInternal.Services.AddSingleton(chatService);
        builderInternal.Services.AddSingleton(_httpClient);
        var kernel = builderInternal.Build();

        var agent = new ChatCompletionAgent
        {
            Kernel = kernel,
            Name = agentConfig.Name,
            Instructions = agentConfig.SystemPrompt
        };

        return agent;
    }
}
