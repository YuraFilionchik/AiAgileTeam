# AiAgileTeam — Виртуальная команда разработки ПО

**Версия:** 1.2  
**Платформа:** .NET 9, Blazor WebAssembly + ASP.NET Core API  
**AI Framework:** Microsoft Semantic Kernel  
**Последнее обновление:** 23.02.2026 (человеческие имена агентов, независимые настройки API)

---

## 📖 Описание

AiAgileTeam — это приложение, которое симулирует работу виртуальной команды ИИ-агентов для планирования и проектирования программного обеспечения. Пользователь вводит задачу, а команда агентов (Project Manager, Product Owner, Architect, Developer, QA Engineer, Scrum Master) проводит обсуждение и генерирует структурированный документ SRS (Software Requirements Specification).

---

## 🏗️ Архитектура

### Структура решения

```
AiAgileTeam.sln
├── AiAgileTeam.csproj          # Blazor WebAssembly (Frontend)
├── AiAgileTeam.Api.csproj      # ASP.NET Core API (Backend)
├── AiAgileTeam.Shared.csproj   # Общие модели (DTO)
├── plans/
│   └── agent-names-and-api-settings.md  # План реализации имен агентов и настроек API
├── docs/
│   ├── agent-streaming-diagnosis.md  # Диагностика проблем стриминга
│   ├── chat-stop-issue.md           # Анализ проблемы остановки чата
│   ├── chat-streaming-fix.md         # Исправление проблем стриминга
│   ├── debug-agents-order.md         # Отладка порядка агентов
│   └── resilient-state-machine.md   # Документация State Machine
│
├── Frontend (Blazor WASM)
│   ├── Program.cs              # Точка входа, DI
│   ├── App.razor               # Корневой компонент
│   ├── Pages/
│   │   ├── Index.razor         # Главная: ввод задачи, выбор агентов
│   │   ├── Session.razor       # Чат сессии с агентами
│   │   ├── Settings.razor      # Настройки API провайдеров и имен агентов
│   │   └── History.razor       # История сессий
│   ├── Components/
│   │   ├── ChatLog.razor       # Компонент лога чата
│   │   ├── AddAgentModal.razor # Модалка добавления агента
│   │   ├── ApiSettingsEditor.razor # Редактор API настроек
│   │   ├── ModelSettingsEditor.razor # Редактор настроек модели
│   │   └── ProviderSettingsEditor.razor
│   └── Services/
│       ├── SettingsService.cs  # Настройки (LocalStorage) + генерация имен
│       └── ChatSessionService.cs # История сессий
├── Backend (ASP.NET Core API)
│   ├── Program.cs              # Точка входа, CORS, DI
│   ├── Controllers/
│   │   └── AiTeamController.cs # API чата, стриминг, PDF
│   ├── Services/
│   │   ├── AiTeamService.cs    # Создание агентов SK
│   │   ├── SessionStore.cs     # In-memory хранилище сессий
│   │   ├── DeterministicStrategies.cs # Детерминированные стратегии
│   │   ├── ResilientWorkflowSelectionStrategy.cs # Устойчивая стратегия выбора
│   │   └── TokenSavingTerminationStrategy.cs # Стратегия завершения
│   └── Models/
│       └── AiTeamController.cs # Модели для API
└── Shared (Class Library)
    ├── AppSettings.cs          # Конфигурация приложения
    ├── AgentConfig.cs          # Конфигурация агента (DisplayName, Role)
    ├── ChatSession.cs          # Модель сессии
    ├── ChatMessage.cs          # Модель сообщения
    └── DTOs.cs                 # DTO для API
```

---

## 🚀 Запуск проекта

### Требования

- .NET 9 SDK
- API ключ одного из провайдеров:
  - OpenAI
  - Azure OpenAI
  - Google Gemini

### Инструкции

```bash
# 1. Клонировать репозиторий
git clone <repository-url>
cd AiAgileTeam

# 2. Запустить Backend (API)
cd Api
dotnet run
# API запустится на http://localhost:5270 или https://localhost:7135

# 3. Запустить Frontend (в новом терминале)
cd ..
dotnet run
# Frontend запустится на http://localhost:5139 или https://localhost:7169

# 4. Открыть в браузере
# http://localhost:5139
```

### Настройка API ключей

