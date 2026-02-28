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
builder.Services.AddScoped<TokenUsageService>();
builder.Services.AddScoped<MediaUploadService>();
await builder.Build().RunAsync();
