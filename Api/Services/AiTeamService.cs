using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using AiAgileTeam.Models;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;

namespace AiAgileTeam.Services;

public class AiTeamService
{
    private readonly HttpClient _httpClient;

    public AiTeamService(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("AiTeamClient");
    }

    public IChatCompletionService CreateChatService(ProviderConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.ApiKey) || config.ApiKey.Contains("[") || config.ApiKey.Contains("YOUR") || config.ApiKey.Contains("GCP_API_KEY"))
        {
            throw new ArgumentException($"API Key is required and appears to be not configured for provider {config.Provider}");
        }

        if (config.Provider == "AzureOpenAI" && string.IsNullOrWhiteSpace(config.Endpoint))
        {
            throw new ArgumentException("Endpoint is required for Azure OpenAI");
        }

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

        if (agentConfig.MaxTokensPerResponse < 100) agentConfig.MaxTokensPerResponse = 1000;
        if (agentConfig.MaxRoundsPerSession < 1) agentConfig.MaxRoundsPerSession = 3;

        var agent = new ChatCompletionAgent
        {
            Kernel = kernel,
            Name = agentConfig.Name,
            Instructions = agentConfig.SystemPrompt,
            Arguments = new KernelArguments(
                new PromptExecutionSettings { ExtensionData = new Dictionary<string, object>
                    { ["max_tokens"] = agentConfig.MaxTokensPerResponse } })
        };

        return agent;
    }
}