1. Откройте `/settings` в приложении
2. Выберите режим: **Global** (один ключ для всех) или **Per-Agent** (разные ключи для каждого агента)
3. Введите API ключ и модель
4. Сохраните настройки

---

## 🤖 Агенты по умолчанию

| Агент | Роль | Обязательный |
|-------|------|--------------|
| Project Manager | Координация, финальный SRS документ | ✅ |
| Product Owner | Бизнес-требования, User Stories | ❌ |
| Architect | Tech stack, архитектура системы | ❌ |
| Developer | Детали реализации, схема БД | ❌ |
| QA Engineer | Стратегия тестирования | ❌ |
| Scrum Master | Sprint roadmap, риски | ❌ |

### Человеческие имена агентов

При первом запуске каждому агенту присваивается случайное человеческое имя (например, "Александр", "Мария", "Дмитрий"). Имя можно изменить в настройках.

**Пример промпта:**
```
Your name is Александр. You are a Project Manager. You are a Senior Project Manager and Orchestrator...
```

---

## 🔧 Новые возможности (версия 1.2)

### 👤 Человеческие имена агентов

**Изменение:** Агенты получают случайные человеческие имена при первом запуске.

**Особенности:**
- Имена выбираются из списка русских мужских и женских имён
- Для каждой роли есть предпочтительные имена
- Имя можно изменить в настройках
- Должность (Role) сохраняется и отображается рядом с именем

**Формат промпта:**
```
Your name is {DisplayName}. You are a {Role}. {SystemPrompt}
```

**Файлы изменены:**
- `Shared/AgentConfig.cs` - добавлено поле `DisplayName`, `Role` теперь отдельно
- `Services/SettingsService.cs` - генерация случайных имён
- `Api/Services/AiTeamService.cs` - формирование промпта с именем и должностью
- `Pages/Settings.razor` - UI для редактирования имени

### 🔐 Независимые настройки API

**Изменение:** API ключи могут быть глобальными или per-agent, а модель/токены всегда per-agent.

**Структура настроек:**
```csharp
public class AppSettings
{
    public string ApiKeyMode { get; set; } = "global"; // global | per-agent
    public ApiConfig GlobalApi { get; set; } = new();
    public List<AgentConfig> Agents { get; set; } = new();
}

public class ApiConfig
{
    public string Provider { get; set; } = "OpenAI";
    public string ApiKey { get; set; } = "";
    public string Endpoint { get; set; } = ""; // только для Azure
}

public class ModelConfig
{
    public string Model { get; set; } = "";
    public int MaxTokensPerResponse { get; set; } = 1000;
    public int MaxRoundsPerSession { get; set; } = 3;
}
```

---

## 📡 API Endpoints

### `POST /api/aiteam/session`
Запуск новой сессии.

**Request:**
```json
{
  "query": "Создать Todo приложение",
  "clarify": false,
  "settings": { 
    "apiKeyMode": "global",
    "globalApi": { "provider": "OpenAI", "apiKey": "sk-...", "endpoint": "" },
    "agents": [
      {
        "displayName": "Александр",
        "role": "Project Manager",
        "systemPrompt": "...",
        "apiSettings": null,
        "modelSettings": { "model": "gpt-4", "maxTokensPerResponse": 1000, "maxRoundsPerSession": 3 }
      }
    ]
  },
  "history": []
}
```

**Response:** Server-Sent Events (SSE) стриминг `StreamingMessageDto`

---

### `POST /api/aiteam/message`
Отправка сообщения в активную сессию.

**Query Parameters:**
- `isClarificationPhase` (bool)

---

### `POST /api/aiteam/report`
Генерация PDF отчёта.

**Response:** `application/pdf` файл

---

## 🔧 Конфигурация

### AgentConfig (Shared/AgentConfig.cs)

```csharp
public class AgentConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// Человеческое имя агента (например, "Александр", "Мария")
    /// </summary>
    public string DisplayName { get; set; } = "";
    
    /// <summary>
    /// Профессиональная роль/должность (например, "Project Manager", "Architect")
    /// </summary>
    public string Role { get; set; } = "";
    
    /// <summary>
    /// Системный промпт агента (без имени - оно добавляется автоматически)
    /// </summary>
    public string SystemPrompt { get; set; } = "";
    
    // API settings - used only when AppSettings.ApiKeyMode == "per-agent"
    public ApiConfig? ApiSettings { get; set; }
    
    // Model settings - always per-agent
    public ModelConfig ModelSettings { get; set; } = new();
    
    public bool IsSelected { get; set; } = true;
    public bool IsMandatory { get; set; } = false;
}
```

