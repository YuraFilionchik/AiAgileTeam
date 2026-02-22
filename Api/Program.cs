using AiAgileTeam.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddHttpClient<AiTeamService>();
builder.Services.AddScoped<AiTeamService>();

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
