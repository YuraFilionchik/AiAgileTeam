#======Project Structure:=====
 
[ROOT] AiAgileTeam
├──  AiAgileTeam.csproj
├──  AiAgileTeam.slnLaunch
├──  App.razor
├──  Program.cs
├──  _Imports.razor
├──  Api
│        ├──  AiAgileTeam.Api.csproj
│        ├──  AiAgileTeam.Api.http
│        ├──  Program.cs
│        ├──  Controllers
│        │        └──  AiTeamController.cs
│        ├──  Properties
│        └──  Services
│                ├──  AiTeamService.cs
│                ├──  ChatContextCompressor.cs
│                ├──  DeterministicStrategies.cs
│                ├──  MarkdownService.cs
│                ├──  PmOrchestratorSelectionStrategy.cs
│                ├──  ResilientWorkflowSelectionStrategy.cs
│                ├──  SafeTerminationStrategy.cs
│                ├──  SessionStore.cs
│                ├──  TokenSavingTerminationStrategy.cs
│                └──  Orchestration
│                        ├──  GroupChatOrchestrationStrategy.cs
│                        ├──  IOrchestrationStrategy.cs
│                        ├──  MagenticOrchestrationStrategy.cs
│                        └──  OrchestrationStrategyFactory.cs
├──  Components
│        ├──  AddAgentModal.razor
│        ├──  ApiSettingsEditor.razor
│        ├──  ChatLog.razor
│        ├──  EditAgentModal.razor
│        ├──  ModelSettingsEditor.razor
│        ├──  ProviderSettingsEditor.razor
│        ├──  SessionSummaryPanel.razor
│        └──  SessionSummaryPanel.razor.css
├──  Layout
│        ├──  MainLayout.razor
│        ├──  MainLayout.razor.css
│        ├──  NavMenu.razor
│        └──  NavMenu.razor.css
├──  Pages
│        ├──  History.razor
│        ├──  Index.razor
│        ├──  Session.razor
│        ├──  Session.razor.css
│        └──  Settings.razor
├──  plans
├──  Properties
├──  Services
│        ├──  ApiHealthService.cs
│        ├──  ChatSessionService.cs
│        └──  SettingsService.cs
└──  Shared
        ├──  AgentConfig.cs
        ├──  AiAgileTeam.Shared.csproj
        ├──  AppSettings.cs
        ├──  BuiltInAgentPrompts.cs
        ├──  ChatMessage.cs
        ├──  ChatSession.cs
        ├──  DTOs.cs
        ├──  OrchestrationMode.cs
        └──  SessionPhase.cs
==============================

# File Contents

## File: AiAgileTeam.csproj
```
<Project Sdk="Microsoft.NET.Sdk.BlazorWebAssembly">

  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <ServiceWorkerAssetsManifest>service-worker-assets.js</ServiceWorkerAssetsManifest>
    <NoWarn>$(NoWarn);SKEXP0110;SKEXP0070;SKEXP0001</NoWarn>
    <DefaultItemExcludes>$(DefaultItemExcludes);Api\**;Shared\**</DefaultItemExcludes>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Blazored.LocalStorage" Version="4.3.0" />
    <PackageReference Include="Markdig" Version="0.38.0" />
    <PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly" Version="9.0.13" />
    <PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly.DevServer" Version="9.0.13" PrivateAssets="all" />
    <PackageReference Include="Microsoft.Extensions.Http" Version="9.0.2" />
    <PackageReference Include="MudBlazor" Version="9.0.0" />
    <PackageReference Include="PSC.Blazor.Components.MarkdownEditor" Version="2.0.1" />
  </ItemGroup>
  <ItemGroup>
    <ServiceWorker Include="wwwroot\service-worker.js" PublishedContent="wwwroot\service-worker.published.js" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="Shared\AiAgileTeam.Shared.csproj" />
  </ItemGroup>

</Project>

```

## File: AiAgileTeam.slnLaunch
```
[
  {
    "Name": "Core+API",
    "Projects": [
      {
        "Path": "AiAgileTeam.csproj",
        "Action": "Start",
        "DebugTarget": "http"
      },
      {
        "Path": "Api\\AiAgileTeam.Api.csproj",
        "Action": "Start",
        "DebugTarget": "http"
      }
    ]
  }
]
```

## File: App.razor
```
<Router AppAssembly="@typeof(App).Assembly">
    <Found Context="routeData">
        <RouteView RouteData="@routeData" DefaultLayout="@typeof(MainLayout)" />
        <FocusOnNavigate RouteData="@routeData" Selector="h1" />
    </Found>
    <NotFound>
        <PageTitle>Not found</PageTitle>
        <LayoutView Layout="@typeof(MainLayout)">
            <p role="alert">Sorry, there's nothing at this address.</p>
        </LayoutView>
    </NotFound>
</Router>

```

## File: Program.cs
```csharp
using AiAgileTeam;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using Blazored.LocalStorage;
using AiAgileTeam.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
// Specifically for the backend API communication
builder.Services.AddHttpClient("ApiClient", (sp, client) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    
    // Get the base URL from configuration, or construct it dynamically based on the app's protocol
    var baseUrl = config["AppSettings:ApiBaseUrl"];
    
    if (string.IsNullOrEmpty(baseUrl))
    {
        // Build API URL dynamically using the same protocol as the app
        var appAddress = new Uri(builder.HostEnvironment.BaseAddress);
        var scheme = appAddress.Scheme; // Will be http or https
        var host = appAddress.Host;
        var port = scheme == "https" ? 7135 : 5270;
        baseUrl = $"{scheme}://{host}:{port}";
    }
    
    client.BaseAddress = new Uri(baseUrl);
});


builder.Services.AddMudServices();
builder.Services.AddBlazoredLocalStorage();
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<ChatSessionService>();
builder.Services.AddScoped<ApiHealthService>();
await builder.Build().RunAsync();

```

## File: _Imports.razor
```
@using System.Net.Http
@using System.Net.Http.Json
@using Microsoft.AspNetCore.Components.Forms
@using Microsoft.AspNetCore.Components.Routing
@using Microsoft.AspNetCore.Components.Web
@using Microsoft.AspNetCore.Components.Web.Virtualization
@using Microsoft.AspNetCore.Components.WebAssembly.Http
@using Microsoft.JSInterop
@using AiAgileTeam
@using AiAgileTeam.Layout
@using AiAgileTeam.Models
@using AiAgileTeam.Services
@using MudBlazor
@using MudBlazor.Services
@using Blazored.LocalStorage
@using AiAgileTeam.Components

```

## File: Api\AiAgileTeam.Api.csproj
```
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <NoWarn>$(NoWarn);SKEXP0110;SKEXP0070;SKEXP0001</NoWarn>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Markdig" Version="0.41.2" />
    <PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="9.0.2" />
    <PackageReference Include="Microsoft.Extensions.Http.Polly" Version="10.0.3" />
    <PackageReference Include="Microsoft.SemanticKernel" Version="1.72.0" />
    <PackageReference Include="Microsoft.SemanticKernel.Agents.Core" Version="1.72.0" />
    <PackageReference Include="Microsoft.SemanticKernel.Agents.Magentic" Version="1.72.0-preview" />
    <PackageReference Include="Microsoft.SemanticKernel.Agents.Orchestration" Version="1.72.0-preview" />
    <PackageReference Include="Microsoft.SemanticKernel.Agents.Runtime.InProcess" Version="1.72.0-preview" />
    <PackageReference Include="Microsoft.SemanticKernel.Connectors.Google" Version="1.72.0-alpha" />
    <PackageReference Include="QuestPDF" Version="2026.2.1" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\Shared\AiAgileTeam.Shared.csproj" />
  </ItemGroup>

</Project>

```

## File: Api\AiAgileTeam.Api.http
```
@AiAgileTeam.Api_HostAddress = http://localhost:5270

GET {{AiAgileTeam.Api_HostAddress}}/weatherforecast/
Accept: application/json

###

```

## File: Api\Program.cs
```csharp
using AiAgileTeam.Services;
using AiAgileTeam.Services.Orchestration;
using Polly;
using Polly.Extensions.Http;
using QuestPDF.Infrastructure;

QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddSingleton<SessionStore>();
builder.Services.AddSingleton<MarkdownService>();
builder.Services.AddSingleton<GroupChatOrchestrationStrategy>();
builder.Services.AddSingleton<MagenticOrchestrationStrategy>();
builder.Services.AddSingleton<OrchestrationStrategyFactory>();

static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
}

builder.Services.AddHttpClient("AiTeamClient")
    .AddPolicyHandler(GetRetryPolicy());
builder.Services.AddSingleton<AiTeamService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazorClient",
        policy =>
        {
            policy.WithOrigins("http://localhost:5139", "https://localhost:7169")
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Basic logging to verify requests reach the API
app.Use(async (context, next) =>
{
    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Request: {context.Request.Method} {context.Request.Path}");
    await next();
});

app.UseCors("AllowBlazorClient");
app.UseAuthorization();
app.MapControllers();

app.Run();

```

## File: Api\Controllers\AiTeamController.cs
```csharp
#pragma warning disable SKEXP0110, SKEXP0070, SKEXP0001
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.ChatCompletion;
using AiAgileTeam.Models;
using AiAgileTeam.Services;
using AiAgileTeam.Services.Orchestration;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AiAgileTeam.Api.Controllers;

[ApiController]
[Route("api/aiteam")]
public class AiTeamController : ControllerBase
{
    private readonly AiTeamService _teamService;
    private readonly SessionStore _sessionStore;
    private readonly MarkdownService _markdownService;
    private readonly OrchestrationStrategyFactory _orchestrationStrategyFactory;

    public AiTeamController(
        AiTeamService teamService,
        SessionStore sessionStore,
        MarkdownService markdownService,
        OrchestrationStrategyFactory orchestrationStrategyFactory)
    {
        _teamService = teamService;
        _sessionStore = sessionStore;
        _markdownService = markdownService;
        _orchestrationStrategyFactory = orchestrationStrategyFactory;
    }

    /// <summary>
    /// Returns API health status.
    /// </summary>
    [HttpGet("health")]
    public IActionResult GetHealth()
    {
        return Ok(new { status = "ok" });
    }

    /// <summary>
    /// Gets API config and model for clarification agent based on settings
    /// </summary>
    private (ApiConfig apiConfig, string model) GetClarificationConfig(AppSettings settings)
    {
        ApiConfig apiConfig;
        string model;
        
        if (settings.ApiKeyMode == "global")
        {
            apiConfig = settings.GlobalApi;
            // Use model from first selected agent or default
            model = settings.Agents.FirstOrDefault(a => a.IsSelected)?.ModelSettings?.Model ?? "gpt-4";
        }
        else
        {
            // Use first selected agent's settings
            var firstAgent = settings.Agents.FirstOrDefault(a => a.IsSelected);
            apiConfig = firstAgent?.ApiSettings ?? settings.GlobalApi;
            model = firstAgent?.ModelSettings?.Model ?? "gpt-4";
        }
        
        return (apiConfig, model);
    }

    [HttpPost("session")]
    public async IAsyncEnumerable<StreamingMessageDto> StartSession([FromBody] SessionRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        string serverSessionId = Guid.NewGuid().ToString();
        var sessionData = _sessionStore.GetOrCreate(serverSessionId);
        var groupChat = sessionData.GroupChat;
        var useGroupChatSessionBuffer = request.Clarify || request.Settings.OrchestrationMode == OrchestrationMode.GroupChat;
        
        if (useGroupChatSessionBuffer && request.History != null)
        {
            foreach (var msg in request.History)
            {
                var role = msg.IsUser ? AuthorRole.User : AuthorRole.Assistant;
                var chatMsg = new Microsoft.SemanticKernel.ChatMessageContent(role, msg.Content);
                chatMsg.AuthorName = msg.Author;
                groupChat.AddChatMessage(chatMsg);
            }
        }
        
        bool clarificationPhase = request.Clarify;
        ChatCompletionAgent? clarificationAgent = null;

        if (clarificationPhase)
        {
            var (apiConfig, model) = GetClarificationConfig(request.Settings);
            
            var cBuilder = Kernel.CreateBuilder();
            cBuilder.Services.AddSingleton(_teamService.CreateChatService(apiConfig, model));
            
            clarificationAgent = new ChatCompletionAgent
            {
                Name = "Clarification Agent",
                Instructions = "Your name is Clarification Agent. You are a Requirements Analyst. Your goal is to refine the user's initial request into a clear project brief.\n\n" +
                               "1. Ask up to 3 targeted questions to clarify the project's purpose, target audience, and key technical constraints.\n" +
                               "2. Ask questions one by one. Do not overwhelm the user.\n" +
                               "3. Once you have enough information, provide a structured 'Project Brief' summary.\n" +
                               "4. End your final summary with the exact word [READY].",
                Kernel = cBuilder.Build()
            };
            
            groupChat.AddChatMessage(new Microsoft.SemanticKernel.ChatMessageContent(AuthorRole.User, request.Query));
            
            await foreach (var content in ProcessAgentResponseAsync(clarificationAgent, groupChat, serverSessionId, cancellationToken))
            {
                yield return content;
            }
        }
        else
        {
            if (request.Settings.OrchestrationMode == OrchestrationMode.GroupChat)
            {
                groupChat.AddChatMessage(new Microsoft.SemanticKernel.ChatMessageContent(AuthorRole.User, request.Query));
            }

            var strategy = _orchestrationStrategyFactory.Resolve(request.Settings.OrchestrationMode);
            await foreach (var content in strategy.RunDiscussionAsync(request.Settings, sessionData, serverSessionId, request.Query, cancellationToken))
            {
                yield return content;
            }
        }
    }

    [HttpPost("report")]
    public IActionResult DownloadReport([FromBody] ReportRequest request)
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

        // Server-side safety: strip system/floor-change messages and [DONE] markers
        var cleanMessages = request.Messages
            .Where(m => m.Author != "System"
                        && !string.IsNullOrWhiteSpace(m.Content)
                        && !m.Content.TrimStart().StartsWith("*[Project Manager gives floor", StringComparison.Ordinal))
            .Select(m => new ChatMessageDto
            {
                Author = m.Author,
                IsUser = m.IsUser,
                Content = m.Content
                    .Replace("[DONE]", "", StringComparison.OrdinalIgnoreCase)
                    .TrimEnd()
            })
            .Where(m => !string.IsNullOrWhiteSpace(m.Content))
            .ToList();

        var deduplicatedMessages = new List<ChatMessageDto>(cleanMessages.Count);
        string? previousAuthor = null;
        string? previousContent = null;

        foreach (var message in cleanMessages)
        {
            var normalizedContent = message.Content.Trim();
            var isDuplicate = string.Equals(previousAuthor, message.Author, StringComparison.Ordinal)
                              && string.Equals(previousContent, normalizedContent, StringComparison.Ordinal);

            if (isDuplicate)
            {
                continue;
            }

            deduplicatedMessages.Add(message);
            previousAuthor = message.Author;
            previousContent = normalizedContent;
        }

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1, Unit.Inch);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(11));

                page.Header().PaddingBottom(10).Column(header =>
                {
                    header.Item().Text(request.Title).SemiBold().FontSize(20).FontColor(Colors.Blue.Medium);
                    header.Item().PaddingTop(2).Text($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC").FontSize(8).FontColor(Colors.Grey.Medium);
                });

                page.Content().Column(column =>
                {
                    column.Spacing(12);

                    foreach (var message in deduplicatedMessages)
                    {
                        if (message.IsUser)
                        {
                            // Render user request as a highlighted brief section
                            column.Item()
                                .Background(Colors.Blue.Lighten5)
                                .Padding(10)
                                .Column(section =>
                                {
                                    section.Item()
                                        .PaddingBottom(4)
                                        .Text("Project Request")
                                        .Bold()
                                        .FontSize(12)
                                        .FontColor(Colors.Blue.Darken2);

                                    section.Item()
                                        .Element(c => _markdownService.RenderMarkdown(c, message.Content));
                                });
                        }
                        else
                        {
                            // Render PM synthesis as the main document body
                            column.Item().Column(section =>
                            {
                                section.Item()
                                    .PaddingBottom(4)
                                    .Text($"Prepared by: {message.Author}")
                                    .FontSize(9)
                                    .FontColor(Colors.Grey.Darken1);

                                section.Item()
                                    .Element(c => _markdownService.RenderMarkdown(c, message.Content));
                            });
                        }
                    }
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Page ");
                    x.CurrentPageNumber();
                });
            });
        });

        byte[] pdfBytes = document.GeneratePdf();
        return File(pdfBytes, "application/pdf", "AiAgileTeam_Report.pdf");
    }

    [HttpPost("message")]
    public async IAsyncEnumerable<StreamingMessageDto> SendMessage([FromBody] SessionRequest request, [FromQuery] bool isClarificationPhase, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        string serverSessionId = request.ServerSessionId ?? Guid.NewGuid().ToString();
        var sessionData = _sessionStore.GetOrCreate(serverSessionId);
        var groupChat = sessionData.GroupChat;
        var useGroupChatSessionBuffer = isClarificationPhase || request.Settings.OrchestrationMode == OrchestrationMode.GroupChat;
        
        // Recover history if session is not configured and history is provided
        if (useGroupChatSessionBuffer && !sessionData.IsConfigured && request.History != null)
        {
            foreach(var msg in request.History) 
            {
                var role = msg.IsUser ? AuthorRole.User : AuthorRole.Assistant;
                var chatMsg = new Microsoft.SemanticKernel.ChatMessageContent(role, msg.Content);
                chatMsg.AuthorName = msg.Author;
                groupChat.AddChatMessage(chatMsg);
            }
        }
        
        if (useGroupChatSessionBuffer && !string.IsNullOrWhiteSpace(request.Query))
        {
            groupChat.AddChatMessage(new Microsoft.SemanticKernel.ChatMessageContent(AuthorRole.User, request.Query));
        }

        if (isClarificationPhase)
        {
            var (apiConfig, model) = GetClarificationConfig(request.Settings);
            
            var cBuilder = Kernel.CreateBuilder();
            cBuilder.Services.AddSingleton(_teamService.CreateChatService(apiConfig, model));
            
            var clarificationAgent = new ChatCompletionAgent
            {
                Name = "Clarification Agent",
                Instructions = "Your name is Clarification Agent. You are a Requirements Analyst. Your goal is to refine the user's initial request into a clear project brief.\n\n" +
                               "1. Ask up to 3 targeted questions to clarify the project's purpose, target audience, and key technical constraints.\n" +
                               "2. Ask questions one by one. Do not overwhelm the user.\n" +
                               "3. Once you have enough information, provide a structured 'Project Brief' summary.\n" +
                               "4. End your final summary with the exact word [READY].",
                Kernel = cBuilder.Build()
            };

            await foreach (var content in ProcessAgentResponseAsync(clarificationAgent, groupChat, serverSessionId, cancellationToken))
            {
                yield return content;
            }
        }
        else
        {
            var strategy = _orchestrationStrategyFactory.Resolve(request.Settings.OrchestrationMode);
            await foreach (var content in strategy.RunDiscussionAsync(request.Settings, sessionData, serverSessionId, request.Query, cancellationToken))
            {
                yield return content;
            }
        }
    }

    private async IAsyncEnumerable<StreamingMessageDto> ProcessAgentResponseAsync(ChatCompletionAgent agent, AgentGroupChat groupChat, string serverSessionId, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        string currentContent = "";
        Exception? caughtException = null;
        
        IAsyncEnumerator<Microsoft.SemanticKernel.ChatMessageContent>? enumerator = null;
        try
        {
            enumerator = groupChat.InvokeAsync(agent).GetAsyncEnumerator(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            caughtException = ex;
        }

        if (caughtException == null)
        {
            bool hasMore = true;
            while (hasMore)
            {
                try
                {
                    hasMore = await enumerator!.MoveNextAsync();
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    hasMore = false;
                    caughtException = ex;
                }

                if (hasMore)
                {
                    var message = enumerator!.Current;
                    currentContent += message.Content;
                    yield return new StreamingMessageDto
                    {
                        Author = agent.Name ?? "Agent", // Author is always the clarification agent
                        ContentPiece = message.Content ?? "",
                        IsComplete = false,
                        ServerSessionId = serverSessionId
                    };
                }
            }
        }

        if (enumerator != null)
        {
            await enumerator.DisposeAsync();
        }

        if (caughtException != null)
        {
            yield return new StreamingMessageDto { Author = "System", ContentPiece = $"\r\n⚠️ Error during processing: {caughtException.Message}\r\n", IsComplete = true, ServerSessionId = serverSessionId };
        }
        else
        {
            yield return new StreamingMessageDto { Author = agent.Name ?? "Agent", ContentPiece = "", IsComplete = true, ServerSessionId = serverSessionId };
        }
    }

}

```

