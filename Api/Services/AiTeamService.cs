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

    public IChatCompletionService CreateChatService(ApiConfig apiConfig, string model)
    {
        if (string.IsNullOrWhiteSpace(apiConfig.ApiKey) || apiConfig.ApiKey.Contains("[") || apiConfig.ApiKey.Contains("YOUR") || apiConfig.ApiKey.Contains("GCP_API_KEY"))
        {
            throw new ArgumentException($"API Key is required and appears to be not configured for provider {apiConfig.Provider}");
        }

        if (apiConfig.Provider == "AzureOpenAI" && string.IsNullOrWhiteSpace(apiConfig.Endpoint))
        {
            throw new ArgumentException("Endpoint is required for Azure OpenAI");
        }

        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton(_httpClient);

        switch (apiConfig.Provider)
        {
            case "OpenAI":
                builder.AddOpenAIChatCompletion(model, apiConfig.ApiKey, httpClient: _httpClient);
                break;
            case "AzureOpenAI":
                builder.AddAzureOpenAIChatCompletion(model, apiConfig.Endpoint, apiConfig.ApiKey, httpClient: _httpClient);
                break;
            case "GoogleGemini":
                #pragma warning disable SKEXP0070
                builder.AddGoogleAIGeminiChatCompletion(model, apiConfig.ApiKey, httpClient: _httpClient);
                #pragma warning restore SKEXP0070
                break;
            default:
                throw new NotSupportedException($"Provider {apiConfig.Provider} is not supported.");
        }
        var kernel = builder.Build();
        return kernel.GetRequiredService<IChatCompletionService>();
    }

    public ChatCompletionAgent CreateAgent(AgentConfig agentConfig, AppSettings appSettings)
    {
        // Get API configuration based on ApiKeyMode
        ApiConfig apiConfig = appSettings.ApiKeyMode == "global" 
            ? appSettings.GlobalApi 
            : agentConfig.ApiSettings ?? appSettings.GlobalApi;
        
        // Get model from agent's ModelSettings
        string model = agentConfig.ModelSettings.Model;
        
        var chatService = CreateChatService(apiConfig, model);
        var builderInternal = Kernel.CreateBuilder();
        builderInternal.Services.AddSingleton(chatService);
        builderInternal.Services.AddSingleton(_httpClient);
        var kernel = builderInternal.Build();

        int maxTokens = agentConfig.ModelSettings.MaxTokensPerResponse;
        if (maxTokens < 100) maxTokens = 1000;

        // Build full prompt with agent name and role
        // Format: "Your name is {DisplayName}. You are a {Role}. {SystemPrompt}"
        string displayName = !string.IsNullOrEmpty(agentConfig.DisplayName) ? agentConfig.DisplayName : "Assistant";
        string role = !string.IsNullOrEmpty(agentConfig.Role) ? agentConfig.Role : "Assistant";
        string fullPrompt = $"Your name is {displayName}. You are a {role}. {agentConfig.SystemPrompt}";

        // Use DisplayName as agent name for identification in chat
        string agentName = !string.IsNullOrEmpty(agentConfig.DisplayName) ? agentConfig.DisplayName : role;

        var agent = new ChatCompletionAgent
        {
            Kernel = kernel,
            Name = agentName,
            Instructions = fullPrompt,
            Arguments = new KernelArguments(
                new PromptExecutionSettings { ExtensionData = new Dictionary<string, object>
                    { ["max_tokens"] = maxTokens } })
        };

        return agent;
    }
}