---

## 🎯 Ключевые технологии

| Компонент | Технология |
|-----------|------------|
| Frontend | Blazor WebAssembly 9 |
| UI Library | MudBlazor 9 |
| Backend | ASP.NET Core 9 |
| AI Orchestration | Microsoft Semantic Kernel 1.72 |
| Local Storage | Blazored.LocalStorage |
| Markdown | Markdig |
| PDF Generation | QuestPDF |
| HTTP Client | Polly (retry policy) |

---

## 📝 Хранение данных

### LocalStorage (Frontend)

| Ключ | Данные |
|------|--------|
| `ai_team_settings` | Настройки приложения и агентов (включая DisplayName) |
| `ai_team_sessions` | История чат-сессий |

---

## 🔄 Поток выполнения сессии

```
1. Пользователь вводит задачу на Index.razor
   ↓
2. Навигация на /session?query=...&clarify=...
   ↓
3. Session.razor: OnInitializedAsync()
   ↓
4. POST /api/aiteam/session
   ↓
5. AiTeamController.StartSession()
   ├── Создаётся SessionStore.GetOrCreate()
   ├── Добавляются агенты в AgentGroupChat
   ├── Настраивается SelectionStrategy + TerminationStrategy
   └── Запускается groupChat.InvokeStreamingAsync()
   ↓
6. Стриминг ответов через SSE
   ↓
7. ProcessApiStream() на фронтенде
   ├── Парсит JSON StreamingMessageDto
   ├── Добавляет сообщения в _messages
   ├── Сохраняет в LocalStorage
   └── Проверяет [DONE] / [READY]
   ↓
8. Session complete → Download Report
```

---

## 🔧 Детерминированный workflow

### Улучшенная стратегия выбора агентов

**Файлы:**
- `Api/Services/ResilientWorkflowSelectionStrategy.cs` - устойчивая стратегия
- `Api/Services/TokenSavingTerminationStrategy.cs` - стратегия завершения

**Особенности:**
- **Детерминированный порядок:** Project Manager → Product Owner → Architect → Developer → QA Engineer → Scrum Master → Project Manager [DONE]
- **Реакция на прерывания:** Новое сообщение от пользователя сбрасывает цикл на PM
- **Гибридная маршрутизация:** @упоминания передают слово конкретному агенту без сброса цикла
- **Защита от преждевременного завершения:** [DONE] проверяется только в последнем сообщении от PM
- **Сигнал завершения хода:** Каждый агент завершает ход явным `IsComplete=true`

---

## 📊 Планы развития

| Приоритет | Функция | Описание |
|-----------|---------|----------|
| High | Обработка ошибок API | Retry logic, timeout, user-friendly сообщения |
| High | Логирование работы стратегий | Запись в консоль выбора агента и состояния _currentStep |
| Medium | Экспорт в Markdown | Кроме PDF |
| Medium | Редактирование промптов | UI для настройки системных промптов агентов |
| Low | Шаблоны команд | Пресеты для типовых задач (CRUD, API, Mobile App) |
| Low | Статистика сессий | Длительность, количество сообщений, токены |

---

## 🧪 Тестирование

### Чек-лист ручного тестирования

- [ ] Запуск с пустыми настройками → ошибка API key
- [ ] Запуск с валидным ключом → сессия начинается
- [ ] Project Manager говорит первым
- [ ] Все выбранные агенты участвуют
- [ ] Финальный документ содержит [DONE]
- [ ] Кнопка Stop прерывает сессию
- [ ] PDF отчёт генерируется
- [ ] История сохраняется в LocalStorage
- [ ] Персистентность сессии после перезагрузки
- [ ] Редактирование имен агентов в настройках
- [ ] Переключение Global/Per-Agent API Key
- [ ] Разные модели для разных агентов
- [ ] Человеческие имена агентов отображаются в чате

---

## 📄 Лицензия

MIT

---

## 👥 Контакты

**Проект:** AiAgileTeam  
**Дата создания:** 2026  
**Последнее обновление:** 23.02.2026  
**Новые возможности:** Человеческие имена агентов, независимые настройки API

ВАЖНО! - всегда обновляй актуальную информацию в данном файле после внесенных значимых изменений.