# AiAgileTeam — Архитектура управления агентами и логика сессии

> Автоматически сгенерировано из исходного кода.

---

## 1. Общая архитектура

```
┌─────────────────────────────────────────────────────┐
│            Blazor WASM (Client)                     │
│  Pages/Session.razor                                │
│  ┌────────────┐  ┌───────────────┐  ┌────────────┐ │
│  │ ChatLog    │  │ Input Field   │  │ Summary    │ │
│  │ Component  │  │ + Send/Stop   │  │ Panel      │ │
│  └─────┬──────┘  └──────┬────────┘  └─────┬──────┘ │
│        │                │                  │        │
│        └────────┬───────┘                  │        │
│                 ▼                          │        │
│         ProcessApiStream()                 │        │
│         (JSON streaming)                   │        │
└────────────────┬───────────────────────────┘        │
                 │ HTTP Streaming                      │
                 ▼                                     │
┌─────────────────────────────────────────────────────┐
│            ASP.NET Core API (Server)                │
│  Api/Controllers/AiTeamController.cs                │
│  ┌──────────────┐  ┌───────────────────────────┐   │
│  │ SessionStore │  │ AiTeamService             │   │
│  │ (in-memory)  │  │ (создание агентов и LLM)  │   │
│  └──────┬───────┘  └──────────┬────────────────┘   │
│         │                     │                     │
│         ▼                     ▼                     │
│  ┌─────────────────────────────────────────┐       │
│  │ OrchestrationStrategyFactory            │       │
│  │  ┌──────────────────────────────────┐   │       │
│  │  │ GroupChatOrchestrationStrategy   │   │       │
│  │  │  ├─ PmOrchestratorSelection...   │   │       │
│  │  │  ├─ SafeTerminationStrategy      │   │       │
│  │  │  └─ ChatContextCompressor        │   │       │
│  │  │ MagenticOrchestrationStrategy    │   │       │
│  │  │  ├─ StandardMagenticManager      │   │       │
│  │  │  └─ InProcessRuntime             │   │       │
│  │  └──────────────────────────────────┘   │       │
│  └─────────────────────────────────────────┘       │
└─────────────────────────────────────────────────────┘
```

---

## 2. Жизненный цикл сессии

### 2.1. Две фазы сессии

Каждая сессия проходит через **две последовательные фазы**:

| # | Фаза | Endpoint | Условие перехода |
|---|-------|----------|------------------|
| 1 | **Уточнение** (Clarification) | `POST /api/aiteam/session` → `POST /api/aiteam/message?isClarificationPhase=true` | Активна, если пользователь включил флаг `Clarify` |
| 2 | **Командная дискуссия** (Team Discussion) | `POST /api/aiteam/message?isClarificationPhase=false` | Начинается после завершения уточнения или сразу, если `Clarify=false` |

### 2.2. Диаграмма полного жизненного цикла

```
[User Input]
     │
     ├── Clarify = true ──► [Clarification Phase]
     │                            │
     │                  ┌─────────┼─────────┐
     │                  ▼         ▼         ▼
     │              Ask Q1    Ask Q2    Ask Q3
     │                  │         │         │
     │              Answer    Answer    Answer
     │                  │         │         │
     │                  ▼         ▼         ▼
     │            [READY]+count>=2  OR  count>=3
     │                        │
     │                        ▼
     │              New Server Session
     │                        │
     └── Clarify = false ─────┤
                              ▼
                    [Discussion Phase]
                         │
              ┌──────────┼──────────┐
              ▼          ▼          ▼
         PM selects   PM selects  PM selects
         Agent A      Agent B     Agent A
              │          │          │
              ▼          ▼          ▼
         Responds    Responds    Responds
              │          │          │
              └──────────┼──────────┘
                         ▼
                    PM says "DONE"
                         │
                         ▼
                  PM Final Synthesis
                   (with [DONE])
                         │
                         ▼
                  [Session Complete]
                   Report available
```