## File: Api\Services\AiTeamService.cs
```csharp
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

        if (string.IsNullOrWhiteSpace(model))
        {
            throw new ArgumentException(
                $"Model is not configured for agent '{agentConfig.DisplayName}' ({agentConfig.Role}). Please set a model in the agent's settings.");
        }
        
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
                new PromptExecutionSettings { ExtensionData = new Dictionary<string, object>
                    { ["max_tokens"] = maxTokens } })
        };

        return agent;
    }
}

```

## File: Api\Services\ChatContextCompressor.cs
```csharp
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace AiAgileTeam.Services;

/// <summary>
/// Compresses chat history for agents: keeps the last N messages verbatim
/// and summarizes older messages into a single compact paragraph via LLM.
/// Caches the running summary so only new messages are re-summarized.
/// </summary>
public sealed class ChatContextCompressor
{
    private readonly IChatCompletionService _llm;

    /// <summary>Number of most-recent messages kept verbatim (not summarized).</summary>
    private readonly int _tailSize;

    /// <summary>Cached running summary of messages already processed.</summary>
    private string _runningSummary = "";

    /// <summary>Number of messages already covered by <see cref="_runningSummary"/>.</summary>
    private int _summarizedCount;

    private static readonly string SummarizeSystemPrompt =
        """
        You are a concise summarizer for a software project discussion.
        You receive the previous running summary (may be empty) and a batch of new messages.
        Produce ONE compact paragraph (max 300 words) that captures:
        - key decisions and agreements
        - open questions and unresolved issues
        - each participant's main contribution
        Do NOT add opinions. Do NOT use markdown headers. Write in the same language as the messages.
        """;

    public ChatContextCompressor(IChatCompletionService llm, int tailSize = 4)
    {
        ArgumentNullException.ThrowIfNull(llm);
        if (tailSize < 1) throw new ArgumentOutOfRangeException(nameof(tailSize));

        _llm = llm;
        _tailSize = tailSize;
    }

    /// <summary>
    /// Returns a compact representation of the full history suitable for agent context.
    /// </summary>
    public async Task<CompressedContext> CompressAsync(
        IReadOnlyList<ChatMessageContent> fullHistory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fullHistory);

        // Nothing to compress — return as-is
        if (fullHistory.Count <= _tailSize)
        {
            return new CompressedContext(
                Summary: "",
                RecentMessages: fullHistory.ToList());
        }

        int boundaryIndex = fullHistory.Count - _tailSize;

        // Only re-summarize if there are new messages beyond what we already covered
        if (boundaryIndex > _summarizedCount)
        {
            var newBatch = fullHistory
                .Skip(_summarizedCount)
                .Take(boundaryIndex - _summarizedCount)
                .ToList();

            _runningSummary = await SummarizeBatchAsync(
                _runningSummary, newBatch, cancellationToken);
            _summarizedCount = boundaryIndex;
        }

        var recentMessages = fullHistory.Skip(boundaryIndex).ToList();

        return new CompressedContext(
            Summary: _runningSummary,
            RecentMessages: recentMessages);
    }

    /// <summary>
    /// Formats compressed context as a single text block for injection into a prompt.
    /// </summary>
    public static string FormatForPrompt(CompressedContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(ctx.Summary))
        {
            parts.Add($"[Discussion summary so far]\n{ctx.Summary}");
        }

        if (ctx.RecentMessages.Count > 0)
        {
            parts.Add("[Recent messages]");
            foreach (var msg in ctx.RecentMessages)
            {
                string author = msg.AuthorName ?? msg.Role.ToString();
                parts.Add($"{author}: {msg.Content}");
            }
        }

        return string.Join("\n\n", parts);
    }

    /// <summary>Resets cached state. Call when starting a new discussion round.</summary>
    public void Reset()
    {
        _runningSummary = "";
        _summarizedCount = 0;
    }

    private async Task<string> SummarizeBatchAsync(
        string previousSummary,
        IReadOnlyList<ChatMessageContent> newMessages,
        CancellationToken cancellationToken)
    {
        var history = new ChatHistory();
        history.AddSystemMessage(SummarizeSystemPrompt);

        var userContent = new System.Text.StringBuilder();
        if (!string.IsNullOrWhiteSpace(previousSummary))
        {
            userContent.AppendLine("[Previous summary]");
            userContent.AppendLine(previousSummary);
            userContent.AppendLine();
        }

        userContent.AppendLine("[New messages to incorporate]");
        foreach (var msg in newMessages)
        {
            string author = msg.AuthorName ?? msg.Role.ToString();
            userContent.AppendLine($"{author}: {msg.Content}");
        }

        history.AddUserMessage(userContent.ToString());

        var result = await _llm.GetChatMessageContentAsync(
            history,
            executionSettings: new PromptExecutionSettings
            {
                ExtensionData = new Dictionary<string, object>
                {
                    ["max_tokens"] = 400,
                    ["temperature"] = 0.2
                }
            },
            cancellationToken: cancellationToken);

        return result.Content ?? previousSummary;
    }
}

/// <summary>
/// Holds a compressed view of the conversation: an LLM-generated summary of older messages
/// plus the most recent messages in full.
/// </summary>
public sealed record CompressedContext(
    string Summary,
    IReadOnlyList<ChatMessageContent> RecentMessages);

```

## File: Api\Services\DeterministicStrategies.cs
```csharp
// Файл удален — стратегии перенесены в отдельные файлы:
// - ResilientWorkflowSelectionStrategy.cs
// - TokenSavingTerminationStrategy.cs

```

## File: Api\Services\MarkdownService.cs
```csharp
using Markdig;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace AiAgileTeam.Services;

public class MarkdownService
{
    private readonly MarkdownPipeline _pipeline;

    public MarkdownService()
    {
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();
    }

    public void RenderMarkdown(IContainer container, string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            container.Text("");
            return;
        }

        var document = Markdown.Parse(markdown, _pipeline);

        container.Column(col =>
        {
            col.Spacing(4);

            foreach (var block in document)
            {
                col.Item().Element(c => RenderBlock(c, block));
            }
        });
    }

    private void RenderBlock(IContainer container, Block block)
    {
        switch (block)
        {
            case ParagraphBlock paragraph:
                RenderParagraph(container, paragraph);
                break;

            case HeadingBlock heading:
                RenderHeading(container, heading);
                break;

            case ListBlock list:
                RenderList(container, list);
                break;

            case CodeBlock codeBlock:
                RenderCodeBlock(container, codeBlock);
                break;

            case QuoteBlock quote:
                RenderQuote(container, quote);
                break;

            case ThematicBreakBlock:
                container.PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Grey.Lighten2);
                break;

            default:
                var text = block.ToString() ?? "";
                container.Text(text);
                break;
        }
    }

    private void RenderParagraph(IContainer container, ParagraphBlock paragraph)
    {
        container.Text(text => RenderInline(text, paragraph.Inline));
    }

    private void RenderHeading(IContainer container, HeadingBlock heading)
    {
        var fontSize = heading.Level switch
        {
            1 => 24,
            2 => 20,
            3 => 16,
            4 => 14,
            _ => 12
        };

        container.Text(text => RenderInline(text, heading.Inline, fontSize, Colors.Blue.Medium, isBold: true));
    }

    private void RenderList(IContainer container, ListBlock list)
    {
        var isOrdered = list.IsOrdered;
        var index = 0;

        container.Column(col =>
        {
            col.Spacing(2);

            foreach (var item in list)
            {
                if (item is ListItemBlock listItem)
                {
                    index++;
                    var prefix = isOrdered ? $"{index}." : "•";
                    
                    col.Item().Row(row =>
                    {
                        row.ConstantItem(20).Text(prefix);
                        row.RelativeItem().Column(itemCol =>
                        {
                            foreach (var child in listItem)
                            {
                                if (child is ParagraphBlock p)
                                {
                                    itemCol.Item().Element(c => RenderParagraph(c, p));
                                }
                                else if (child is ListBlock nestedList)
                                {
                                    itemCol.Item().Element(c => RenderList(c, nestedList));
                                }
                            }
                        });
                    });
                }
            }
        });
    }

    private static void RenderInline(
        TextDescriptor text,
        Inline? inline,
        float? fontSize = null,
        string? fontColor = null,
        bool isBold = false,
        bool isItalic = false,
        bool isMonospace = false,
        bool isUnderline = false)
    {
        var current = inline;
        while (current != null)
        {
            switch (current)
            {
                case LiteralInline literal:
                    var spanText = literal.Content.ToString();
                    if (!string.IsNullOrEmpty(spanText))
                    {
                        ApplyStyle(text.Span(spanText), fontSize, fontColor, isBold, isItalic, isMonospace, isUnderline);
                    }
                    break;

                case EmphasisInline emphasis:
                    var emphasisBold = isBold || emphasis.DelimiterCount >= 2;
                    var emphasisItalic = isItalic || emphasis.DelimiterCount == 1;
                    RenderInline(text, emphasis.FirstChild, fontSize, fontColor, emphasisBold, emphasisItalic, isMonospace, isUnderline);
                    break;

                case CodeInline code:
                    ApplyStyle(text.Span(code.Content), fontSize, fontColor, isBold, isItalic, isMonospace: true, isUnderline);
                    break;

                case LinkInline link:
                    RenderInline(text, link.FirstChild, fontSize, Colors.Blue.Medium, isBold, isItalic, isMonospace, isUnderline: true);
                    if (!string.IsNullOrWhiteSpace(link.Url))
                    {
                        ApplyStyle(text.Span($" ({link.Url})"), fontSize, Colors.Blue.Medium, false, false, false, false);
                    }
                    break;

                case LineBreakInline:
                    text.Span("\n");
                    break;

                case ContainerInline containerInline:
                    RenderInline(text, containerInline.FirstChild, fontSize, fontColor, isBold, isItalic, isMonospace, isUnderline);
                    break;
            }

            current = current.NextSibling;
        }
    }

    private static void ApplyStyle(
        TextSpanDescriptor span,
        float? fontSize,
        string? fontColor,
        bool isBold,
        bool isItalic,
        bool isMonospace,
        bool isUnderline)
    {
        if (fontSize.HasValue)
        {
            span.FontSize(fontSize.Value);
        }

        if (!string.IsNullOrWhiteSpace(fontColor))
        {
            span.FontColor(fontColor);
        }

        if (isBold)
        {
            span.Bold();
        }

        if (isItalic)
        {
            span.Italic();
        }

        if (isMonospace)
        {
            span.FontFamily("Courier New");
        }

        if (isUnderline)
        {
            span.Underline();
        }
    }

    private void RenderCodeBlock(IContainer container, CodeBlock codeBlock)
    {
        var code = codeBlock.Lines.ToString();
        container
            .Background(Colors.Grey.Lighten3)
            .Padding(8)
            .Text(code)
            .FontFamily("Courier New")
            .FontSize(10);
    }

    private void RenderQuote(IContainer container, QuoteBlock quote)
    {
        container
            .BorderLeft(3)
            .BorderColor(Colors.Blue.Lighten2)
            .PaddingLeft(8)
            .Column(col =>
            {
                col.Spacing(4);

                foreach (var block in quote)
                {
                    col.Item().Element(c => RenderBlock(c, block));
                }
            });
    }
}

```

