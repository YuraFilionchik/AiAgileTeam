using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using AiAgileTeam.Models;
using AiAgileTeam.Extensions;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;

namespace AiAgileTeam.Services;

public class AiTeamService
{
    private readonly HttpClient _httpClient;
    private readonly ITokenUsageTracker _tokenUsageTracker;
    private readonly IModelPricingService _modelPricingService;
    private readonly ITokenUsageContextAccessor _tokenUsageContextAccessor;
    private readonly ILogger<TrackingChatCompletionService> _trackingLogger;

    public AiTeamService(
        IHttpClientFactory httpClientFactory,
        ITokenUsageTracker tokenUsageTracker,
        IModelPricingService modelPricingService,
        ITokenUsageContextAccessor tokenUsageContextAccessor,
        ILogger<TrackingChatCompletionService> trackingLogger)
    {
        _httpClient = httpClientFactory.CreateClient("AiTeamClient");
        _tokenUsageTracker = tokenUsageTracker;
        _modelPricingService = modelPricingService;
        _tokenUsageContextAccessor = tokenUsageContextAccessor;
        _trackingLogger = trackingLogger;
    }

    public IChatCompletionService CreateChatService(
        ApiConfig apiConfig,
        string model,
        string agentName,
        string executionId,
        string step = "Response")
    {
        if (string.IsNullOrWhiteSpace(apiConfig.ApiKey) || apiConfig.ApiKey.Contains("[") || apiConfig.ApiKey.Contains("YOUR") || apiConfig.ApiKey.Contains("GCP_API_KEY"))
        {
            throw new ArgumentException($"API Key is required and appears to be not configured for provider {apiConfig.Provider}");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(agentName);
        ArgumentException.ThrowIfNullOrWhiteSpace(executionId);

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
                builder.AddVisionSupport(model, apiConfig.Endpoint, apiConfig.ApiKey, _httpClient);
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
        var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

        _tokenUsageContextAccessor.Current = new TokenUsageContext(executionId, step);

        return new TrackingChatCompletionService(
            chatCompletionService,
            _tokenUsageTracker,
            _modelPricingService,
            _tokenUsageContextAccessor,
            agentName,
            executionId,
            _trackingLogger);
    }

    public ChatCompletionAgent CreateAgent(AgentConfig agentConfig, AppSettings appSettings, string executionId)
    {
        // Get API configuration based on ApiKeyMode
        ApiConfig apiConfig = appSettings.ApiKeyMode == "global" 
            ? appSettings.GlobalApi 
            : agentConfig.ApiSettings ?? appSettings.GlobalApi;

        // Get model from agent's ModelSettings
        string model = agentConfig.ModelSettings.Model;

        if (string.IsNullOrWhiteSpace(model))
        {
            throw new ArgumentException(
                $"Model is not configured for agent '{agentConfig.DisplayName}' ({agentConfig.Role}). Please set a model in the agent's settings.");
        }
        
        _tokenUsageContextAccessor.Current = new TokenUsageContext(executionId, "Discussion");
        var chatService = CreateChatService(apiConfig, model, agentName: agentConfig.DisplayName, executionId: executionId, step: "Discussion");
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
        string effectivePrompt = agentConfig.SystemPrompt;
        if (agentConfig.IsBuiltIn && agentConfig.UseDefaultPrompt && BuiltInAgentPrompts.TryGetPrompt(role, out var builtInPrompt))
        {
            effectivePrompt = builtInPrompt;
        }

        string fullPrompt = $"Your name is {displayName}. You are a {role}. {effectivePrompt}";

        // Use DisplayName as agent name for identification in chat
        string agentName = !string.IsNullOrEmpty(agentConfig.DisplayName) ? agentConfig.DisplayName : role;

        var agent = new ChatCompletionAgent
        {
            Kernel = kernel,
            Name = agentName,
            Instructions = fullPrompt,
            Arguments = new KernelArguments(
                new PromptExecutionSettings
                {
                    ModelId = model,
                    ExtensionData = new Dictionary<string, object>
                        { ["max_tokens"] = maxTokens }
                })
        };

        return agent;
    }
}
