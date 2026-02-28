using AiAgileTeam.Services;
using AiAgileTeam.Services.Orchestration;
using AiAgileTeam.Models;
using Polly;
using Polly.Extensions.Http;
using QuestPDF.Infrastructure;

QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.Configure<ModelPricingOptions>(builder.Configuration.GetSection("TokenPricing"));
builder.Services.Configure<GeminiPricingConfig>(builder.Configuration.GetSection("TokenPricing:GeminiPricing"));
builder.Services.Configure<MediaStorageOptions>(builder.Configuration.GetSection("MediaStorage"));
builder.Services.AddSingleton<ITokenUsageTracker, InMemoryTokenUsageTracker>();
builder.Services.AddSingleton<IModelPricingService, GeminiPricingService>();
builder.Services.AddSingleton<ITokenUsageContextAccessor, AsyncLocalTokenUsageContextAccessor>();
builder.Services.AddSingleton<IMediaStorageService>(sp =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<MediaStorageOptions>>().Value;
    if (options.Provider.Equals("AzureBlob", StringComparison.OrdinalIgnoreCase))
    {
        return new AzureBlobMediaStorageService(sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<MediaStorageOptions>>());
    }

    return new InMemoryMediaStorageService();
});

builder.Services.AddSingleton<SessionStore>();
builder.Services.AddSingleton<MarkdownService>();
builder.Services.AddSingleton<GroupChatOrchestrationStrategy>();
builder.Services.AddSingleton<MagenticOrchestrationStrategy>();
builder.Services.AddSingleton<IAgentOrchestrator, AgentOrchestrator>();
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