## File: Api\Services\PmOrchestratorSelectionStrategy.cs
```csharp
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.ChatCompletion;

namespace AiAgileTeam.Services;

/// <summary>
/// Selection strategy where the Project Manager LLM decides which agent speaks next
/// based on a compressed conversation context. Includes anti-loop guards and @mention support.
/// </summary>
public sealed class PmOrchestratorSelectionStrategy : SelectionStrategy
{
    private readonly IChatCompletionService _pmLlm;
    private readonly ChatContextCompressor _compressor;

    /// <summary>Maximum times the same agent can speak consecutively before forcing rotation.</summary>
    private const int MaxConsecutiveSameAgent = 2;

    /// <summary>Maximum LLM retries when PM returns an unparseable response.</summary>
    private const int MaxParseRetries = 2;

    private string? _lastSelectedRole;
    private int _consecutiveCount;

    private static readonly string OrchestratorSystemPrompt =
        """
        You are the orchestrator of a software team discussion.
        Based on the discussion context, decide which team member should speak NEXT.

        Available roles: {ROLES}

        Rules:
        1. Each expert should speak at least once before the discussion ends.
        2. Do not select the same expert more than twice in a row.
        3. If all experts have contributed and the discussion is complete, respond with exactly: DONE
        4. Otherwise respond with ONLY the role name (e.g. "Architect" or "QA Engineer"). Nothing else.

        Respond with a single line: either the exact role name or DONE.
        """;

    public PmOrchestratorSelectionStrategy(
        IChatCompletionService pmLlm,
        ChatContextCompressor compressor)
    {
        ArgumentNullException.ThrowIfNull(pmLlm);
        ArgumentNullException.ThrowIfNull(compressor);

        _pmLlm = pmLlm;
        _compressor = compressor;
    }

    /// <summary>Resets internal state for a new discussion round.</summary>
    public void Reset()
    {
        _lastSelectedRole = null;
        _consecutiveCount = 0;
        _compressor.Reset();
    }

    protected override async Task<Agent> SelectAgentAsync(
        IReadOnlyList<Agent> agents,
        IReadOnlyList<ChatMessageContent> history,
        CancellationToken cancellationToken = default)
    {
        var lastMessage = history.LastOrDefault();

        // 1. User @mention — direct handoff without LLM call
        if (lastMessage is not null && lastMessage.Role == AuthorRole.User)
        {
            var mentioned = TryFindMentionedAgent(agents, lastMessage.Content);
            if (mentioned is not null)
            {
                TrackSelection(GetAgentRole(mentioned));
                return mentioned;
            }

            // User interrupted — PM should re-evaluate, so give floor to PM first
            var pm = FindAgentByRole(agents, "Project Manager");
            if (pm is not null)
            {
                TrackSelection("Project Manager");
                return pm;
            }
        }

        // 2. Compress history
        var compressed = await _compressor.CompressAsync(history, cancellationToken);
        string contextText = ChatContextCompressor.FormatForPrompt(compressed);

        // 3. Build role list (excluding PM — PM is the orchestrator, not a discussion participant here)
        var nonPmAgents = agents.Where(a => GetAgentRole(a) != "Project Manager").ToList();
        var allRoles = nonPmAgents.Select(GetAgentRole).Distinct().ToList();
        string rolesStr = string.Join(", ", allRoles);

        // 4. Ask PM LLM to pick next speaker
        string? chosenRole = null;
        for (int attempt = 0; attempt <= MaxParseRetries; attempt++)
        {
            chosenRole = await AskPmForNextSpeakerAsync(
                contextText, rolesStr, cancellationToken);

            if (chosenRole is not null)
                break;
        }

        // 5. Handle DONE — hand back to PM agent for final synthesis
        if (string.Equals(chosenRole, "DONE", StringComparison.OrdinalIgnoreCase))
        {
            var pm = FindAgentByRole(agents, "Project Manager")
                     ?? throw new InvalidOperationException("Project Manager agent not found");
            TrackSelection("Project Manager");
            return pm;
        }

        // 6. Anti-loop guard
        if (chosenRole is not null
            && chosenRole == _lastSelectedRole
            && _consecutiveCount >= MaxConsecutiveSameAgent)
        {
            // Force a different agent
            var alternative = allRoles.FirstOrDefault(r => r != _lastSelectedRole);
            if (alternative is not null)
            {
                chosenRole = alternative;
            }
        }

        // 7. Resolve agent by role
        if (chosenRole is not null)
        {
            var agent = FindAgentByRole(agents, chosenRole);
            if (agent is not null)
            {
                TrackSelection(chosenRole);
                return agent;
            }
        }

        // 8. Fallback: round-robin through non-PM agents that haven't spoken recently
        var fallback = PickFallbackAgent(agents, history);
        TrackSelection(GetAgentRole(fallback));
        return fallback;
    }

    private async Task<string?> AskPmForNextSpeakerAsync(
        string contextText,
        string availableRoles,
        CancellationToken cancellationToken)
    {
        var prompt = OrchestratorSystemPrompt.Replace("{ROLES}", availableRoles);

        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(prompt);
        chatHistory.AddUserMessage(contextText);

        try
        {
            var result = await _pmLlm.GetChatMessageContentAsync(
                chatHistory,
                executionSettings: new PromptExecutionSettings
                {
                    ExtensionData = new Dictionary<string, object>
                    {
                        ["max_tokens"] = 30,
                        ["temperature"] = 0.1
                    }
                },
                cancellationToken: cancellationToken);

            var response = result.Content?.Trim();
            if (string.IsNullOrWhiteSpace(response))
                return null;

            // Check for DONE
            if (response.Contains("DONE", StringComparison.OrdinalIgnoreCase))
                return "DONE";

            // Try to find a known role in the response
            foreach (var role in availableRoles.Split(',', StringSplitOptions.TrimEntries))
            {
                if (response.Contains(role, StringComparison.OrdinalIgnoreCase))
                    return role;
            }

            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.WriteLine($"[PmOrchestrator] LLM call failed: {ex.Message}");
            return null;
        }
    }

    private void TrackSelection(string role)
    {
        if (role == _lastSelectedRole)
        {
            _consecutiveCount++;
        }
        else
        {
            _lastSelectedRole = role;
            _consecutiveCount = 1;
        }
    }

    private static Agent PickFallbackAgent(
        IReadOnlyList<Agent> agents,
        IReadOnlyList<ChatMessageContent> history)
    {
        // Find agents who haven't spoken yet
        var spokenRoles = history
            .Where(m => m.Role == AuthorRole.Assistant && !string.IsNullOrEmpty(m.AuthorName))
            .Select(m => m.AuthorName!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var unspoken = agents.FirstOrDefault(a =>
            GetAgentRole(a) != "Project Manager"
            && !spokenRoles.Contains(a.Name ?? ""));

        if (unspoken is not null)
            return unspoken;

        // Everyone has spoken — give floor to PM for synthesis
        return FindAgentByRole(agents, "Project Manager")
               ?? agents[0];
    }

    private static Agent? TryFindMentionedAgent(
        IReadOnlyList<Agent> agents, string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return null;

        foreach (var agent in agents)
        {
            if (content.Contains($"@{agent.Name}", StringComparison.OrdinalIgnoreCase))
                return agent;
        }

        foreach (var agent in agents)
        {
            var role = GetAgentRole(agent);
            if (content.Contains($"@{role}", StringComparison.OrdinalIgnoreCase))
                return agent;
        }

        return null;
    }

    private static Agent? FindAgentByRole(IReadOnlyList<Agent> agents, string role)
    {
        return agents.FirstOrDefault(a =>
            string.Equals(GetAgentRole(a), role, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Extracts agent role from instructions formatted as:
    /// "Your name is {DisplayName}. You are a {Role}. {SystemPrompt}"
    /// </summary>
    private static string GetAgentRole(Agent agent)
    {
        var instructions = agent.Instructions ?? "";
        var match = System.Text.RegularExpressions.Regex.Match(
            instructions, @"You are a ([^.]+)\.");
        return match.Success ? match.Groups[1].Value.Trim() : "Unknown";
    }
}

```

## File: Api\Services\ResilientWorkflowSelectionStrategy.cs
```csharp
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

```

## File: Api\Services\SafeTerminationStrategy.cs
```csharp
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.ChatCompletion;

namespace AiAgileTeam.Services;

/// <summary>
/// Robust termination strategy that detects:
/// 1. [DONE] token from Project Manager (normal completion)
/// 2. Repetition loops (same agent selected too many times in a row)
/// 3. Stale content (last N messages are near-identical)
/// MaximumIterations (inherited) serves as the hard safety net.
/// </summary>
public sealed class SafeTerminationStrategy : TerminationStrategy
{
    /// <summary>How many consecutive turns by the same agent trigger a forced stop.</summary>
    private const int MaxConsecutiveSameAgent = 3;

    /// <summary>How many recent messages to check for staleness.</summary>
    private const int StalenessWindow = 4;

    /// <summary>Similarity ratio (0-1) above which two messages are considered identical.</summary>
    private const double StalenessThreshold = 0.85;

    protected override Task<bool> ShouldAgentTerminateAsync(
        Agent agent,
        IReadOnlyList<ChatMessageContent> history,
        CancellationToken cancellationToken = default)
    {
        // 1. Normal completion: PM said [DONE]
        var lastMessage = history.LastOrDefault();
        if (lastMessage is not null
            && string.Equals(lastMessage.AuthorName, "Project Manager", StringComparison.OrdinalIgnoreCase)
            && lastMessage.Content is not null
            && lastMessage.Content.Contains("[DONE]", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("[SafeTermination] PM said [DONE] — terminating.");
            return Task.FromResult(true);
        }

        // 2. Loop detection: same author N times in a row
        if (history.Count >= MaxConsecutiveSameAgent)
        {
            var tail = history
                .Skip(history.Count - MaxConsecutiveSameAgent)
                .ToList();

            bool allSameAuthor = tail
                .All(m => string.Equals(m.AuthorName, tail[0].AuthorName, StringComparison.OrdinalIgnoreCase));

            if (allSameAuthor && !string.IsNullOrEmpty(tail[0].AuthorName))
            {
                Console.WriteLine(
                    $"[SafeTermination] Loop detected: {tail[0].AuthorName} spoke {MaxConsecutiveSameAgent} times in a row — terminating.");
                return Task.FromResult(true);
            }
        }

        // 3. Staleness detection: last N messages are near-identical
        if (history.Count >= StalenessWindow)
        {
            var recentContents = history
                .Skip(history.Count - StalenessWindow)
                .Where(m => m.Role == AuthorRole.Assistant && !string.IsNullOrWhiteSpace(m.Content))
                .Select(m => m.Content!)
                .ToList();

            if (recentContents.Count >= StalenessWindow - 1 && AreAllSimilar(recentContents))
            {
                Console.WriteLine("[SafeTermination] Stale content detected — terminating.");
                return Task.FromResult(true);
            }
        }

        return Task.FromResult(false);
    }

    /// <summary>
    /// Checks whether all strings in the list are similar to the first one
    /// using a simple Jaccard similarity on word sets.
    /// </summary>
    private static bool AreAllSimilar(IReadOnlyList<string> texts)
    {
        if (texts.Count < 2)
            return false;

        var baseWords = Tokenize(texts[0]);
        for (int i = 1; i < texts.Count; i++)
        {
            var otherWords = Tokenize(texts[i]);
            double similarity = JaccardSimilarity(baseWords, otherWords);
            if (similarity < StalenessThreshold)
                return false;
        }

        return true;
    }

    private static HashSet<string> Tokenize(string text)
    {
        return text
            .Split([' ', '\n', '\r', '\t', '.', ',', ';', ':', '!', '?'],
                   StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.ToLowerInvariant())
            .ToHashSet();
    }

    private static double JaccardSimilarity(HashSet<string> a, HashSet<string> b)
    {
        if (a.Count == 0 && b.Count == 0)
            return 1.0;

        int intersection = a.Count(w => b.Contains(w));
        int union = a.Count + b.Count - intersection;
        return union == 0 ? 1.0 : (double)intersection / union;
    }
}

```

## File: Api\Services\SessionStore.cs
```csharp
using System;
using System.Collections.Concurrent;
using AiAgileTeam.Models;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.ChatCompletion;

namespace AiAgileTeam.Services;

public class SessionData
{
    public AgentGroupChat GroupChat { get; } = new();
    public bool IsConfigured { get; set; }
    public OrchestrationMode? OrchestrationMode { get; set; }

    /// <summary>Context compressor instance bound to this session's PM LLM.</summary>
    public ChatContextCompressor? Compressor { get; set; }

    /// <summary>The PM's chat completion service, reused for orchestration LLM calls.</summary>
    public IChatCompletionService? PmLlm { get; set; }
}

public class SessionStore
{
    public record SessionEntry(SessionData Data, DateTime LastAccessed);

    private readonly ConcurrentDictionary<string, SessionEntry> _sessions = new();

    public SessionData GetOrCreate(string sessionId)
    {
        var entry = _sessions.GetOrAdd(sessionId, id => new SessionEntry(new SessionData(), DateTime.UtcNow));
        Touch(sessionId);
        return entry.Data;
    }

    public SessionData? Get(string sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var entry))
        {
            Touch(sessionId);
            return entry.Data;
        }
        return null;
    }

    public void Touch(string sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var entry))
        {
            _sessions[sessionId] = entry with { LastAccessed = DateTime.UtcNow };
        }
    }

    public void Remove(string sessionId)
    {
        _sessions.TryRemove(sessionId, out _);
    }
}

```

## File: Api\Services\TokenSavingTerminationStrategy.cs
```csharp
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

```

## File: Api\Services\Orchestration\GroupChatOrchestrationStrategy.cs
```csharp
#pragma warning disable SKEXP0110, SKEXP0070, SKEXP0001
using System.Runtime.CompilerServices;
using AiAgileTeam.Models;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;

namespace AiAgileTeam.Services.Orchestration;

/// <summary>
/// Runs team discussion with AgentGroupChat and custom PM-driven strategies.
/// </summary>
public sealed class GroupChatOrchestrationStrategy : IOrchestrationStrategy
{
    private readonly AiTeamService _teamService;

    public GroupChatOrchestrationStrategy(AiTeamService teamService)
    {
        ArgumentNullException.ThrowIfNull(teamService);
        _teamService = teamService;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<StreamingMessageDto> RunDiscussionAsync(
        AppSettings settings,
        SessionData sessionData,
        string serverSessionId,
        string userQuery,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(sessionData);
        ArgumentException.ThrowIfNullOrWhiteSpace(serverSessionId);

        var groupChat = sessionData.GroupChat;

        if (!sessionData.IsConfigured)
        {
            var agentsToRun = settings.Agents.Where(a => a.IsSelected).ToList();
            ChatCompletionAgent? projectManager = null;

            Console.WriteLine($"[GroupChatOrchestration] Creating agents. Total selected: {agentsToRun.Count}");
            foreach (var agentConfig in agentsToRun)
            {
                Console.WriteLine($"[GroupChatOrchestration]   - {agentConfig.DisplayName} ({agentConfig.Role}) IsSelected={agentConfig.IsSelected}, IsMandatory={agentConfig.IsMandatory}");
                var agent = _teamService.CreateAgent(agentConfig, settings);
                groupChat.AddAgent(agent);
                if (agentConfig.Role == "Project Manager")
                {
                    projectManager = agent;
                }
            }

            if (projectManager != null)
            {
                var pmConfig = agentsToRun.First(a => a.Role == "Project Manager");
                ApiConfig pmApiConfig = settings.ApiKeyMode == "global"
                    ? settings.GlobalApi
                    : pmConfig.ApiSettings ?? settings.GlobalApi;
                var pmLlm = _teamService.CreateChatService(pmApiConfig, pmConfig.ModelSettings.Model);

                sessionData.PmLlm = pmLlm;
                sessionData.Compressor = new ChatContextCompressor(pmLlm, tailSize: 4);

                var selectionStrategy = new PmOrchestratorSelectionStrategy(pmLlm, sessionData.Compressor);

                groupChat.ExecutionSettings = new()
                {
                    SelectionStrategy = selectionStrategy,
                    TerminationStrategy = new SafeTerminationStrategy()
                    {
                        MaximumIterations = agentsToRun.Count * 3 + 3
                    }
                };
                Console.WriteLine($"[GroupChatOrchestration] PM-driven orchestration configured. Agents count: {agentsToRun.Count}");
            }
            else
            {
                Console.WriteLine("[GroupChatOrchestration] WARNING: Project Manager not found in selected agents!");
            }

            sessionData.IsConfigured = true;
            sessionData.OrchestrationMode = OrchestrationMode.GroupChat;
        }

        string currentAuthor = string.Empty;
        bool firstChunk = true;
        bool currentAuthorHasContent = false;
        bool pendingFloorChange = false;
        Exception? caughtException = null;

        IAsyncEnumerator<StreamingChatMessageContent>? enumerator = null;
        try
        {
            enumerator = groupChat.InvokeStreamingAsync().GetAsyncEnumerator(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            caughtException = ex;
        }

        if (caughtException == null)
        {
            bool hasMore = true;
            while (hasMore)
            {
                try
                {
                    hasMore = await enumerator!.MoveNextAsync();
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    hasMore = false;
                    caughtException = ex;
                }

                if (!hasMore)
                {
                    continue;
                }

                var message = enumerator!.Current;

                Console.WriteLine($"[GroupChatOrchestration] SK stream chunk: AuthorName='{message.AuthorName}', Content='{message.Content?.Substring(0, Math.Min(30, message.Content?.Length ?? 0))}', HasContent={!string.IsNullOrEmpty(message.Content)}");

                if (firstChunk || (!string.IsNullOrEmpty(message.AuthorName) && message.AuthorName != currentAuthor))
                {
                    if (!firstChunk && !string.IsNullOrEmpty(currentAuthor) && currentAuthorHasContent)
                    {
                        yield return new StreamingMessageDto { Author = currentAuthor, ContentPiece = string.Empty, IsComplete = true, ServerSessionId = serverSessionId };
                    }
                    else if (!firstChunk && !string.IsNullOrEmpty(currentAuthor) && !currentAuthorHasContent)
                    {
                        Console.WriteLine($"[GroupChatOrchestration] Skipping empty turn from '{currentAuthor}'");
                    }

                    if (!string.IsNullOrEmpty(message.AuthorName))
                    {
                        currentAuthor = message.AuthorName;
                    }
                    else if (firstChunk)
                    {
                        currentAuthor = "Unknown";
                    }

                    pendingFloorChange = true;
                    currentAuthorHasContent = false;
                    firstChunk = false;
                }

                if (string.IsNullOrEmpty(message.Content) || message.Content.Trim().All(char.IsWhiteSpace))
                {
                    continue;
                }

                if (pendingFloorChange)
                {
                    yield return new StreamingMessageDto
                    {
                        Author = "System",
                        ContentPiece = $"\r\n*[Project Manager gives floor to {currentAuthor}...]*\r\n\r\n",
                        IsComplete = true,
                        ServerSessionId = serverSessionId
                    };
                    pendingFloorChange = false;
                }

                currentAuthorHasContent = true;
                yield return new StreamingMessageDto
                {
                    Author = currentAuthor,
                    ContentPiece = message.Content,
                    IsComplete = false,
                    ServerSessionId = serverSessionId
                };
            }
        }

        if (enumerator != null)
        {
            await enumerator.DisposeAsync();
        }

        if (caughtException != null)
        {
            var hint = string.Empty;
            try
            {
                var message = caughtException.Message ?? string.Empty;
                if (message.Contains("404") || message.Contains("Not Found", StringComparison.OrdinalIgnoreCase) || message.Contains("Response status code does not indicate success", StringComparison.OrdinalIgnoreCase))
                {
                    hint = "\r\nHint: Provider returned 404 Not Found. Check provider settings (ApiKey, Endpoint) and the model name — the model may be unavailable for your account or the name is incorrect.\r\n";
                }
            }
            catch
            {
                // no-op
            }

            Console.WriteLine($"[ERROR] Discussion exception: {caughtException}");
            yield return new StreamingMessageDto
            {
                Author = "System",
                ContentPiece = $"\r\n⚠️ Discussion error: {caughtException.Message}\r\n{hint}",
                IsComplete = true,
                ServerSessionId = serverSessionId
            };
        }
        else if (!firstChunk && currentAuthorHasContent)
        {
            yield return new StreamingMessageDto { Author = currentAuthor, ContentPiece = string.Empty, IsComplete = true, ServerSessionId = serverSessionId };
        }
    }
}

```