---

## 3. Фаза уточнения (Clarification)

### 3.1. Агент уточнения

**Clarification Agent** — временный агент, создаваемый на время фазы. Не входит в команду дискуссии.

```
Инструкции:
1. Задать до 3 целевых вопросов (цель проекта, аудитория, техн. ограничения)
2. Вопросы — по одному, не перегружать пользователя
3. При достаточной информации — структурированный Project Brief
4. Завершить финальный вывод словом [READY]
```

### 3.2. Управление переходом (Session.razor)

Клиент отслеживает `_clarificationQuestionsAsked` — счётчик завершённых раундов Q&A.

```
Условие перехода к дискуссии:
  ([READY] в ответе И count >= 2)  ИЛИ  (count >= 3)
```

| Состояние счётчика | [READY] в ответе | Результат |
|--------------------|------------------|-----------|
| 0 (первый вопрос) | Да | Игнорируется — слишком рано |
| 1 | Да | Игнорируется |
| 2 | Да | Переход к дискуссии |
| 3 | Нет | Авто-переход (safety net) |

**Критически важно**: при переходе к дискуссии `_serverSessionId` сбрасывается в `null` → создаётся **новая серверная сессия**. Это необходимо, потому что SK's `InvokeAsync(agent)` регистрирует Clarification Agent в `AgentGroupChat` через `EnsureAgent`. Если переиспользовать сессию, PM будет выбирать Clarification Agent в дискуссии, получая пустые ответы.

### 3.3. Поток данных уточнения

```
User (Blazor)              AiTeamController           Clarification Agent    LLM
    │                           │                           │                 │
    │ POST /session             │                           │                 │
    │ {Clarify:true, Query:""}  │                           │                 │
    │──────────────────────────►│                           │                 │
    │                           │ groupChat.InvokeAsync()   │                 │
    │                           │──────────────────────────►│                 │
    │                           │                           │ Chat Completion │
    │                           │                           │────────────────►│
    │                           │                           │◄────────────────│
    │                           │◄──────────────────────────│                 │
    │◄──────────────────────────│ StreamingMessageDto       │                 │
    │  _clarificationQuestionsAsked++                       │                 │
    │                           │                           │                 │
    │ POST /message             │                           │                 │
    │ {isClarification:true}    │                           │                 │
    │──────────────────────────►│                           │                 │
    │   ... повторяется ...     │                           │                 │
    │                           │                           │                 │
    │ [READY] detected &&       │                           │                 │
    │  count >= 2               │                           │                 │
    │                           │                           │                 │
    │ POST /message             │                           │                 │
    │ {isClarification:false,   │                           │                 │
    │  ServerSessionId:null}    │ ◄── Новая серверная сессия                  │
    │──────────────────────────►│     (чистый GroupChat)    │                 │
```

---

## 4. Фаза командной дискуссии (Team Discussion)

### 4.1. Создание агентов

Агенты создаются из `AppSettings.Agents` (только `IsSelected == true`):

```
AiTeamService.CreateAgent(agentConfig, appSettings):
  1. Определить ApiConfig (global или per-agent)
  2. Определить Model из agentConfig.ModelSettings.Model
  3. Валидация: модель не может быть пустой
  4. Создать IChatCompletionService через Semantic Kernel
  5. Сформировать промпт:
     "Your name is {DisplayName}. You are a {Role}. {SystemPrompt}"
  6. Создать ChatCompletionAgent с max_tokens из настроек
```

Формат промпта агента используется для извлечения роли:
```
Regex: "You are a ([^.]+)\."
```
Этот паттерн критичен — `PmOrchestratorSelectionStrategy` и `GetAgentRole()` зависят от него.

### 4.2. Выбор оркестрации

Во время фазы дискуссии сервер выбирает стратегию по `AppSettings.OrchestrationMode`.

