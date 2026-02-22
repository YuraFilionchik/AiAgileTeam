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
    var baseUrl = config["AppSettings:ApiBaseUrl"] ?? "https://localhost:7135";
    client.BaseAddress = new Uri(baseUrl);
});


builder.Services.AddMudServices();
builder.Services.AddBlazoredLocalStorage();
builder.Services.AddScoped<SettingsService>();

await builder.Build().RunAsync();