## File: Api\Services\Orchestration\IOrchestrationStrategy.cs
```csharp
using AiAgileTeam.Models;

namespace AiAgileTeam.Services.Orchestration;

/// <summary>
/// Defines a strategy for running team discussion orchestration.
/// </summary>
public interface IOrchestrationStrategy
{
    /// <summary>
    /// Runs a discussion phase and streams messages to the client.
    /// </summary>
    IAsyncEnumerable<StreamingMessageDto> RunDiscussionAsync(
        AppSettings settings,
        SessionData sessionData,
        string serverSessionId,
        string userQuery,
        CancellationToken cancellationToken);
}

```

## File: Api\Services\Orchestration\MagenticOrchestrationStrategy.cs
```csharp
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using AiAgileTeam.Models;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Magentic;
using Microsoft.SemanticKernel.Agents.Orchestration;
using Microsoft.SemanticKernel.Agents.Runtime.InProcess;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace AiAgileTeam.Services.Orchestration;

/// <summary>
/// Runs team discussion using Semantic Kernel Magentic orchestration.
/// </summary>
public sealed class MagenticOrchestrationStrategy : IOrchestrationStrategy
{
    private const int FinalResultTimeoutSeconds = 300;
    private readonly AiTeamService _teamService;

    public MagenticOrchestrationStrategy(AiTeamService teamService)
    {
        ArgumentNullException.ThrowIfNull(teamService);
        _teamService = teamService;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<StreamingMessageDto> RunDiscussionAsync(
        AppSettings settings,
        SessionData sessionData,
        string serverSessionId,
        string userQuery,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(sessionData);
        ArgumentException.ThrowIfNullOrWhiteSpace(serverSessionId);

        var selectedAgents = settings.Agents.Where(a => a.IsSelected).ToList();
        if (selectedAgents.Count == 0)
        {
            throw new InvalidOperationException("At least one selected agent is required for Magentic orchestration.");
        }

        var pmConfig = selectedAgents.FirstOrDefault(a => a.Role == "Project Manager");
        if (pmConfig is null)
        {
            throw new InvalidOperationException("Project Manager agent is required for Magentic orchestration.");
        }

        var agents = new List<Agent>(selectedAgents.Count);
        foreach (var agentConfig in selectedAgents)
        {
            agents.Add(_teamService.CreateAgent(agentConfig, settings));
        }

        ApiConfig pmApiConfig = settings.ApiKeyMode == "global"
            ? settings.GlobalApi
            : pmConfig.ApiSettings ?? settings.GlobalApi;
        IChatCompletionService managerLlm = _teamService.CreateChatService(pmApiConfig, pmConfig.ModelSettings.Model);

        var manager = new StandardMagenticManager(
            managerLlm,
            new OpenAIPromptExecutionSettings { Temperature = 0.1f })
        {
            MaximumInvocationCount = selectedAgents.Count * 3 + 3
        };

        var streamChannel = Channel.CreateUnbounded<StreamingMessageDto>();
        sessionData.IsConfigured = true;
        sessionData.OrchestrationMode = OrchestrationMode.Magentic;

        ValueTask OnResponseAsync(ChatMessageContent message)
        {
            if (string.IsNullOrWhiteSpace(message.Content))
            {
                return ValueTask.CompletedTask;
            }

            streamChannel.Writer.TryWrite(new StreamingMessageDto
            {
                Author = string.IsNullOrWhiteSpace(message.AuthorName) ? "Agent" : message.AuthorName,
                ContentPiece = message.Content,
                IsComplete = true,
                ServerSessionId = serverSessionId
            });

            return ValueTask.CompletedTask;
        }

        var orchestration = new MagenticOrchestration(manager, [.. agents])
        {
            ResponseCallback = OnResponseAsync
        };

        var runtime = new InProcessRuntime();
        await runtime.StartAsync();

        var task = string.IsNullOrWhiteSpace(userQuery)
            ? "Continue the team discussion using the existing context and produce a final synthesis."
            : userQuery;

        var orchestrationTask = Task.Run(async () =>
        {
            try
            {
                OrchestrationResult<string> result = await orchestration.InvokeAsync(task, runtime);
                string final = await result.GetValueAsync(TimeSpan.FromSeconds(FinalResultTimeoutSeconds), cancellationToken);

                if (!string.IsNullOrWhiteSpace(final))
                {
                    var finalContent = final.Contains("[DONE]", StringComparison.OrdinalIgnoreCase)
                        ? final
                        : $"{final}\n\n[DONE]";

                    streamChannel.Writer.TryWrite(new StreamingMessageDto
                    {
                        Author = "Project Manager",
                        ContentPiece = finalContent,
                        IsComplete = true,
                        ServerSessionId = serverSessionId
                    });
                }
            }
            finally
            {
                streamChannel.Writer.TryComplete();
                await runtime.RunUntilIdleAsync();
            }
        }, cancellationToken);

        await foreach (var message in streamChannel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return message;
        }

        await orchestrationTask;
    }
}

```

## File: Api\Services\Orchestration\OrchestrationStrategyFactory.cs
```csharp
using AiAgileTeam.Models;

namespace AiAgileTeam.Services.Orchestration;

/// <summary>
/// Resolves orchestration strategy by selected orchestration mode.
/// </summary>
public sealed class OrchestrationStrategyFactory
{
    private readonly GroupChatOrchestrationStrategy _groupChatStrategy;
    private readonly MagenticOrchestrationStrategy _magenticStrategy;

    public OrchestrationStrategyFactory(
        GroupChatOrchestrationStrategy groupChatStrategy,
        MagenticOrchestrationStrategy magenticStrategy)
    {
        ArgumentNullException.ThrowIfNull(groupChatStrategy);
        ArgumentNullException.ThrowIfNull(magenticStrategy);

        _groupChatStrategy = groupChatStrategy;
        _magenticStrategy = magenticStrategy;
    }

    /// <summary>
    /// Resolves strategy implementation for requested orchestration mode.
    /// </summary>
    public IOrchestrationStrategy Resolve(OrchestrationMode mode)
    {
        return mode switch
        {
            OrchestrationMode.Magentic => _magenticStrategy,
            _ => _groupChatStrategy
        };
    }
}

```

## File: Components\AddAgentModal.razor
```
@using AiAgileTeam.Models

<MudDialog>
    <DialogContent>
        <MudTextField Label="Agent Name" @bind-Value="_agent.DisplayName" Class="mb-3" />
        <MudTextField Label="Role" @bind-Value="_agent.Role" Class="mb-3" />
        <MudTextField Label="System Prompt" @bind-Value="_agent.SystemPrompt" Lines="4" Class="mb-3" />
    </DialogContent>
    <DialogActions>
        <MudButton OnClick="Cancel">Cancel</MudButton>
        <MudButton Color="Color.Primary" OnClick="Submit">Add Agent</MudButton>
    </DialogActions>
</MudDialog>

@code {
    [CascadingParameter]
    MudBlazor.IMudDialogInstance? MudDialog { get; set; }

    private AgentConfig _agent = new();

    void Submit()
    {
        if (string.IsNullOrWhiteSpace(_agent.DisplayName) || string.IsNullOrWhiteSpace(_agent.Role))
        {
            return; // Can add validation
        }

        MudDialog?.Close(DialogResult.Ok(_agent));
    }

    void Cancel() => MudDialog?.Cancel();
}

```

## File: Components\ApiSettingsEditor.razor
```
@using AiAgileTeam.Models

<MudSelect T="string" Label="Provider" @bind-Value="Config.Provider" Class="mb-3">
    <MudSelectItem Value="@("OpenAI")">OpenAI</MudSelectItem>
    <MudSelectItem Value="@("AzureOpenAI")">Azure OpenAI</MudSelectItem>
    <MudSelectItem Value="@("GoogleGemini")">Google Gemini</MudSelectItem>
</MudSelect>

@if (Config.Provider == "AzureOpenAI")
{
    <MudTextField Label="Endpoint" @bind-Value="Config.Endpoint" Class="mb-3" Placeholder="https://your-resource.openai.azure.com" />
}

<MudTextField Label="API Key" @bind-Value="Config.ApiKey" InputType="InputType.Password" Class="mb-3" />

@code {
    [Parameter]
    public ApiConfig Config { get; set; } = new();
}

```

## File: Components\ChatLog.razor
```
@using AiAgileTeam.Models
@using Markdig
@inject IJSRuntime JSRuntime

<div class="chat-container">
    <MudPaper @ref="_scrollContainer" Elevation="0" Class="pa-4 d-flex flex-column chat-log-container" Style="height: 60vh; overflow-y: auto;">
        @foreach (var message in Messages)
        {
            <div class="@(message.IsUser ? "d-flex justify-end mb-4" : "d-flex justify-start mb-4")">
                <MudCard Class="@(message.IsUser ? "mud-theme-primary pa-3" : "mud-theme-dark pa-3")" Style="max-width: 80%;">
                    <MudText Typo="Typo.subtitle2" Class="mb-1"><strong>@message.Author</strong></MudText>
                    <div class="markdown-body">
                        @((MarkupString)Markdown.ToHtml(message.Content ?? "", _pipeline))
                    </div>
                </MudCard>
            </div>
        }
    </MudPaper>
</div>

<style>
    .chat-log-container {
        scroll-behavior: smooth;
    }
    .markdown-body {
        font-family: inherit;
        line-height: 1.5;
    }
    .markdown-body p:last-child {
        margin-bottom: 0;
    }
    .markdown-body ul, .markdown-body ol {
        padding-left: 1.5rem;
        margin-bottom: 1rem;
    }
    .markdown-body pre {
        background-color: rgba(0,0,0,0.2);
        padding: 0.5rem;
        border-radius: 4px;
        overflow-x: auto;
    }
    .markdown-body code {
        font-family: monospace;
        background-color: rgba(0,0,0,0.1);
        padding: 0.1rem 0.3rem;
        border-radius: 3px;
    }
</style>

@code {
    [Parameter]
    public List<ChatMessage> Messages { get; set; } = new();

    private MudPaper? _scrollContainer;
    private MarkdownPipeline _pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await ScrollToBottom();
    }

    private async Task ScrollToBottom()
    {
        try
        {
            await JSRuntime.InvokeVoidAsync("eval", "const el = document.querySelector('.chat-log-container'); if (el) el.scrollTop = el.scrollHeight;");
        }
        catch { }
    }
}

```

## File: Components\EditAgentModal.razor
```
@using AiAgileTeam.Models

<MudDialog>
    <DialogContent>
        <MudTextField Label="Agent Name" @bind-Value="_agent.DisplayName" Class="mb-3" />
        <MudTextField Label="System Prompt" @bind-Value="_agent.SystemPrompt" Lines="6" Class="mb-3" />

        @if (_agent.IsBuiltIn)
        {
            <MudCheckBox T="bool" @bind-Value="_agent.UseDefaultPrompt" Color="Color.Primary" Class="mb-2">
                Use default system prompt
            </MudCheckBox>

            @if (_agent.UseDefaultPrompt)
            {
                <MudAlert Severity="Severity.Info" Dense="true">Built-in system prompt will be used for this agent at runtime.</MudAlert>
            }
        }
    </DialogContent>
    <DialogActions>
        <MudButton OnClick="Cancel">Cancel</MudButton>
        <MudButton Color="Color.Primary" OnClick="Submit">Save</MudButton>
    </DialogActions>
</MudDialog>

@code {
    [CascadingParameter]
    MudBlazor.IMudDialogInstance? MudDialog { get; set; }

    [Parameter]
    public AgentConfig Agent { get; set; } = new();

    private AgentConfig _agent = new();

    protected override void OnParametersSet()
    {
        _agent = new AgentConfig
        {
            Id = Agent.Id,
            DisplayName = Agent.DisplayName,
            Role = Agent.Role,
            SystemPrompt = Agent.SystemPrompt,
            ApiSettings = Agent.ApiSettings,
            ModelSettings = Agent.ModelSettings,
            IsSelected = Agent.IsSelected,
            IsMandatory = Agent.IsMandatory,
            IsBuiltIn = Agent.IsBuiltIn,
            UseDefaultPrompt = Agent.UseDefaultPrompt,
            Name = Agent.Name,
            ProviderSettings = Agent.ProviderSettings,
            MaxTokensPerResponse = Agent.MaxTokensPerResponse,
            MaxRoundsPerSession = Agent.MaxRoundsPerSession
        };
    }

    private void Submit()
    {
        if (string.IsNullOrWhiteSpace(_agent.DisplayName))
        {
            return;
        }

        MudDialog?.Close(DialogResult.Ok(_agent));
    }

    private void Cancel() => MudDialog?.Cancel();
}

```

## File: Components\ModelSettingsEditor.razor
```
@using AiAgileTeam.Models

<MudButton Variant="Variant.Outlined" Color="Color.Primary" OnClick="@(() => OnLoadModels.InvokeAsync(ApiConfig))" Disabled="IsLoading" Class="mb-3">
    @if (IsLoading)
    {
        <MudProgressCircular Class="ms-n1" Size="Size.Small" Indeterminate="true" />
        <MudText Class="ms-2">Loading...</MudText>
    }
    else
    {
        <MudText>Load / Refresh Models</MudText>
    }
</MudButton>

@if (Models.Any() || !string.IsNullOrWhiteSpace(Config.Model))
{
    <MudSelect T="string" Label="Model" @bind-Value="Config.Model" Class="mb-3">
        @if (!Models.Contains(Config.Model) && !string.IsNullOrWhiteSpace(Config.Model))
        {
            <MudSelectItem Value="@Config.Model">@Config.Model</MudSelectItem>
        }
        @foreach (var model in Models)
        {
            <MudSelectItem Value="@model">@model</MudSelectItem>
        }
    </MudSelect>
}

<div class="d-flex gap-4 mt-4">
    <MudNumericField @bind-Value="Config.MaxTokensPerResponse" Label="Max Tokens" Min="100" Max="32000" Variant="Variant.Outlined" />
    <MudNumericField @bind-Value="Config.MaxRoundsPerSession" Label="Max Rounds" Min="1" Max="10" Variant="Variant.Outlined" />
</div>

@code {
    [Parameter]
    public ModelConfig Config { get; set; } = new();
    
    [Parameter]
    public ApiConfig ApiConfig { get; set; } = new();
    
    [Parameter]
    public EventCallback<ApiConfig> OnLoadModels { get; set; }
    
    [Parameter]
    public List<string> Models { get; set; } = new();
    
    [Parameter]
    public bool IsLoading { get; set; }
}

```

## File: Components\ProviderSettingsEditor.razor
```
@using AiAgileTeam.Models

<MudSelect T="string" Label="Provider" @bind-Value="Config.Provider" Class="mb-3">
    <MudSelectItem Value="@("OpenAI")">OpenAI</MudSelectItem>
    <MudSelectItem Value="@("AzureOpenAI")">Azure OpenAI</MudSelectItem>
    <MudSelectItem Value="@("GoogleGemini")">Google Gemini</MudSelectItem>
</MudSelect>

@if (Config.Provider == "AzureOpenAI")
{
    <MudTextField Label="Endpoint" @bind-Value="Config.Endpoint" Class="mb-3" />
}

<MudTextField Label="API Key" @bind-Value="Config.ApiKey" InputType="InputType.Password" Class="mb-3" />

<MudButton Variant="Variant.Outlined" Color="Color.Primary" OnClick="@(() => OnLoadModels.InvokeAsync(Config))" Disabled="IsLoading" Class="mb-3">
    @if (IsLoading)
    {
        <MudProgressCircular Class="ms-n1" Size="Size.Small" Indeterminate="true" />
        <MudText Class="ms-2">Loading...</MudText>
    }
    else
    {
        <MudText>Load / Refresh Models</MudText>
    }
</MudButton>

@if (Models.Any() || !string.IsNullOrWhiteSpace(Config.Model))
{
    <MudSelect T="string" Label="Model" @bind-Value="Config.Model" Class="mb-3">
        @if (!Models.Contains(Config.Model) && !string.IsNullOrWhiteSpace(Config.Model))
        {
            <MudSelectItem Value="@Config.Model">@Config.Model</MudSelectItem>
        }
        @foreach (var model in Models)
        {
            <MudSelectItem Value="@model">@model</MudSelectItem>
        }
    </MudSelect>
}

@code {
    [Parameter]
    public ProviderConfig Config { get; set; } = new();

    [Parameter]
    public EventCallback<ProviderConfig> OnLoadModels { get; set; }

    [Parameter]
    public List<string> Models { get; set; } = new();

    [Parameter]
    public bool IsLoading { get; set; }
}

```