| OrchestrationMode | Реализация | Назначение |
|---|---|---|
| `GroupChat` (default) | `GroupChatOrchestrationStrategy` | Текущий battle-tested режим с кастомными стратегиями выбора/завершения |
| `Magentic` | `MagenticOrchestrationStrategy` | Dynamic planning через `StandardMagenticManager` и `MagenticOrchestration` |

### 4.3. Конфигурация GroupChat

```csharp
groupChat.ExecutionSettings = new()
{
    SelectionStrategy = new PmOrchestratorSelectionStrategy(pmLlm, compressor),
    TerminationStrategy = new SafeTerminationStrategy()
    {
        MaximumIterations = agentsCount * 3 + 3
    }
};
```

### 4.4. Стратегия выбора следующего агента (PmOrchestratorSelectionStrategy)

PM LLM решает, кто говорит следующим. Полный алгоритм:

```
[Новый ход]
     │
     ▼
 Последнее сообщение от User?
     │
     ├── Да ──► Есть @mention? ──► Да ──► Отдать ход упомянутому агенту
     │                │
     │                └── Нет ──► Отдать ход PM (re-evaluate)
     │
     └── Нет ──► Сжать историю (ChatContextCompressor)
                      │
                      ▼
               Собрать список ролей (без PM)
                      │
                      ▼
               Спросить PM LLM: "Кто следующий?"
                      │
                      ├── PM ответил DONE ──► Передать PM для финального синтеза
                      │
                      ├── Распознана роль ──► Anti-loop check
                      │                            │
                      │                    тот же агент >2 раз?
                      │                      │            │
                      │                     Да           Нет
                      │                      │            │
                      │              Принудительная   Выбрать
                      │                ротация        агента
                      │
                      └── Не распознана ──► Retry (до 2 раз)
                                                │
                                           Всё ещё нет?
                                                │
                                                ▼
                                      Fallback: round-robin
```

#### Промпт оркестратора PM

```
You are the orchestrator of a software team discussion.
Available roles: {ROLES}

Rules:
1. Each expert should speak at least once before the discussion ends.
2. Do not select the same expert more than twice in a row.
3. If all experts have contributed and the discussion is complete → DONE
4. Otherwise respond with ONLY the role name.
```

Параметры LLM-вызова: `max_tokens=30`, `temperature=0.1`.

### 4.5. Сжатие контекста (ChatContextCompressor)

Для экономии токенов история разделяется на две части:

```
┌──────────────────────────────────────────────────┐
│  Полная история (N сообщений)                    │
│                                                   │
│  ┌──────────────────────┐ ┌───────────────────┐  │
│  │  Старые сообщения    │ │ Последние 4 msg   │  │
│  │  → LLM-суммаризация  │ │ (verbatim)        │  │
│  │  (кэшированная)      │ │                   │  │
│  └──────────────────────┘ └───────────────────┘  │
│           ↓                        ↓              │
│     [Discussion summary]    [Recent messages]     │
└──────────────────────────────────────────────────┘
```

- `tailSize = 4` — последние 4 сообщения сохраняются полностью
- Старые сообщения суммаризируются LLM в один абзац (max 300 слов)
- Суммаризация кэшируется — при новых сообщениях дополняется инкрементально
- Параметры суммаризации: `max_tokens=400`, `temperature=0.2`

### 4.6. Стратегия завершения (SafeTerminationStrategy)

Четыре уровня защиты от зацикливания:

| # | Условие | Действие |
|---|---------|----------|
| 1 | PM написал `[DONE]` в последнем сообщении | Нормальное завершение |
| 2 | Один агент говорит **3 раза подряд** | Принудительное завершение (loop) |
| 3 | Последние **4 сообщения** похожи на ≥85% (Jaccard) | Завершение по staleness |
| 4 | `MaximumIterations` (= agents×3 + 3) | Hard limit (наследуется от SK) |

