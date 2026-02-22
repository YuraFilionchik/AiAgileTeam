**инструкции для ИИ-кодера**  
(версия от 22 февраля 2026)

```markdown
# Инструкции для ИИ по реализации проекта: AI Agile/Scrum-команда в Blazor Web

Ты — ИИ-кодер, специализирующийся на .NET, Blazor и Semantic Kernel. Реализуй MVP веб-приложения строго по этому плану. Используй **Blazor WebAssembly (.NET 9)**. Проект должен быть простым, но полностью рабочим, с акцентом на Semantic Kernel Agents.

**Обязательные требования:**
- .NET 9 (целевая framework)
- Все настройки (ключи, провайдеры, модели, выбранные агенты) хранятся  в localStorage через Blazored.LocalStorage,но при их отсутствии использовать по умолчанию из appsettings.json (для удобства тестирования)
- Обработка ошибок: если ключей нет — показывать MudAlert + автоматический редирект в /settings
- Полная поддержка трёх провайдеров: OpenAI, Azure OpenAI, Google Gemini, c возможностью легкого добавления новых
- Автоматическая загрузка списка моделей после ввода ключа (кнопка «Load / Refresh Models»)
- Streaming ответов агентов в чат
- Чекбокс «Enable Clarification Agent»

## Структура проекта
- Pages: Index.razor, Main.razor, Settings.razor, Session.razor
- Components: AgentSelector.razor, ChatLog.razor, AddAgentModal.razor
- Services: AiTeamService.cs (scoped), SettingsService.cs
- Models: AppSettings.cs, AgentConfig.cs, ChatMessage.cs

## Шаг 1: Настройка проекта и базовая структура
- Проект уже создан по шаблону Blazor WebAssembly
- убери лишние файлы и компоненты (например, Counter.razor, FetchData.razor)
- Добавь MudBlazor в Program.cs и MainLayout.razor
- Добавь навигацию MudNavMenu с ссылками на Main и Settings

## Шаг 2: Страница Settings.razor (самый важный шаг — сделай идеально)

**UI-элементы:**
- Radio: Global Settings vs Per-Agent Settings
- Select Provider: OpenAI / Azure OpenAI / Google Gemini
- Поля (в зависимости от провайдера):
  - OpenAI → API Key
  - Azure OpenAI → Endpoint + API Key
  - Google Gemini → API Key
- Кнопка **«Load / Refresh Models»** (асинхронная)
- После загрузки — MudSelect с загруженными моделями (id + displayName если есть)

**Актуальные endpoints для загрузки моделей (используй именно эти):**

```csharp
// OpenAI
private const string OpenAIModelsUrl = "https://api.openai.com/v1/models";

// Google Gemini
private const string GeminiModelsUrl = "https://generativelanguage.googleapis.com/v1beta/models?key={0}";

// Azure OpenAI (2026)
private string GetAzureModelsUrl(string endpoint) => 
    $"{endpoint.TrimEnd('/')}/openai/models?api-version=2025-06-01";
```

**Пример кода загрузки моделей (вставь в Settings.razor или SettingsService):**

```csharp
// Для OpenAI
var client = new HttpClient();
client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
var response = await client.GetFromJsonAsync<JsonElement>("https://api.openai.com/v1/models");
var models = response.GetProperty("data")
    .EnumerateArray()
    .Select(m => m.GetProperty("id").GetString())
    .Where(id => id.Contains("gpt-") || id.Contains("o"))
    .ToList();

// Для Google Gemini
var url = string.Format(GeminiModelsUrl, apiKey);
var geminiResponse = await client.GetFromJsonAsync<JsonElement>(url);
var geminiModels = geminiResponse.GetProperty("models")
    .EnumerateArray()
    .Select(m => m.GetProperty("name").GetString()!.Replace("models/", ""))
    .Where(name => name.Contains("gemini-"))
    .ToList();
```

**Хранение настроек (AppSettings.cs):**
```csharp
public class AppSettings
{
    public string Mode { get; set; } = "global"; // global | per-agent
    public ProviderConfig Global { get; set; } = new();
    public List<AgentConfig> Agents { get; set; } = new();
}

public class ProviderConfig
{
    public string Provider { get; set; } = "OpenAI"; // OpenAI | AzureOpenAI | GoogleGemini
    public string ApiKey { get; set; } = "";
    public string Endpoint { get; set; } = "";       // только для Azure
    public string Model { get; set; } = "";
}
```

## Шаг 3: AiTeamService.cs — создание Kernel и агентов

**Ключевой метод:**

```csharp
public IChatCompletionService CreateChatService(ProviderConfig config)
{
    var builder = Kernel.CreateBuilder();

    switch (config.Provider)
    {
        case "OpenAI":
            builder.AddOpenAIChatCompletion(config.Model, config.ApiKey);
            break;
        case "AzureOpenAI":
            builder.AddAzureOpenAIChatCompletion(config.Model, config.Endpoint, config.ApiKey);
            break;
        case "GoogleGemini":
            #pragma warning disable SKEXP0070
            builder.AddGoogleAIGeminiChatCompletion(config.Model, config.ApiKey);
            #pragma warning restore SKEXP0070
            break;
    }
    var kernel = builder.Build();
    return kernel.GetRequiredService<IChatCompletionService>();
}
```

**Создание агента:**
```csharp
var agent = new ChatCompletionAgent
{
    Kernel = kernelWithService,
    Name = agentConfig.Name,
    Instructions = agentConfig.SystemPrompt
};
```

## Шаг 4: Main.razor
- MudTable предопределённых агентов (5 шт.) с чекбоксами
- Modal добавления кастомного агента (Name, Role, System Prompt)
- Textarea для user query
- Чекбокс «Enable Clarification Agent»
- Button «Start Session» → проверка ключей → NavigationManager.NavigateTo($"/session?query=...")

## Шаг 5: Clarification Agent
- Создай отдельного агента `ClarificationAgent` с инструкцией:
  "Ты Clarification Agent. Задай пользователю максимум 3 уточняющих вопроса. После получения ответов сделай summary и предложи refined query."

## Шаг 6: Session.razor + Group Chat
- Используй `AgentGroupChat` (из Microsoft.SemanticKernel.Agents.Core)
- Streaming: `await foreach (var message in groupChat.InvokeAsync(...))`
- Human-in-the-loop: `groupChat.AddUserMessage(userInput)`

## Шаг 7: Интеграция и тестирование
- Streaming обновление UI через StateHasChanged()
- Mock-режим при отсутствии ключей
- Edge cases: invalid key, empty query, clarification loop, per-agent разные провайдеры

Реализуй **строго по шагам**, генерируя полный рабочий код для каждого шага.  
После каждого шага отмечай в комментариях:
`// ШАГ X ВЫПОЛНЕН — [короткий комментарий]`

Если возникнут вопросы по API — спрашивай сразу.  
Начинай с Шага 1.
```