## File: Components\SessionSummaryPanel.razor
```
@using AiAgileTeam.Models

<MudPaper Elevation="1" Class="pa-3 summary-panel">
    <MudText Typo="Typo.subtitle1" Class="mb-2" Style="font-weight: 600;">
        <MudIcon Icon="@Icons.Material.Filled.Summarize" Size="Size.Small" Class="mr-1" Style="vertical-align: middle;" />
        Ход сессии
    </MudText>

    @if (Phases is { Count: > 0 })
    {
        <MudTimeline TimelinePosition="TimelinePosition.Start" Class="summary-timeline">
            @foreach (var phase in Phases)
            {
                <MudTimelineItem Color="@GetPhaseColor(phase.Status)"
                                 Size="Size.Small"
                                 Variant="@GetPhaseVariant(phase.Status)">
                    <ItemContent>
                        <div class="d-flex flex-column">
                            <MudText Typo="Typo.body2" Style="@GetPhaseTitleStyle(phase.Status)">
                                @phase.Title
                            </MudText>
                            @if (!string.IsNullOrWhiteSpace(phase.Description))
                            {
                                <MudText Typo="Typo.caption" Style="opacity: 0.7;">
                                    @phase.Description
                                </MudText>
                            }
                            @if (phase.Status == SessionPhaseStatus.InProgress)
                            {
                                <MudProgressLinear Color="Color.Primary" Indeterminate="true"
                                                   Size="Size.Small" Class="mt-1" Style="max-width: 120px;" />
                            }
                        </div>
                    </ItemContent>
                </MudTimelineItem>
            }
        </MudTimeline>
    }
    else
    {
        <MudText Typo="Typo.caption" Style="opacity: 0.5;">Ожидание начала...</MudText>
    }
</MudPaper>

@code {
    [Parameter]
    public List<SessionPhase> Phases { get; set; } = [];

    private static Color GetPhaseColor(SessionPhaseStatus status) => status switch
    {
        SessionPhaseStatus.Completed => Color.Success,
        SessionPhaseStatus.InProgress => Color.Primary,
        _ => Color.Default
    };

    private static Variant GetPhaseVariant(SessionPhaseStatus status) => status switch
    {
        SessionPhaseStatus.Completed => Variant.Filled,
        SessionPhaseStatus.InProgress => Variant.Filled,
        _ => Variant.Outlined
    };

    private static string GetPhaseTitleStyle(SessionPhaseStatus status) => status switch
    {
        SessionPhaseStatus.InProgress => "font-weight: 600;",
        SessionPhaseStatus.Completed => "opacity: 0.7; text-decoration: line-through;",
        _ => "opacity: 0.5;"
    };
}

```

## File: Components\SessionSummaryPanel.razor.css
```css
.summary-panel {
    height: 100%;
    overflow-y: auto;
}

::deep .summary-timeline {
    padding: 0;
}

::deep .summary-timeline .mud-timeline-item {
    padding-block: 2px;
}

```

## File: Layout\MainLayout.razor
```
@inherits LayoutComponentBase
@implements IDisposable
@inject ApiHealthService ApiHealthService
@using AiAgileTeam.Services

<MudThemeProvider IsDarkMode="true" />
<MudDialogProvider />
<MudSnackbarProvider />
<MudPopoverProvider />

<MudLayout Class="main-layout">
    <MudAppBar Elevation="1">
        <MudIconButton Icon="@Icons.Material.Filled.Menu" Color="Color.Inherit" Edge="Edge.Start" OnClick="@((e) => DrawerToggle())" />
        <MudText Typo="Typo.h5" Class="ml-3">AI Agile Team</MudText>
        <MudSpacer />
        <div class="api-status-wrapper">
            <MudIcon Icon="@ApiStatusIcon" Color="@ApiStatusColor" Size="Size.Small" />
            <MudText Typo="Typo.body2" Class="api-status-text">@ApiStatusText</MudText>
        </div>
        <MudIconButton Icon="@Icons.Material.Filled.Settings" Color="Color.Inherit" Href="/settings" />
    </MudAppBar>
    <MudDrawer @bind-Open="_drawerOpen" ClipMode="DrawerClipMode.Always" Elevation="2">
        <NavMenu />
    </MudDrawer>
    <MudMainContent Class="main-content-area">
        <MudContainer MaxWidth="MaxWidth.ExtraLarge" Class="pt-4 main-content-container">
            @Body
        </MudContainer>
    </MudMainContent>
</MudLayout>

@code {
    bool _drawerOpen = true;
    bool _hasInitialHealthCheck;
    CancellationTokenSource? _healthCheckCancellationTokenSource;

    void DrawerToggle()
    {
        _drawerOpen = !_drawerOpen;
    }

    protected override void OnInitialized()
    {
        ApiHealthService.StatusChanged += OnApiHealthStatusChanged;
        _healthCheckCancellationTokenSource = new CancellationTokenSource();
        _ = MonitorApiHealthAsync(_healthCheckCancellationTokenSource.Token);
    }

    private async Task MonitorApiHealthAsync(CancellationToken cancellationToken)
    {
        try
        {
            await ApiHealthService.CheckAsync(cancellationToken);
            _hasInitialHealthCheck = true;
            await InvokeAsync(StateHasChanged);

            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await ApiHealthService.CheckAsync(cancellationToken);
                await InvokeAsync(StateHasChanged);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void OnApiHealthStatusChanged()
    {
        _ = InvokeAsync(StateHasChanged);
    }

    private string ApiStatusText => !_hasInitialHealthCheck
        ? "API: checking..."
        : ApiHealthService.IsApiAvailable ? "API: online" : "API: offline";

    private string ApiStatusIcon => !_hasInitialHealthCheck
        ? Icons.Material.Filled.Sync
        : ApiHealthService.IsApiAvailable ? Icons.Material.Filled.CloudDone : Icons.Material.Filled.CloudOff;

    private Color ApiStatusColor => !_hasInitialHealthCheck
        ? Color.Warning
        : ApiHealthService.IsApiAvailable ? Color.Success : Color.Error;

    public void Dispose()
    {
        ApiHealthService.StatusChanged -= OnApiHealthStatusChanged;

        if (_healthCheckCancellationTokenSource is null)
        {
            return;
        }

        _healthCheckCancellationTokenSource.Cancel();
        _healthCheckCancellationTokenSource.Dispose();
        _healthCheckCancellationTokenSource = null;
    }
}

```

## File: Layout\MainLayout.razor.css
```css
.page {
    position: relative;
    display: flex;
    flex-direction: column;
}

.api-status-wrapper {
    display: flex;
    align-items: center;
    gap: 0.35rem;
    margin-right: 0.75rem;
}

.api-status-text {
    margin: 0;
}

.main-layout {
    display: flex;
    flex-direction: column;
    min-height: 100vh;
}

.main-layout ::deep .mud-app-bar {
    flex-shrink: 0;
}

.main-layout ::deep .mud-drawer-content {
    flex-shrink: 0;
}

.main-layout ::deep .mud-main-content {
    display: flex;
    flex-direction: column;
    flex: 1;
    overflow: auto;
}

.main-content-area {
    display: flex;
    flex-direction: column;
    flex: 1;
}

.main-content-container {
    display: flex;
    flex-direction: column;
    flex: 1;
    height: 100%;
}

main {
    flex: 1;
}

.sidebar {
    background-image: linear-gradient(180deg, rgb(5, 39, 103) 0%, #3a0647 70%);
}

.top-row {
    background-color: #f7f7f7;
    border-bottom: 1px solid #d6d5d5;
    justify-content: flex-end;
    height: 3.5rem;
    display: flex;
    align-items: center;
}

    .top-row ::deep a, .top-row ::deep .btn-link {
        white-space: nowrap;
        margin-left: 1.5rem;
        text-decoration: none;
    }

    .top-row ::deep a:hover, .top-row ::deep .btn-link:hover {
        text-decoration: underline;
    }

    .top-row ::deep a:first-child {
        overflow: hidden;
        text-overflow: ellipsis;
    }

@media (max-width: 640.98px) {
    .top-row {
        justify-content: space-between;
    }

    .top-row ::deep a, .top-row ::deep .btn-link {
        margin-left: 0;
    }
}

@media (min-width: 641px) {
    .page {
        flex-direction: row;
    }

    .sidebar {
        width: 250px;
        height: 100vh;
        position: sticky;
        top: 0;
    }

    .top-row {
        position: sticky;
        top: 0;
        z-index: 1;
    }

    .top-row.auth ::deep a:first-child {
        flex: 1;
        text-align: right;
        width: 0;
    }

    .top-row, article {
        padding-left: 2rem !important;
        padding-right: 1.5rem !important;
    }
}

```

## File: Layout\NavMenu.razor
```
<MudNavMenu>
    <MudNavLink Href="/" Match="NavLinkMatch.All" Icon="@Icons.Material.Filled.Home">Home</MudNavLink>
    <MudNavLink Href="/history" Match="NavLinkMatch.Prefix" Icon="@Icons.Material.Filled.History">History</MudNavLink>
    <MudNavLink Href="/settings" Match="NavLinkMatch.Prefix" Icon="@Icons.Material.Filled.Settings">Settings</MudNavLink>
</MudNavMenu>

```

## File: Layout\NavMenu.razor.css
```css
.navbar-toggler {
    background-color: rgba(255, 255, 255, 0.1);
}

.top-row {
    min-height: 3.5rem;
    background-color: rgba(0,0,0,0.4);
}

.navbar-brand {
    font-size: 1.1rem;
}

.bi {
    display: inline-block;
    position: relative;
    width: 1.25rem;
    height: 1.25rem;
    margin-right: 0.75rem;
    top: -1px;
    background-size: cover;
}

.bi-house-door-fill-nav-menu {
    background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='16' height='16' fill='white' class='bi bi-house-door-fill' viewBox='0 0 16 16'%3E%3Cpath d='M6.5 14.5v-3.505c0-.245.25-.495.5-.495h2c.25 0 .5.25.5.5v3.5a.5.5 0 0 0 .5.5h4a.5.5 0 0 0 .5-.5v-7a.5.5 0 0 0-.146-.354L13 5.793V2.5a.5.5 0 0 0-.5-.5h-1a.5.5 0 0 0-.5.5v1.293L8.354 1.146a.5.5 0 0 0-.708 0l-6 6A.5.5 0 0 0 1.5 7.5v7a.5.5 0 0 0 .5.5h4a.5.5 0 0 0 .5-.5Z'/%3E%3C/svg%3E");
}

.bi-plus-square-fill-nav-menu {
    background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='16' height='16' fill='white' class='bi bi-plus-square-fill' viewBox='0 0 16 16'%3E%3Cpath d='M2 0a2 2 0 0 0-2 2v12a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V2a2 2 0 0 0-2-2H2zm6.5 4.5v3h3a.5.5 0 0 1 0 1h-3v3a.5.5 0 0 1-1 0v-3h-3a.5.5 0 0 1 0-1h3v-3a.5.5 0 0 1 1 0z'/%3E%3C/svg%3E");
}

.bi-list-nested-nav-menu {
    background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='16' height='16' fill='white' class='bi bi-list-nested' viewBox='0 0 16 16'%3E%3Cpath fill-rule='evenodd' d='M4.5 11.5A.5.5 0 0 1 5 11h10a.5.5 0 0 1 0 1H5a.5.5 0 0 1-.5-.5zm-2-4A.5.5 0 0 1 3 7h10a.5.5 0 0 1 0 1H3a.5.5 0 0 1-.5-.5zm-2-4A.5.5 0 0 1 1 3h10a.5.5 0 0 1 0 1H1a.5.5 0 0 1-.5-.5z'/%3E%3C/svg%3E");
}

.nav-item {
    font-size: 0.9rem;
    padding-bottom: 0.5rem;
}

    .nav-item:first-of-type {
        padding-top: 1rem;
    }

    .nav-item:last-of-type {
        padding-bottom: 1rem;
    }

    .nav-item ::deep a {
        color: #d7d7d7;
        border-radius: 4px;
        height: 3rem;
        display: flex;
        align-items: center;
        line-height: 3rem;
    }

.nav-item ::deep a.active {
    background-color: rgba(255,255,255,0.37);
    color: white;
}

.nav-item ::deep a:hover {
    background-color: rgba(255,255,255,0.1);
    color: white;
}

@media (min-width: 641px) {
    .navbar-toggler {
        display: none;
    }

    .collapse {
        /* Never collapse the sidebar for wide screens */
        display: block;
    }

    .nav-scrollable {
        /* Allow sidebar to scroll for tall menus */
        height: calc(100vh - 3.5rem);
        overflow-y: auto;
    }
}

```

## File: Pages\History.razor
```
@page "/history"
@using AiAgileTeam.Models
@using AiAgileTeam.Services
@inject ChatSessionService SessionService
@inject NavigationManager NavigationManager

<MudText Typo="Typo.h4" GutterBottom="true">Chat History</MudText>

@if (_sessions == null)
{
    <MudProgressCircular Color="Color.Primary" Indeterminate="true" />
}
else if (!_sessions.Any())
{
    <MudText Typo="Typo.body1">No sessions found.</MudText>
}
else
{
    <MudTable Items="@_sessions" Hover="true" Breakpoint="Breakpoint.Sm">
        <HeaderContent>
            <MudTh>Title</MudTh>
            <MudTh>Created At</MudTh>
            <MudTh>Updated At</MudTh>
            <MudTh>Actions</MudTh>
        </HeaderContent>
        <RowTemplate>
            <MudTd DataLabel="Title">@context.Title</MudTd>
            <MudTd DataLabel="Created At">@context.CreatedAt.ToLocalTime().ToString("g")</MudTd>
            <MudTd DataLabel="Updated At">@context.UpdatedAt.ToLocalTime().ToString("g")</MudTd>
            <MudTd DataLabel="Actions">
                <MudButton Variant="Variant.Filled" Color="Color.Primary" Size="Size.Small" OnClick="@(() => OpenSession(context.Id))">Open</MudButton>
                <MudButton Variant="Variant.Filled" Color="Color.Error" Size="Size.Small" OnClick="@(() => DeleteSession(context.Id))" Class="ml-2">Delete</MudButton>
            </MudTd>
        </RowTemplate>
    </MudTable>
}

@code {
    private List<ChatSession> _sessions = new();

    protected override async Task OnInitializedAsync()
    {
        await LoadSessions();
    }

    private async Task LoadSessions()
    {
        _sessions = await SessionService.GetAllSessionsAsync();
    }

    private void OpenSession(string id)
    {
        NavigationManager.NavigateTo($"/session/{id}");
    }

    private async Task DeleteSession(string id)
    {
        await SessionService.DeleteSessionAsync(id);
        await LoadSessions();
    }
}

```