Jaccard similarity считается по word-sets сообщений:
```
similarity = |A ∩ B| / |A ∪ B|
```

### 4.7. Буферизация floor-change сообщений (только GroupChat)

Проблема: SK всегда производит metadata-chunk с `AuthorName` для каждого агента, даже если LLM вернул пустой ответ. Контроллер **буферизует** floor-change и отправляет клиенту только при наличии реального контента:

```
SK chunk: AuthorName="Наталья", Content=""
  → буфер: pendingFloorChange = true

SK chunk: AuthorName="Наталья", Content="Текст анализа..."
  → flush: yield "floor to Наталья" + yield content

SK chunk: AuthorName="Артём", Content=""
  → предыдущий буфер (Артём) без контента → skip + log
```

### 4.8. Magentic orchestration (true Magentic)

В режиме `Magentic` используется `MagenticOrchestration` + `StandardMagenticManager`.

```csharp
var manager = new StandardMagenticManager(pmLlm, new OpenAIPromptExecutionSettings { Temperature = 0.1f })
{
    MaximumInvocationCount = agentsCount * 3 + 3
};

var orchestration = new MagenticOrchestration(manager, agents)
{
    ResponseCallback = OnResponseAsync
};
```

Особенности режима:

- `ResponseCallback` отдает **полные сообщения** (не chunk-stream как `InvokeStreamingAsync` в GroupChat)
- выполняется через `InProcessRuntime`
- финальный ответ дополнительно нормализуется до наличия маркера `[DONE]`
- `@mention` и floor-change механика GroupChat в этом режиме не используются

---

## 5. Управление серверными сессиями

### 5.1. SessionStore

```
SessionStore (ConcurrentDictionary<string, SessionEntry>)
  │
  ├── SessionData
  │     ├── AgentGroupChat (SK group chat instance)
  │     ├── IsConfigured (bool — агенты созданы?)
  │     ├── OrchestrationMode (GroupChat | Magentic)
  │     ├── Compressor (ChatContextCompressor)
  │     └── PmLlm (IChatCompletionService для PM)
  │
  └── LastAccessed (DateTime — для возможной очистки)
```

### 5.2. Создание / переиспользование сессий

| Сценарий | ServerSessionId | Поведение |
|----------|----------------|-----------|
| Первый запрос | `null` → генерируется `Guid` | Новая сессия, пустой GroupChat |
| Следующее сообщение в той же фазе | Существующий ID | Переиспользование (история в GroupChat) |
| Переход Clarification → Discussion | Сброс в `null` | **Новая сессия** (чистый agent pool) |
| Пользователь переключил агентов | `_startNewServerSessionOnNextMessage = true` | Новая сессия + replay истории |

---

## 6. Конфигурация агентов

### 6.1. Структура AgentConfig

```
AgentConfig
  ├── Id (Guid)
  ├── DisplayName ("Наталья", "Артём")     → Agent.Name в SK
  ├── Role ("Business Analyst", "Architect") → извлекается из промпта
  ├── SystemPrompt (без имени — добавляется автоматически)
  ├── ModelSettings
  │     ├── Model ("gemini-2.5-flash-lite")
  │     └── MaxTokensPerResponse (min 100, default 1000)
  ├── ApiSettings (если ApiKeyMode == "per-agent")
  ├── IsSelected (включён в сессию?)
  └── IsMandatory (нельзя отключить, например PM)
```

### 6.2. Формирование промпта

```
Входные данные:
  DisplayName = "Наталья"
  Role = "Business Analyst"
  SystemPrompt = "Analyze requirements and create user stories..."

Результат:
  "Your name is Наталья. You are a Business Analyst. Analyze requirements..."
```

### 6.3. Поддерживаемые LLM провайдеры