## File: Pages\Index.razor
```
@page "/"
@using AiAgileTeam.Models
@inject SettingsService SettingsService
@inject NavigationManager NavigationManager
@inject IDialogService DialogService
@inject ISnackbar Snackbar

<MudText Typo="Typo.h4" GutterBottom="true">AI Agile Team Session Setup</MudText>

@if (_settings == null)
{
    <MudProgressCircular Color="Color.Primary" Indeterminate="true" />
}
else
{
    <MudCard Class="mb-4">
        <MudCardContent>
            <MudTable Items="@_settings.Agents" Hover="true" Breakpoint="Breakpoint.Sm">
                <ToolBarContent>
                    <MudText Typo="Typo.h6">Team Members</MudText>
                    <MudSpacer />
                    <MudButton Variant="Variant.Filled" Color="Color.Primary" OnClick="OpenAddAgentDialog">Add Agent</MudButton>
                </ToolBarContent>
                <HeaderContent>
                    <MudTh>Include</MudTh>
                    <MudTh>Name</MudTh>
                    <MudTh>Role</MudTh>
                    <MudTh>Actions</MudTh>
                </HeaderContent>
                <RowTemplate>
                    <MudTd DataLabel="Include">
                        <MudCheckBox T="bool" @bind-Value="@context.IsSelected" Disabled="@context.IsMandatory" Color="Color.Primary" />
                    </MudTd>
                    <MudTd DataLabel="Name">@context.DisplayName</MudTd>
                    <MudTd DataLabel="Role">@context.Role</MudTd>
                    <MudTd DataLabel="Actions">
                        <MudButton Variant="Variant.Text" Color="Color.Primary" Size="Size.Small" OnClick="@(() => OpenEditAgentDialog(context))">
                            Edit
                        </MudButton>
                    </MudTd>
                </RowTemplate>
            </MudTable>
        </MudCardContent>
    </MudCard>

    <MudCard Class="mb-4">
        <MudCardContent>
            <MudTextField Label="Your Query / Task" @bind-Value="_query" Lines="4" Required="true" RequiredError="Query is required" />
            
            <MudCheckBox T="bool" @bind-Value="_enableClarification" Color="Color.Primary" Class="mt-3">
                Enable Clarification Agent
            </MudCheckBox>
        </MudCardContent>
        <MudCardActions>
            <MudButton Variant="Variant.Filled" Color="Color.Success" OnClick="StartSession" Disabled="@string.IsNullOrWhiteSpace(_query)">
                Start Session
            </MudButton>
        </MudCardActions>
    </MudCard>
}

@code {
    private AppSettings _settings = new();
    private string _query = "";
    private bool _enableClarification = false;

    protected override async Task OnInitializedAsync()
    {
        _settings = await SettingsService.LoadSettingsAsync();
    }

    private async Task OpenAddAgentDialog()
    {
        var options = new DialogOptions { CloseOnEscapeKey = true, MaxWidth = MaxWidth.Small, FullWidth = true };
        var dialog = await DialogService.ShowAsync<AddAgentModal>("Add Custom Agent", options);
        var result = await dialog.Result;

        if (result != null && !result.Canceled)
        {
            if (result.Data is AgentConfig newAgent)
            {
                _settings.Agents.Add(newAgent);
                await SettingsService.SaveSettingsAsync(_settings);
                StateHasChanged();
            }
        }
    }

    private async Task OpenEditAgentDialog(AgentConfig agent)
    {
        var options = new DialogOptions { CloseOnEscapeKey = true, MaxWidth = MaxWidth.Medium, FullWidth = true };
        var parameters = new DialogParameters
        {
            ["Agent"] = agent
        };

        var dialog = await DialogService.ShowAsync<EditAgentModal>($"Edit Agent: {agent.Role}", parameters, options);
        var result = await dialog.Result;

        if (result != null && !result.Canceled && result.Data is AgentConfig editedAgent)
        {
            var existingAgent = _settings.Agents.FirstOrDefault(a => a.Id == editedAgent.Id);
            if (existingAgent == null)
            {
                return;
            }

            existingAgent.DisplayName = editedAgent.DisplayName;
            existingAgent.SystemPrompt = editedAgent.SystemPrompt;
            existingAgent.UseDefaultPrompt = editedAgent.UseDefaultPrompt;

            await SettingsService.SaveSettingsAsync(_settings);
            StateHasChanged();
        }
    }

    private async Task StartSession()
    {
        // Simple validation of settings before starting session
        bool keyMissing = false;
        if (_settings.Mode == "global")
        {
            if (string.IsNullOrWhiteSpace(_settings.Global?.ApiKey)) keyMissing = true;
        }
        else
        {
            var selectedAgents = _settings.Agents.Where(a => a.IsSelected);
            if (!selectedAgents.Any() || selectedAgents.Any(a => string.IsNullOrWhiteSpace(a.ProviderSettings?.ApiKey)))
            {
                keyMissing = true;
            }
        }

        if (keyMissing)
        {
            Snackbar.Add("API Keys are missing. Please configure them in Settings.", Severity.Error);
            NavigationManager.NavigateTo("/settings");
            return;
        }

        await SettingsService.SaveSettingsAsync(_settings);

        var uri = NavigationManager.GetUriWithQueryParameters("/session", new Dictionary<string, object?> {
            { "query", _query },
            { "clarify", _enableClarification }
        });
        
        NavigationManager.NavigateTo(uri);
    }
}

```

## File: Pages\Session.razor
```
@page "/session"
@page "/session/{SessionId}"
@using System.Net.Http.Json
@using AiAgileTeam.Models
@using AiAgileTeam.Services
@inject SettingsService SettingsService
@inject ChatSessionService SessionService
@inject NavigationManager NavigationManager
@inject IHttpClientFactory HttpClientFactory
@inject IJSRuntime JSRuntime
@using Microsoft.AspNetCore.Components.WebAssembly.Http

<MudGrid>
    <MudItem xs="12" sm="4" md="3">
        <MudPaper Elevation="1" Class="pa-4 flex-column" Style="height: 100%;">
            <MudText Typo="Typo.h6" GutterBottom="true">Ai Team</MudText>
            <MudText Typo="Typo.body2" Class="mb-4">Toggle agents in real-time</MudText>
            
            @if (_settings?.Agents != null)
            {
                <div class="d-flex flex-column gap-1">
                    @foreach (var agent in _settings.Agents)
                    {
                        <div class="agent-toggle-item @(IsActiveAgent(agent) ? "agent-toggle-item--active" : string.Empty)">
                            <MudSwitch Value="agent.IsSelected" ValueChanged="@((bool val) => ToggleAgentAsync(agent, val))" Disabled="@agent.IsMandatory" Color="Color.Primary" Label="@GetAgentToggleLabel(agent)" T="bool" />
                        </div>
                    }
                </div>
            }
        </MudPaper>
    </MudItem>
    <MudItem xs="12" sm="8" md="9">
        <div class="session-content-row">
            <div class="session-chat-area">
                <MudText Typo="Typo.h5" GutterBottom="true">Session</MudText>

                @if (_isInitializing)
                {
                    <MudProgressCircular Color="Color.Primary" Indeterminate="true" />
                }
                else
                {
                    <ChatLog Messages="_messages" />

                    @if (!_isSessionComplete)
                    {
                        <MudCard Class="mt-4">
                            <MudCardContent>
                                <MudTextField @bind-Value="_userInput" Label="Reply..." Lines="3" />
                                <div class="d-flex align-center mt-2 gap-2">
                                    <MudButton Variant="Variant.Filled" Color="Color.Primary" OnClick="SendMessage" Disabled="_isProcessing">
                                        Send
                                    </MudButton>
                                    @if (_isProcessing)
                                    {
                                        <MudButton Variant="Variant.Filled" Color="Color.Error" OnClick="StopProcessing">
                                            Stop
                                        </MudButton>
                                    }
                                </div>
                            </MudCardContent>
                        </MudCard>
                    }
                    else
                    {
                        <MudCard Class="mt-4" Style="border: 2px solid var(--mud-palette-success);">
                            <MudCardContent>
                                <div class="d-flex align-center justify-space-between">
                                    <div>
                                        <MudText Typo="Typo.h6">Session Complete</MudText>
                                        <MudText Typo="Typo.body2">The virtual team has finished their work. You can download the full report below.</MudText>
                                        @if (!string.IsNullOrWhiteSpace(_reportStatusMessage))
                                        {
                                            <MudText Typo="Typo.body2" Class="mt-2">@_reportStatusMessage</MudText>
                                        }
                                    </div>
                                    <MudButton Variant="Variant.Filled"
                                               Color="Color.Secondary"
                                               OnClick="DownloadReportAsync"
                                               StartIcon="@Icons.Material.Filled.Download"
                                               Size="Size.Large"
                                               Disabled="@_isGeneratingReport">
                                        @(_isGeneratingReport ? "Generating report..." : "Download Report (.pdf)")
                                    </MudButton>
                                </div>
                                @if (_isGeneratingReport)
                                {
                                    <MudProgressLinear Class="mt-3" Color="Color.Secondary" Indeterminate="true" />
                                }
                            </MudCardContent>
                        </MudCard>
                    }
                }
            </div>
            <div class="session-summary-wrapper">
                <SessionSummaryPanel Phases="_phases" />
            </div>
        </div>
    </MudItem>
</MudGrid>

@code {
    [Parameter]
    public string? SessionId { get; set; }

    [SupplyParameterFromQuery]
    public string Query { get; set; } = "";

    [SupplyParameterFromQuery]
    public bool Clarify { get; set; }

    private ChatSession _currentSession = new();
    private AppSettings _settings = new();
    private List<ChatMessage> _messages = new();
    private string _userInput = "";
    private bool _isInitializing = true;
    private bool _isProcessing = false;
    private bool _isSessionComplete = false;
    private bool _isGeneratingReport = false;
    private bool _clarificationPhase = false;
    private int _clarificationQuestionsAsked = 0;
    private CancellationTokenSource? _cts;
    private string? _serverSessionId;
    private string? _reportStatusMessage;
    private bool _startNewServerSessionOnNextMessage;
    private string? _activeAgentName;
    private readonly List<SessionPhase> _phases = [];

    private async Task ToggleAgentAsync(AgentConfig agent, bool val)
    {
        agent.IsSelected = val;
        await SettingsService.SaveSettingsAsync(_settings);

        // Existing server session is configured only once.
        // Mark next user request to start a fresh server session with full history,
        // without interrupting current streaming output.
        _startNewServerSessionOnNextMessage = true;

        _messages.Add(new ChatMessage
        {
            Author = "System",
            Content = $"Team composition updated: {(val ? "enabled" : "disabled")} '{GetAgentToggleLabel(agent)}'. Changes will apply from the next turn.",
            IsUser = false
        });

        if (_currentSession != null)
        {
            _currentSession.SettingsSnapshot = _settings;
            await SaveCurrentSessionAsync();
        }

        StateHasChanged();
    }

    private static string GetAgentToggleLabel(AgentConfig agent)
    {
        if (string.IsNullOrWhiteSpace(agent.Role))
            return agent.DisplayName;

        if (string.IsNullOrWhiteSpace(agent.DisplayName))
            return agent.Role;

        return $"{agent.Role} — {agent.DisplayName}";
    }

    private bool IsActiveAgent(AgentConfig agent)
    {
        if (string.IsNullOrWhiteSpace(_activeAgentName))
        {
            return false;
        }

        return string.Equals(agent.DisplayName, _activeAgentName, StringComparison.OrdinalIgnoreCase)
               || string.Equals(agent.Role, _activeAgentName, StringComparison.OrdinalIgnoreCase);
    }

    private void StopProcessing()
    {
        try
        {
            if (_cts != null && !_cts.IsCancellationRequested)
            {
                _cts.Cancel();
            }
        }
        catch { }
    }

    protected override async Task OnInitializedAsync()
    {
        if (!string.IsNullOrEmpty(SessionId))
        {
            var session = await SessionService.GetSessionAsync(SessionId);
            if (session != null)
            {
                _currentSession = session;
                _settings = _currentSession.SettingsSnapshot;
                _messages = _currentSession.Messages;
                _isInitializing = false;
                _clarificationPhase = false;
                _isSessionComplete = false;
                return;
            }
        }

        _settings = await SettingsService.LoadSettingsAsync();
        
        if (string.IsNullOrWhiteSpace(Query))
        {
            NavigationManager.NavigateTo("/");
            return;
        }

        _currentSession = new ChatSession
        {
            Title = Query.Length > 30 ? Query.Substring(0, 30) + "..." : Query,
            SettingsSnapshot = _settings,
            Messages = _messages
        };

        await InitializeSessionAsync();
    }

    private async Task SaveCurrentSessionAsync()
    {
        if (_currentSession != null)
        {
            _currentSession.UpdatedAt = DateTime.UtcNow;
            await SessionService.SaveSessionAsync(_currentSession);
        }
    }

    private async Task ProcessApiStream(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        response.EnsureSuccessStatusCode();
        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        string currentAuthor = "";
        ChatMessage? uiMessage = null;
        bool lastMessageWasComplete = false;
        int messageCount = 0;

        try
        {
            await foreach (var message in System.Text.Json.JsonSerializer.DeserializeAsyncEnumerable<StreamingMessageDto>(stream, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken))
            {
                if (message == null) continue;
                
                messageCount++;
                Console.WriteLine($"[Session.razor] Received message #{messageCount}: Author='{message.Author}', ContentPiece='{message.ContentPiece?.Substring(0, Math.Min(50, message.ContentPiece?.Length ?? 0))}', IsComplete={message.IsComplete}");

                if (!string.IsNullOrEmpty(message.ServerSessionId))
                {
                    _serverSessionId = message.ServerSessionId;
                }

                // Handle completion signal from previous agent
                if (message.IsComplete && !string.IsNullOrEmpty(message.Author) && message.Author != "System")
                {
                    Console.WriteLine($"[Session.razor] Agent '{message.Author}' completed their turn");
                    lastMessageWasComplete = true;
                    CompleteAgentPhase(message.Author);
                    if (string.Equals(_activeAgentName, message.Author, StringComparison.OrdinalIgnoreCase))
                    {
                        _activeAgentName = null;
                    }
                    await SaveCurrentSessionAsync();

                    // Check for [DONE] in the last message from this agent
                    var lastMessage = _messages.LastOrDefault(m => m.Author == message.Author);
                    if (lastMessage != null && lastMessage.Content.Contains("[DONE]"))
                    {
                        Console.WriteLine($"[Session.razor] [DONE] detected in last message from '{message.Author}'");
                        _isSessionComplete = true;
                        CompleteAllPhases();
                        AddPhase("Session completed", "Final report is ready");
                        MarkPhaseCompleted(_phases.Count - 1);
                        StateHasChanged();
                        return; // Stop processing - session is complete
                    }

                    // Transition to discussion when the agent signals [READY] (after at least 2 Q&A rounds)
                    // or as a safety net after 3 full Q&A rounds regardless of [READY].
                    // The counter is incremented at the end of ProcessApiStream, so at check time
                    // it reflects previously completed rounds, not the current one.
                    bool readySignalled = lastMessage?.Content.Contains("[READY]") == true && _clarificationQuestionsAsked >= 2;
                    bool maxRoundsReached = _clarificationQuestionsAsked >= 3;
                    if (_clarificationPhase && (readySignalled || maxRoundsReached))
                    {
                        _clarificationPhase = false;
                        CompleteCurrentPhase();
                        SetPhaseInProgress();
                        _messages.Add(new ChatMessage { Author = "System", Content = "Clarification complete. Starting team discussion...", IsUser = false });
                        await SaveCurrentSessionAsync();
                        StateHasChanged();
                        await StartTeamDiscussionAsync();
                        return;
                    }

                    continue; // Skip further processing for completion messages
                }

                // Handle author change (including System messages about floor changes)
                if (currentAuthor != message.Author || uiMessage == null)
                {
                    // Finalize previous message if it wasn't already marked complete
                    if (uiMessage != null && !lastMessageWasComplete)
                    {
                        Console.WriteLine($"[Session.razor] Finalizing message for '{currentAuthor}' (length: {uiMessage.Content.Length})");
                        await SaveCurrentSessionAsync();
                    }

                    currentAuthor = message.Author;
                    uiMessage = new ChatMessage { Author = currentAuthor, Content = "", IsUser = false };
                    _messages.Add(uiMessage);
                    lastMessageWasComplete = false;

                    if (!string.IsNullOrWhiteSpace(currentAuthor) && currentAuthor != "System")
                    {
                        _activeAgentName = currentAuthor;
                        EnsureAgentPhase(currentAuthor);
                    }

                    Console.WriteLine($"[Session.razor] Switched to author '{currentAuthor}'");
                }

                if (!string.IsNullOrEmpty(message.ContentPiece) && AppendStreamingContent(uiMessage, message.ContentPiece))
                {
                    StateHasChanged();
                }
            }
        }
        catch (OperationCanceledException)
        {
            _activeAgentName = null;
            _messages.Add(new ChatMessage { Author = "System", Content = "[Stopped by user]", IsUser = false });
            StateHasChanged();
        }

        _activeAgentName = null;

        if (_clarificationPhase)
        {
             _clarificationQuestionsAsked++;
        }
        
        Console.WriteLine($"[Session.razor] Stream processing complete. Total messages: {messageCount}");
    }

    private static bool AppendStreamingContent(ChatMessage targetMessage, string incomingPiece)
    {
        if (string.IsNullOrEmpty(incomingPiece))
        {
            return false;
        }

        var existing = targetMessage.Content ?? string.Empty;
        if (string.IsNullOrEmpty(existing))
        {
            targetMessage.Content = incomingPiece;
            return true;
        }

        if (string.Equals(existing, incomingPiece, StringComparison.Ordinal))
        {
            return false;
        }

        if (incomingPiece.StartsWith(existing, StringComparison.Ordinal))
        {
            targetMessage.Content += incomingPiece[existing.Length..];
            return true;
        }

        if (existing.EndsWith(incomingPiece, StringComparison.Ordinal))
        {
            return false;
        }

        var maxOverlap = Math.Min(existing.Length, incomingPiece.Length);
        for (var overlap = maxOverlap; overlap > 0; overlap--)
        {
            if (existing.EndsWith(incomingPiece[..overlap], StringComparison.Ordinal))
            {
                targetMessage.Content += incomingPiece[overlap..];
                return true;
            }
        }

        targetMessage.Content += incomingPiece;
        return true;
    }

    private async Task InitializeSessionAsync()
    {
        try
        {
            _isProcessing = true;
            _messages.Add(new ChatMessage { Author = "User", Content = Query, IsUser = true });
            _isInitializing = false;
            await SaveCurrentSessionAsync();

            AddPhase("Request sent", Query.Length > 60 ? Query[..60] + "…" : Query);
            if (Clarify)
            {
                AddPhase("Clarification");
            }
            AddPhase(Clarify ? "Team discussion" : "Processing request");
            SetPhaseInProgress();
            StateHasChanged();
            
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            
            _clarificationPhase = Clarify;
            
            var request = new SessionRequest
            {
                Query = Query,
                Clarify = Clarify,
                Settings = _settings,
                History = new List<ChatMessageDto>()
            };

            using var httpClient = HttpClientFactory.CreateClient("ApiClient");
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, "api/aiteam/session")
            {
                Content = JsonContent.Create(request)
            };
            httpRequest.SetBrowserResponseStreamingEnabled(true);
            var response = await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, _cts.Token);

            
            await ProcessApiStream(response, _cts.Token);

            if (!Clarify) 
            {
                // Removed _isSessionComplete = true to keep the input visible
            }
        }
        catch (OperationCanceledException)
        {
            // Handled
        }
        catch (Exception ex)
        {
            _messages.Add(new ChatMessage { Author = "System", Content = $"Error initializing session: {ex.Message}", IsUser = false });
        }
        finally
        {
            await SaveCurrentSessionAsync();
            _isProcessing = false;
            _isInitializing = false;
            StateHasChanged();
        }
    }

    private async Task StartTeamDiscussionAsync()
    {
         // Always start a fresh server session for the discussion phase.
         // The clarification phase registers the Clarification Agent in the
         // group chat (via SK's InvokeAsync → EnsureAgent). Reusing that
         // session would let the PM orchestrator keep selecting it, producing
         // empty turns. A new session gets a clean agent pool.
         _serverSessionId = null;
         _startNewServerSessionOnNextMessage = false;

         var history = _messages
             .Where(m => !string.Equals(m.Author, "System", StringComparison.Ordinal))
             .Select(m => new ChatMessageDto { Author = m.Author, Content = m.Content, IsUser = m.IsUser })
             .ToList();

         var request = new SessionRequest
         {
             Query = "",
             Clarify = false,
             Settings = _settings,
             History = history,
             ServerSessionId = null
         };
         
         _cts?.Dispose();
         _cts = new CancellationTokenSource();
         
         using var httpClient = HttpClientFactory.CreateClient("ApiClient");
         var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"api/aiteam/message?isClarificationPhase=false")
         {
             Content = JsonContent.Create(request)
         };
         httpRequest.SetBrowserResponseStreamingEnabled(true);
         var response = await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, _cts.Token);
         await ProcessApiStream(response, _cts.Token);
         await SaveCurrentSessionAsync();
    }

    private async Task SendMessage()
    {
        if (string.IsNullOrWhiteSpace(_userInput)) return;

        var message = _userInput;
        _userInput = "";
        _isProcessing = true;
        
        var mustStartNewSession = _startNewServerSessionOnNextMessage || string.IsNullOrEmpty(_serverSessionId);
        var historyBeforeCurrent = mustStartNewSession
            ? _messages.Select(m => new ChatMessageDto { Author = m.Author, Content = m.Content, IsUser = m.IsUser }).ToList()
            : new List<ChatMessageDto>();
        
        _messages.Add(new ChatMessage { Author = "User", Content = message, IsUser = true });
        await SaveCurrentSessionAsync();
        StateHasChanged();

        try
        {
            _cts?.Dispose();
            _cts = new CancellationTokenSource();

            var request = new SessionRequest
            {
                Query = message,
                Settings = _settings,
                History = historyBeforeCurrent,
                ServerSessionId = mustStartNewSession ? null : _serverSessionId
            };

            _startNewServerSessionOnNextMessage = false;

            using var httpClient = HttpClientFactory.CreateClient("ApiClient");
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"api/aiteam/message?isClarificationPhase={_clarificationPhase}")
            {
                Content = JsonContent.Create(request)
            };
            httpRequest.SetBrowserResponseStreamingEnabled(true);
            var response = await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, _cts.Token);
            await ProcessApiStream(response, _cts.Token);
            
            if (!_clarificationPhase)
            {
                // Removed _isSessionComplete = true to keep the input visible
            }
        }
        catch (OperationCanceledException)
        {
            // Handled
        }
        catch (Exception ex)
        {
            _messages.Add(new ChatMessage { Author = "System", Content = $"Error: {ex.Message}", IsUser = false });
        }
        finally
        {
            await SaveCurrentSessionAsync();
            _isProcessing = false;
            StateHasChanged();
        }
    }

    private async Task DownloadReportAsync()
    {
        if (_isGeneratingReport)
        {
            return;
        }

        try
        {
            _isGeneratingReport = true;
            _reportStatusMessage = "Preparing report payload...";
            StateHasChanged();

            // Extract only the PM's final synthesis and the original user query
            var reportMessages = ExtractReportMessages();

            var request = new ReportRequest
            {
                Title = _currentSession?.Title ?? "Ai Team Session",
                Messages = reportMessages
            };

            using var httpClient = HttpClientFactory.CreateClient("ApiClient");
            _reportStatusMessage = "Generating PDF on server...";
            StateHasChanged();

            var response = await httpClient.PostAsJsonAsync("api/aiteam/report", request);
            response.EnsureSuccessStatusCode();

            _reportStatusMessage = "Downloading PDF...";
            StateHasChanged();

            var fileStream = await response.Content.ReadAsStreamAsync();
            using var streamRef = new DotNetStreamReference(stream: fileStream);

            var fileName = $"Report_{request.Title.Replace(" ", "_")}.pdf";
            await JSRuntime.InvokeVoidAsync("downloadFileFromStream", fileName, streamRef);

            _reportStatusMessage = "Report generated successfully.";
        }
        catch (Exception ex)
        {
             _reportStatusMessage = "Report generation failed.";
             _messages.Add(new ChatMessage { Author = "System", Content = $"Error downloading report: {ex.Message}", IsUser = false });
        }
        finally
        {
            _isGeneratingReport = false;
            StateHasChanged();
        }
    }

    /// <summary>
    /// Extracts only the meaningful content for the PDF report:
    /// the original user request and the PM's final synthesis (the message containing [DONE]).
    /// </summary>
    private List<ChatMessageDto> ExtractReportMessages()
    {
        var result = new List<ChatMessageDto>();

        // Include the first user message as project context
        var userQuery = _messages.FirstOrDefault(m => m.IsUser);
        if (userQuery is not null)
        {
            result.Add(new ChatMessageDto
            {
                Author = "Project Request",
                Content = userQuery.Content,
                IsUser = true
            });
        }

        // Find the PM's final synthesis — the last message containing [DONE]
        var pmSynthesis = _messages
            .LastOrDefault(m => !m.IsUser
                && m.Author != "System"
                && m.Content.Contains("[DONE]", StringComparison.OrdinalIgnoreCase));

        if (pmSynthesis is not null)
        {
            // Strip the [DONE] marker and any surrounding whitespace
            string cleanContent = NormalizeReportContent(pmSynthesis.Content
                .Replace("[DONE]", "", StringComparison.OrdinalIgnoreCase));

            result.Add(new ChatMessageDto
            {
                Author = pmSynthesis.Author,
                Content = cleanContent,
                IsUser = false
            });
        }
        else
        {
            // Fallback: if no [DONE] message found, include the last non-system agent message
            var lastAgentMessage = _messages
                .LastOrDefault(m => !m.IsUser && m.Author != "System");

            if (lastAgentMessage is not null)
            {
                result.Add(new ChatMessageDto
                {
                    Author = lastAgentMessage.Author,
                    Content = lastAgentMessage.Content,
                    IsUser = false
                });
            }
        }

        return result;
    }

    private static string NormalizeReportContent(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        var normalized = content.Replace("\r\n", "\n").Trim();
        var paragraphs = normalized.Split("\n\n", StringSplitOptions.None);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var uniqueParagraphs = new List<string>(paragraphs.Length);

        foreach (var paragraph in paragraphs)
        {
            var trimmedParagraph = paragraph.Trim();
            if (string.IsNullOrWhiteSpace(trimmedParagraph))
            {
                continue;
            }

            if (seen.Add(trimmedParagraph))
            {
                uniqueParagraphs.Add(trimmedParagraph);
            }
        }

        return string.Join("\n\n", uniqueParagraphs);
    }

    private void AddPhase(string title, string? description = null)
    {
        _phases.Add(new SessionPhase { Title = title, Description = description });
    }

    private void SetPhaseInProgress()
    {
        var next = _phases.FirstOrDefault(p => p.Status == SessionPhaseStatus.Pending);
        if (next is not null)
        {
            next.Status = SessionPhaseStatus.InProgress;
        }
    }

    private void CompleteCurrentPhase()
    {
        var current = _phases.LastOrDefault(p => p.Status == SessionPhaseStatus.InProgress);
        if (current is not null)
        {
            current.Status = SessionPhaseStatus.Completed;
        }
    }

    private void MarkPhaseCompleted(int index)
    {
        if (index >= 0 && index < _phases.Count)
        {
            _phases[index].Status = SessionPhaseStatus.Completed;
        }
    }

    private void EnsureAgentPhase(string agentName)
    {
        var label = ResolveAgentLabel(agentName);
        var existing = _phases.FirstOrDefault(p =>
            p.Status != SessionPhaseStatus.Completed
            && p.Title.Contains(label, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            existing.Status = SessionPhaseStatus.InProgress;
            return;
        }

        // Complete generic "processing" phase on first agent turn
        var processingPhase = _phases.LastOrDefault(p => p.Status == SessionPhaseStatus.InProgress);
        if (processingPhase is not null
            && !_phases.Any(p => p.Title.StartsWith("\U0001F5E3", StringComparison.Ordinal) && p.Status == SessionPhaseStatus.Completed))
        {
            processingPhase.Status = SessionPhaseStatus.Completed;
        }

        _phases.Add(new SessionPhase { Title = $"\U0001F5E3 {label}", Status = SessionPhaseStatus.InProgress });
    }

    private void CompleteAgentPhase(string agentName)
    {
        var label = ResolveAgentLabel(agentName);
        var phase = _phases.LastOrDefault(p =>
            p.Status == SessionPhaseStatus.InProgress
            && p.Title.Contains(label, StringComparison.OrdinalIgnoreCase));

        if (phase is not null)
        {
            phase.Status = SessionPhaseStatus.Completed;
        }
    }

    private string ResolveAgentLabel(string authorName)
    {
        if (_settings?.Agents is null)
        {
            return authorName;
        }

        var agent = _settings.Agents.FirstOrDefault(a =>
            string.Equals(a.DisplayName, authorName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(a.Role, authorName, StringComparison.OrdinalIgnoreCase));

        return agent is not null ? GetAgentToggleLabel(agent) : authorName;
    }

    private void CompleteAllPhases()
    {
        foreach (var phase in _phases)
        {
            if (phase.Status != SessionPhaseStatus.Completed)
            {
                phase.Status = SessionPhaseStatus.Completed;
            }
        }
    }
}

```

## File: Pages\Session.razor.css
```css
.agent-toggle-item {
    border-radius: 8px;
    transition: background-color 0.2s ease, box-shadow 0.2s ease;
    padding: 0.1rem 0.35rem;
}

.agent-toggle-item--active {
    background-color: rgba(76, 175, 80, 0.14);
    box-shadow: inset 0 0 0 1px rgba(76, 175, 80, 0.5);
}

/* Session content: chat + resizable summary panel side by side */
.session-content-row {
    display: flex;
    gap: 16px;
    height: 100%;
}

.session-chat-area {
    flex: 1 1 0%;
    min-width: 0;
    display: flex;
    flex-direction: column;
}

.session-summary-wrapper {
    flex: 0 0 auto;
    width: 260px;
    min-width: 180px;
    max-width: 50%;
    resize: horizontal;
    overflow: auto;
    direction: rtl;        /* puts the native resize grip on the left edge */
    border-left: 1px solid rgba(255, 255, 255, 0.08);
    padding-left: 8px;
}

.session-summary-wrapper > * {
    direction: ltr;        /* restore normal text direction for content */
}

/* On small screens stack vertically */
@media (max-width: 959px) {
    .session-content-row {
        flex-direction: column;
    }

    .session-summary-wrapper {
        width: 100%;
        max-width: 100%;
        min-width: 0;
        resize: vertical;
        max-height: 300px;
        min-height: 100px;
        border-left: none;
        border-top: 1px solid rgba(255, 255, 255, 0.08);
        padding-left: 0;
        padding-top: 8px;
        direction: ltr;
    }
}

```

## File: Pages\Settings.razor
```
@page "/settings"
@using System.Text.Json
@using AiAgileTeam.Models
@inject SettingsService SettingsService
@inject ISnackbar Snackbar
@inject HttpClient Http

<MudText Typo="Typo.h4" GutterBottom="true">Settings</MudText>

@if (_settings == null)
{
    <MudProgressCircular Color="Color.Primary" Indeterminate="true" />
}
else
{
    <MudCard Class="mb-4">
        <MudCardHeader>
            <CardHeaderContent>
                <MudText Typo="Typo.h6">API Key Settings</MudText>
            </CardHeaderContent>
        </MudCardHeader>
        <MudCardContent>
            <MudRadioGroup T="string" @bind-Value="_settings.ApiKeyMode" Class="mb-4">
                <MudRadio Value="@("global")" Color="Color.Primary">Global API Key (same for all agents)</MudRadio>
                <MudRadio Value="@("per-agent")" Color="Color.Secondary">Per-Agent API Key (each agent can use different provider)</MudRadio>
            </MudRadioGroup>
            
            @if (_settings.ApiKeyMode == "global")
            {
                <MudDivider Class="mb-4" />
                <MudText Typo="Typo.subtitle1" Class="mb-2">Global API Configuration</MudText>
                <ApiSettingsEditor Config="_settings.GlobalApi" />
            }
        </MudCardContent>
    </MudCard>

    <MudCard Class="mb-4">
        <MudCardHeader>
            <CardHeaderContent>
                <MudText Typo="Typo.h6">Orchestration</MudText>
            </CardHeaderContent>
        </MudCardHeader>
        <MudCardContent>
            <MudSelect T="OrchestrationMode" Label="Discussion orchestration" Variant="Variant.Outlined" @bind-Value="_settings.OrchestrationMode">
                <MudSelectItem Value="@OrchestrationMode.GroupChat">GroupChat (default)</MudSelectItem>
                <MudSelectItem Value="@OrchestrationMode.Magentic">Magentic</MudSelectItem>
            </MudSelect>
        </MudCardContent>
    </MudCard>

    <MudText Typo="Typo.h5" Class="mb-2">Agent Settings</MudText>
    
    @foreach (var agent in _settings.Agents)
    {
        <MudCard Class="mb-4">
            <MudCardHeader>
                <CardHeaderContent>
                    <div class="d-flex align-center gap-2 flex-wrap">
                        <MudTextField @bind-Value="agent.DisplayName" Label="Name" Variant="Variant.Outlined" Margin="Margin.Dense" Style="max-width: 200px;" />
                        <MudChip T="string" Color="Color.Info" Size="Size.Small">@agent.Role</MudChip>
                        @if (agent.IsMandatory)
                        {
                            <MudChip T="string" Color="Color.Warning" Size="Size.Small">Mandatory</MudChip>
                        }
                    </div>
                </CardHeaderContent>
            </MudCardHeader>
            <MudCardContent>
                @if (_settings.ApiKeyMode == "per-agent")
                {
                    <MudText Typo="Typo.subtitle2" Class="mb-2">API Configuration</MudText>
                    <ApiSettingsEditor Config="agent.ApiSettings ??= new ApiConfig()" />
                    <MudDivider Class="my-4" />
                }
                
                <MudText Typo="Typo.subtitle2" Class="mb-2">Model Configuration</MudText>
                <ModelSettingsEditor 
                    Config="agent.ModelSettings" 
                    ApiConfig="GetApiConfigForAgent(agent)"
                    OnLoadModels="LoadModels"
                    Models="_models"
                    IsLoading="_isLoadingModels" />
            </MudCardContent>
        </MudCard>
    }

    <MudButton Variant="Variant.Filled" Color="Color.Primary" OnClick="SaveSettings">Save Settings</MudButton>
}

@code {
    private AppSettings _settings = new();
    private List<string> _models = new();
    private bool _isLoadingModels = false;

    protected override async Task OnInitializedAsync()
    {
        _settings = await SettingsService.LoadSettingsAsync();
    }

    private ApiConfig GetApiConfigForAgent(AgentConfig agent)
    {
        if (_settings.ApiKeyMode == "global")
        {
            return _settings.GlobalApi;
        }
        return agent.ApiSettings ?? _settings.GlobalApi;
    }

    private async Task SaveSettings()
    {
        await SettingsService.SaveSettingsAsync(_settings);
        Snackbar.Add("Settings saved successfully.", Severity.Success);
    }

    private async Task LoadModels(ApiConfig config)
    {
        _isLoadingModels = true;
        _models.Clear();
        StateHasChanged();

        try
        {
            if (config.Provider == "OpenAI")
            {
                var request = new HttpRequestMessage(HttpMethod.Get, "https://api.openai.com/v1/models");
                request.Headers.Add("Authorization", $"Bearer {config.ApiKey}");
                var response = await Http.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                    _models = json.GetProperty("data")
                        .EnumerateArray()
                        .Select(m => m.GetProperty("id").GetString())
                        .Where(id => id != null && (id.Contains("gpt-") || id.Contains("o1") || id.Contains("o3")))
                        .ToList()!;
                }
                else
                {
                    Snackbar.Add($"Error loading OpenAI models: {response.StatusCode}", Severity.Error);
                }
            }
            else if (config.Provider == "GoogleGemini")
            {
                var url = $"https://generativelanguage.googleapis.com/v1beta/models?key={config.ApiKey}";
                var response = await Http.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                    _models = json.GetProperty("models")
                        .EnumerateArray()
                        .Select(m => m.GetProperty("name").GetString()!.Replace("models/", ""))
                        .Where(name => name.Contains("gemini-"))
                        .ToList()!;
                }
                else
                {
                    Snackbar.Add($"Error loading Gemini models: {response.StatusCode}", Severity.Error);
                }
            }
            else if (config.Provider == "AzureOpenAI")
            {
                if (string.IsNullOrWhiteSpace(config.Endpoint))
                {
                    Snackbar.Add("Endpoint is required for Azure OpenAI.", Severity.Warning);
                    return;
                }
                var url = $"{config.Endpoint.TrimEnd('/')}/openai/models?api-version=2025-06-01";
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("api-key", config.ApiKey);
                var response = await Http.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                    _models = json.GetProperty("data")
                        .EnumerateArray()
                        .Select(m => m.GetProperty("id").GetString())
                        .ToList()!;
                }
                else
                {
                    Snackbar.Add($"Error loading Azure models: {response.StatusCode}", Severity.Error);
                }
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Exception: {ex.Message}", Severity.Error);
        }
        finally
        {
            _isLoadingModels = false;
        }
    }
}

```