| Provider | Connector | Примечание |
|----------|-----------|------------|
| OpenAI | `AddOpenAIChatCompletion` | Стандартный |
| Azure OpenAI | `AddAzureOpenAIChatCompletion` | Требует Endpoint |
| Google Gemini | `AddGoogleAIGeminiChatCompletion` | SKEXP0070 |

---

## 7. Клиентская обработка стрима (Session.razor)

### 7.1. ProcessApiStream

Клиент десериализует `IAsyncEnumerable<StreamingMessageDto>` и обрабатывает:

```
StreamingMessageDto
  ├── Author ("Наталья", "System", "Clarification Agent")
  ├── ContentPiece (фрагмент текста)
  ├── IsComplete (true = агент завершил ход)
  └── ServerSessionId (для привязки к серверной сессии)
```

### 7.2. Обработка маркеров

| Маркер | Где проверяется | Действие |
|--------|----------------|----------|
| `[READY]` | Session.razor (клиент) | Переход от Clarification к Discussion |
| `[DONE]` | `SafeTerminationStrategy` (GroupChat) / `MagenticOrchestrationStrategy` (Magentic) | Завершение дискуссии |
| `[DONE]` | Session.razor (клиент) | `_isSessionComplete = true`, показать кнопку отчёта |
| `@AgentName` | PmOrchestratorSelectionStrategy | Прямая передача хода упомянутому агенту |

### 7.3. Фазы UI (SessionSummaryPanel)

```
SessionPhase { Title, Description, Status }
  Status: Pending → InProgress → Completed

Типичная последовательность:
  1. "Request sent" → Completed
  2. "Clarification" → Completed
  3. "Team discussion" → InProgress
  4. "🗣 Business Analyst — Наталья" → Completed
  5. "🗣 Architect — Артём" → InProgress
  6. "Session completed" → Completed
```

---

## 8. Генерация отчёта

```
[Кнопка Download Report]
        │
        ▼
  ExtractReportMessages()
        │
        ▼
  POST /api/aiteam/report
        │
        ▼
  Очистка:
  - убрать System
  - убрать floor-change
  - убрать [DONE]
  - дедупликация
        │
        ▼
  QuestPDF генерация PDF
        │
        ▼
  A4 документ с Markdown
```

Отчёт содержит:
- **Project Request** — исходный запрос пользователя (выделен синим фоном)
- **PM Synthesis** — финальное сообщение PM с `[DONE]` (основное тело документа)
- Рендеринг Markdown через `MarkdownService`

---

## 9. Ключевые файлы

| Файл | Ответственность |
|------|----------------|
| `Pages/Session.razor` | UI сессии, управление фазами, стриминг |
| `Api/Controllers/AiTeamController.cs` | API endpoints, создание агентов, streaming |
| `Api/Services/Orchestration/IOrchestrationStrategy.cs` | Контракт оркестрации дискуссии |
| `Api/Services/Orchestration/OrchestrationStrategyFactory.cs` | Выбор оркестратора по `OrchestrationMode` |
| `Api/Services/Orchestration/GroupChatOrchestrationStrategy.cs` | Реализация GroupChat-оркестрации |
| `Api/Services/Orchestration/MagenticOrchestrationStrategy.cs` | Реализация true Magentic orchestration |
| `Api/Services/PmOrchestratorSelectionStrategy.cs` | PM-driven выбор следующего агента |
| `Api/Services/SafeTerminationStrategy.cs` | Условия завершения дискуссии |
| `Api/Services/ChatContextCompressor.cs` | Сжатие истории для экономии токенов |
| `Api/Services/AiTeamService.cs` | Фабрика агентов и LLM-сервисов |
| `Api/Services/SessionStore.cs` | In-memory хранилище серверных сессий |
| `Services/ApiHealthService.cs` | Health-check polling |
| `Shared/DTOs.cs` | Модели запросов/ответов API |
| `Shared/AgentConfig.cs` | Конфигурация агента |
| `Shared/OrchestrationMode.cs` | Доступные режимы оркестрации |