## File: Services\ApiHealthService.cs
```csharp
using System.Net.Http;

namespace AiAgileTeam.Services;

public sealed class ApiHealthService
{
    private readonly IHttpClientFactory _httpClientFactory;

    public ApiHealthService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public bool IsApiAvailable { get; private set; }

    public DateTimeOffset? LastCheckedAt { get; private set; }

    public event Action? StatusChanged;

    /// <summary>
    /// Checks whether the API server is reachable.
    /// </summary>
    public async Task CheckAsync(CancellationToken cancellationToken = default)
    {
        var isAvailable = false;

        try
        {
            using var httpClient = _httpClientFactory.CreateClient("ApiClient");
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/aiteam/health");
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            isAvailable = response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            isAvailable = false;
        }
        catch (TaskCanceledException)
        {
            isAvailable = false;
        }

        var hasStateChanged = IsApiAvailable != isAvailable;
        IsApiAvailable = isAvailable;
        LastCheckedAt = DateTimeOffset.Now;

        if (hasStateChanged)
        {
            StatusChanged?.Invoke();
        }
    }
}

```

## File: Services\ChatSessionService.cs
```csharp
using Blazored.LocalStorage;
using AiAgileTeam.Models;

namespace AiAgileTeam.Services;

public class ChatSessionService
{
    private readonly ILocalStorageService _localStorage;
    private const string SessionsKey = "ai_team_sessions";

    public ChatSessionService(ILocalStorageService localStorage)
    {
        _localStorage = localStorage;
    }

    public async Task<List<ChatSession>> GetAllSessionsAsync()
    {
        var sessions = await _localStorage.GetItemAsync<List<ChatSession>>(SessionsKey);
        return sessions ?? new List<ChatSession>();
    }

    public async Task<ChatSession?> GetSessionAsync(string id)
    {
        var sessions = await GetAllSessionsAsync();
        return sessions.FirstOrDefault(s => s.Id == id);
    }

    public async Task SaveSessionAsync(ChatSession session)
    {
        var sessions = await GetAllSessionsAsync();
        var existingIndex = sessions.FindIndex(s => s.Id == session.Id);

        if (existingIndex >= 0)
        {
            sessions[existingIndex] = session;
        }
        else
        {
            sessions.Add(session);
        }

        // Sort by updated descending
        sessions = sessions.OrderByDescending(s => s.UpdatedAt).ToList();
        
        await _localStorage.SetItemAsync(SessionsKey, sessions);
    }

    public async Task DeleteSessionAsync(string id)
    {
        var sessions = await GetAllSessionsAsync();
        sessions.RemoveAll(s => s.Id == id);
        await _localStorage.SetItemAsync(SessionsKey, sessions);
    }
}

```

## File: Services\SettingsService.cs
```csharp
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
    
    // Lists of human names used for generation
    private static readonly string[] MaleNames = { "Alexander", "Dmitry", "Maxim", "Sergey", "Andrew", "Alexey", "Artem", "Ilya", "Kirill", "Mikhail", "Nikita", "Egor", "Ivan", "Vladimir", "Pavel" };
    private static readonly string[] FemaleNames = { "Maria", "Anna", "Elena", "Olga", "Natalia", "Irina", "Tatyana", "Ekaterina", "Svetlana", "Anastasia", "Yulia", "Aleksandra", "Victoria", "Daria", "Polina" };
    
        private static readonly Dictionary<string, string[]> RoleSpecificNames = new()
        {
            { "Project Manager", new[] { "Alexander", "Dmitry", "Sergey", "Mikhail", "Elena", "Olga" } },
            { "Product Owner", new[] { "Maria", "Anna", "Natalia", "Andrew", "Alexey", "Irina" } },
            { "Architect", new[] { "Dmitry", "Maxim", "Artem", "Ilya", "Ekaterina", "Svetlana" } },
            { "Developer", new[] { "Kirill", "Nikita", "Egor", "Ivan", "Anastasia", "Yulia" } },
            { "QA", new[] { "Tatyana", "Victoria", "Daria", "Pavel", "Vladimir", "Polina" } },
            { "Scrum Master", new[] { "Aleksandra", "Irina", "Mikhail", "Sergey", "Elena", "Andrew" } }
        };
    
    private static readonly HashSet<string> _usedNames = new();
    private static readonly Random _random = new();

    private static readonly HashSet<string> LegacyRoleNames =
    [
        "Project Manager",
        "Product Owner",
        "Architect",
        "Developer",
        "QA Engineer",
        "Scrum Master",
        "QA"
    ];

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
                if (LegacyRoleNames.Contains(agent.Name))
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

            agent.Role = NormalizeRole(agent.Role);
            
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

            var isBuiltInRole = BuiltInAgentPrompts.IsBuiltInRole(agent.Role);
            if (isBuiltInRole)
            {
                agent.IsBuiltIn = true;

                if (string.IsNullOrWhiteSpace(agent.SystemPrompt) && BuiltInAgentPrompts.TryGetPrompt(agent.Role, out var defaultPrompt))
                {
                    agent.SystemPrompt = defaultPrompt;
                }
            }
        }
    }

    public async Task SaveSettingsAsync(AppSettings settings)
    {
        await _localStorage.SetItemAsync(SettingsKey, settings);
    }
    
    /// <summary>
    /// Get a random display name for the specified role
    /// </summary>
    public static string GetRandomNameForRole(string role)
    {
        // Normalize role name
        var normalizedRole = NormalizeRole(role);
        
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
            CreateBuiltInAgent("Project Manager", isMandatory: true),
            CreateBuiltInAgent("Product Owner"),
            CreateBuiltInAgent("Architect"),
            CreateBuiltInAgent("Developer"),
            CreateBuiltInAgent("QA"),
            CreateBuiltInAgent("Scrum Master")
        };
    }

    private static string NormalizeRole(string? role)
    {
        var normalizedRole = role?.Trim() ?? "";
        if (normalizedRole.Equals("QA Engineer", StringComparison.OrdinalIgnoreCase))
        {
            return "QA";
        }

        return normalizedRole;
    }

    private static AgentConfig CreateBuiltInAgent(string role, bool isMandatory = false)
    {
        if (!BuiltInAgentPrompts.TryGetPrompt(role, out var prompt))
        {
            throw new InvalidOperationException($"Default prompt for role '{role}' was not found.");
        }

        return new AgentConfig
        {
            DisplayName = GetRandomNameForRole(role),
            Role = role,
            SystemPrompt = prompt,
            IsMandatory = isMandatory,
            IsSelected = true,
            IsBuiltIn = true,
            UseDefaultPrompt = true
        };
    }
}

```

## File: Shared\AgentConfig.cs
```csharp
namespace AiAgileTeam.Models;

public class AgentConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// Human-friendly display name for the agent (e.g. "Alexander", "Maria")
    /// </summary>
    public string DisplayName { get; set; } = "";
    
    /// <summary>
    /// Professional role/position (e.g. "Project Manager", "Architect")
    /// </summary>
    public string Role { get; set; } = "";
    
    /// <summary>
    /// Agent system prompt (without the name - it is added automatically)
    /// </summary>
    public string SystemPrompt { get; set; } = "";
    
    // API settings - used only when AppSettings.ApiKeyMode == "per-agent"
    public ApiConfig? ApiSettings { get; set; }
    
    // Model settings - always per-agent
    public ModelConfig ModelSettings { get; set; } = new();
    
    public bool IsSelected { get; set; } = true;
    public bool IsMandatory { get; set; } = false;
    public bool IsBuiltIn { get; set; } = false;
    public bool UseDefaultPrompt { get; set; } = false;
    
    // Legacy properties for migration
    public string? Name { get; set; } // deprecated - now uses DisplayName, kept for migration
    public ProviderConfig? ProviderSettings { get; set; } // deprecated, use ApiSettings + ModelSettings
    public int MaxTokensPerResponse { get; set; } = 1000; // deprecated, use ModelSettings.MaxTokensPerResponse
    public int MaxRoundsPerSession { get; set; } = 3; // deprecated, use ModelSettings.MaxRoundsPerSession
}

```

## File: Shared\AiAgileTeam.Shared.csproj
```
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

</Project>

```

## File: Shared\AppSettings.cs
```csharp
namespace AiAgileTeam.Models;

public class AppSettings
{
    public string ApiKeyMode { get; set; } = "global"; // global | per-agent
    public OrchestrationMode OrchestrationMode { get; set; } = OrchestrationMode.GroupChat;
    public ApiConfig GlobalApi { get; set; } = new();
    public List<AgentConfig> Agents { get; set; } = new();
    
    // Legacy properties for migration
    public string? Mode { get; set; } // deprecated, use ApiKeyMode
    public ProviderConfig? Global { get; set; } // deprecated, use GlobalApi
}

public class ApiConfig
{
    public string Provider { get; set; } = "OpenAI"; // OpenAI | AzureOpenAI | GoogleGemini
    public string ApiKey { get; set; } = "";
    public string Endpoint { get; set; } = ""; // только для Azure
}

public class ModelConfig
{
    public string Model { get; set; } = "";
    public int MaxTokensPerResponse { get; set; } = 1000;
    public int MaxRoundsPerSession { get; set; } = 3;
}

public class ProviderConfig
{
    public string Provider { get; set; } = "OpenAI"; // OpenAI | AzureOpenAI | GoogleGemini
    public string ApiKey { get; set; } = "";
    public string Endpoint { get; set; } = "";       // только для Azure
    public string Model { get; set; } = "";
}

```

## File: Shared\BuiltInAgentPrompts.cs
```csharp
namespace AiAgileTeam.Models;

public static class BuiltInAgentPrompts
{
    private static readonly Dictionary<string, string> Prompts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Project Manager"] = "You are a Senior Project Manager and Orchestrator. Your mission is to deliver a professional Software Requirements Specification (SRS) and a Roadmap.\n\n" +
                              "Responsibilities:\n" +
                              "1. Initiate the discussion by setting the agenda and defining the project scope based on the user's query.\n" +
                              "2. The system will automatically call experts in the right order — you do NOT need to pick who speaks next.\n" +
                              "3. When you speak, focus on moderating: summarize progress, resolve conflicts, and keep the team on track.\n" +
                              "4. Final Synthesis: Once all experts have contributed, combine their input into a final, structured Markdown document (SRS).\n" +
                              "The document MUST include: \n" +
                              "   - Project Overview\n" +
                              "   - Business Requirements (from PO)\n" +
                              "   - Technical Architecture & Stack (from Architect)\n" +
                              "   - Implementation Details (from Developer)\n" +
                              "   - Quality Assurance Plan (from QA)\n" +
                              "   - Project Roadmap & Risks (from Scrum Master)\n" +
                              "5. Conclude with exactly '[DONE]' after the final document is presented.\n\n" +
                              "IMPORTANT: You receive a compressed summary of the discussion. Use it to stay informed without re-reading everything.\n" +
                              "Interaction Style: Professional, leadership-oriented, focused on deliverables.",

        ["Product Owner"] = "You are a Product Owner. Your focus is on maximizing product value and defining 'the what'.\n\n" +
                            "Responsibilities:\n" +
                            "1. Define high-level business requirements and user personas.\n" +
                            "2. Create a prioritized backlog of features/user stories.\n" +
                            "3. Define acceptance criteria for the main features.\n" +
                            "Deliverables: Business value proposition, User Stories, Feature List.",

        ["Architect"] = "You are a Software Architect. Your focus is on 'the how' at a high level.\n\n" +
                        "Responsibilities:\n" +
                        "1. Propose the technology stack (backend, frontend, database, etc.).\n" +
                        "2. Describe the system architecture (microservices, monolith, layers).\n" +
                        "3. Identify key technical risks and scalability strategies.\n" +
                        "Deliverables: Tech Stack, Component Diagrams (described in text/Mermaid), Infrastructure overview.",

        ["Developer"] = "You are a Senior Software Engineer. Your focus is on technical implementation and feasibility.\n\n" +
                        "Responsibilities:\n" +
                        "1. Provide implementation details for complex features.\n" +
                        "2. Suggest database schema (key tables/entities).\n" +
                        "3. Advise on security best practices and API design.\n" +
                        "Deliverables: Data Schema, Implementation Strategy, Critical Algorithms description.",

        ["QA"] = "You are a Lead QA Engineer. Your focus is on quality and reliability.\n\n" +
                 "Responsibilities:\n" +
                 "1. Define the testing strategy (Unit, Integration, E2E).\n" +
                 "2. Identify potential edge cases and security vulnerabilities.\n" +
                 "3. Suggest quality metrics and CI/CD quality gates.\n" +
                 "Deliverables: Test Plan, List of Edge Cases, Quality Assurance Strategy.",

        ["Scrum Master"] = "You are a Scrum Master. Your focus is on the Agile process and project execution.\n\n" +
                           "Responsibilities:\n" +
                           "1. Define the sprint structure and ceremony cadence.\n" +
                           "2. Estimate project timelines and identify delivery risks.\n" +
                           "3. Suggest team composition and communication protocols.\n" +
                           "Deliverables: Sprint Roadmap, Risk Matrix, Team Workflow definition."
    };

    public static bool IsBuiltInRole(string role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return false;
        }

        return Prompts.ContainsKey(role.Trim());
    }

    public static bool TryGetPrompt(string role, out string prompt)
    {
        prompt = string.Empty;

        if (string.IsNullOrWhiteSpace(role))
        {
            return false;
        }

        return Prompts.TryGetValue(role.Trim(), out prompt);
    }
}

```

## File: Shared\ChatMessage.cs
```csharp
namespace AiAgileTeam.Models;

public class ChatMessage
{
    public string Author { get; set; } = "";
    public string Content { get; set; } = "";
    public bool IsUser { get; set; }
}

```

## File: Shared\ChatSession.cs
```csharp
using System;
using System.Collections.Generic;

namespace AiAgileTeam.Models;

public class ChatSession
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = "New Session";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<ChatMessage> Messages { get; set; } = new();
    public AppSettings SettingsSnapshot { get; set; } = new();
}

```

## File: Shared\DTOs.cs
```csharp
using System.ComponentModel.DataAnnotations;

namespace AiAgileTeam.Models;

public class SessionRequest
{
    public string Query { get; set; } = "";
    
    public bool Clarify { get; set; } = false;

    [Required]
    public AppSettings Settings { get; set; } = new();

    public List<ChatMessageDto> History { get; set; } = new();
    
    public string? ServerSessionId { get; set; }
}

public class ChatMessageDto
{
    public string Author { get; set; } = "";
    public string Content { get; set; } = "";
    public bool IsUser { get; set; }
}

public class StreamingMessageDto
{
    public string Author { get; set; } = "";
    public string ContentPiece { get; set; } = "";
    public bool IsComplete { get; set; }
    public string? ServerSessionId { get; set; }
}

public class ReportRequest
{
    public string Title { get; set; } = "";
    public List<ChatMessageDto> Messages { get; set; } = new();
}

```

## File: Shared\OrchestrationMode.cs
```csharp
using System.Text.Json.Serialization;

namespace AiAgileTeam.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OrchestrationMode
{
    GroupChat,
    Magentic
}

```

## File: Shared\SessionPhase.cs
```csharp
namespace AiAgileTeam.Models;

/// <summary>
/// Represents a discrete phase/step in the session workflow shown in the summary panel.
/// </summary>
public record SessionPhase
{
    public string Title { get; init; } = "";
    public string? Description { get; init; }
    public SessionPhaseStatus Status { get; set; } = SessionPhaseStatus.Pending;
}

public enum SessionPhaseStatus
{
    Pending,
    InProgress,
    Completed
}

```
