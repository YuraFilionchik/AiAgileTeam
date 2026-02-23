# Проблема: Чат ИИ-агентов останавливается после первого сообщения Scrum Master

**Дата:** 23 февраля 2026 г.
**Статус:** ✅ Исправлено (ResilientWorkflowSelectionStrategy)
**Приоритет:** Высокий

---

## Описание проблемы

При запуске сессии с командой ИИ-агентов для планирования проекта чат останавливается после первого же сообщения от Scrum Master'а. Остальные участники команды (Product Owner, Architect, Developer, QA Engineer) не получают возможности высказаться.

### Пример лога чата

```
User: Todo приложение с быстрым добавлением заметок и удобной сортировкой

System: [Project Manager gives floor to Scrum Master...]

Scrum Master:
Отлично! Как скрам-мастер, я рад помочь вашей команде...
[длинное сообщение с вопросами для команды, user stories, планом спринта]
...
С чего начнем?

[ЧАТ ОСТАНАВЛИВАЕТСЯ — НИКТО БОЛЬШЕ НЕ ОТВЕЧАЕТ]
```

**Ключевая аномалия:** Первым говорит Scrum Master, а не Project Manager (как задумано по архитектуре).

---

## Архитектура системы

### Компоненты

```
AiAgileTeam (Blazor WebAssembly + ASP.NET Core Backend)
│
├── Frontend (Blazor)
│   ├── Pages/Session.razor          # Страница сессии чата
│   ├── Components/ChatLog.razor     # Компонент отображения сообщений
│   └── Services/
│       ├── SettingsService.cs       # Управление настройками агентов
│       └── ChatSessionService.cs    # Сохранение сессий в LocalStorage
│
├── Backend (ASP.NET Core API)
│   ├── Api/Controllers/
│   │   └── AiTeamController.cs      # Контроллер чата (основная логика)
│   └── Api/Services/
│       ├── AiTeamService.cs         # Создание агентов Semantic Kernel
│       └── SessionStore.cs          # Хранение активных сессий в памяти
│
└── Models
    ├── AppSettings.cs               # Конфигурация приложения
    ├── AgentConfig.cs               # Конфигурация агента
    ├── ChatSession.cs               # Модель сессии
    └── ChatMessage.cs               # Модель сообщения
```

### Поток выполнения

1. Пользователь вводит запрос → `Session.razor`
2. Запрос отправляется на `/api/aiteam/session` → `AiTeamController.StartSession()`
3. Создаётся `AgentGroupChat` с агентами из настроек
4. Запускается обсуждение через `RunTeamDiscussionAsync()`
5. Ответы стримятся обратно на фронтенд через Server-Sent Events (SSE)

---

## Анализ кода

### Конфигурация агентов по умолчанию

Файл: `Services/SettingsService.cs`

```csharp
private List<AgentConfig> GetDefaultAgents()
{
    return new List<AgentConfig>
    {
        new AgentConfig {
            Name = "Project Manager",
            Role = "Project Manager",
            IsMandatory = true,  // Всегда включён
            SystemPrompt = "You are a Senior Project Manager and Orchestrator..."
        },
        new AgentConfig { Name = "Product Owner", ... },
        new AgentConfig { Name = "Architect", ... },
        new AgentConfig { Name = "Developer", ... },
        new AgentConfig { Name = "QA Engineer", ... },
        new AgentConfig {
            Name = "Scrum Master",
            Role = "Scrum Master",
            SystemPrompt = "You are a Scrum Master..."
        }
    };
}
```

**Ожидаемый порядок выступлений:**
1. Project Manager (устанавливает повестку)
2. Product Owner (требования)
3. Architect (архитектура)
4. Developer (реализация)
5. QA Engineer (тестирование)
6. Scrum Master (план спринта)
7. Project Manager (финальный SRS документ + [DONE])

---

### Стратегия выбора агента (SelectionStrategy)

Файл: `Api/Controllers/AiTeamController.cs`

Используется `KernelFunctionSelectionStrategy` с промптом для LLM-модератора:

```csharp
SelectionStrategy = new KernelFunctionSelectionStrategy(
    KernelFunctionFactory.CreateFromPrompt(
        """
        You are the moderator deciding who speaks next in a software development team discussion.
        
        ## Conversation so far:
        {{$history}}
        
        ## Available participants:
        {agentNames}
        
        ## Orchestration Logic:
        1. Initialization: If the conversation just started, choose 'Project Manager'.
        2. Specialist Input: After Project Manager sets the stage, call specialists...
        ...
        """,
    projectManager.Kernel)
)
```

**Проблема:** Промпт не давал чёткого указания, что Project Manager должен говорить ПЕРВЫМ. LLM могла выбрать любого агента, включая Scrum Master.

---

### Стратегия завершения (TerminationStrategy)

```csharp
TerminationStrategy = new KernelFunctionTerminationStrategy(
    KernelFunctionFactory.CreateFromPrompt(
        """
        Review the conversation below and determine if the team has successfully completed the task.
        
        ## Termination Criteria (Must meet all):
        1. All requested aspects have been discussed.
        2. The 'Project Manager' has delivered a final summary.
        3. The 'Project Manager' has explicitly concluded with '[DONE]'.
        4. If the conversation is clearly looping or no new information is being added, you may terminate.
        """),
    projectManager.Kernel)
{
    MaximumIterations = agentsToRun.Count * 2 + 2
}
```

**Проблема:** Пункт 4 позволял завершить обсуждение преждевременно, если LLM решит, что "no new information is being added".

---

## Корневые причины проблемы

### Причина 1: Нечёткое правило первого спикера

**Было:**
```
1. Initialization: If the conversation just started, choose 'Project Manager'.
```

**Проблема:** LLM интерпретирует "conversation just started" неоднозначно. Если в истории уже есть сообщение пользователя, LLM может считать, что разговор уже начался, и выбрать любого доступного агента.

### Причина 2: Scrum Master говорит первым

Из лога видно:
```
System: [Project Manager gives floor to Scrum Master...]
```

Это сообщение генерируется в коде при смене автора, но **Project Manager никогда не говорил**. Стратегия выбора сразу выбрала Scrum Master.

### Причина 3: Преждевременное завершение

После длинного сообщения Scrum Master'а стратегия завершения могла сработать, потому что:
- Только один специалист высказался
- Нет финального документа от Project Manager
- Но LLM могла решить, что "достаточно информации для начала работы"

---

## Внесённые исправления

### Файл: `Api/Controllers/AiTeamController.cs`

#### 1. Создана гибкая стратегия выбора агента (ResilientWorkflowSelectionStrategy)

**Файл:** `Api/Services/ResilientWorkflowSelectionStrategy.cs`

**Ключевые особенности:**
- **Детерминированный порядок выступлений** — жёсткий workflow вместо LLM-модератора
- **Реакция на прерывания пользователя** — сброс цикла при новом сообщении от пользователя
- **Гибридная маршрутизация** — поддержка @упоминаний для обращения к конкретным агентам
- **Защита от бесконечного цикла** — fallback на Project Manager

**Логика работы:**

```csharp
protected override Task<Agent> SelectAgentAsync(...)
{
    var lastMessage = history.LastOrDefault();

    // 1. РЕАКЦИЯ НА ПРЕРЫВАНИЕ ПОЛЬЗОВАТЕЛЕМ
    if (lastMessage != null && lastMessage.Role == AuthorRole.User)
    {
        // Проверяем наличие прямого упоминания (@AgentName)
        var mentionedAgent = TryFindMentionedAgent(agents, lastMessage.Content);
        
        if (mentionedAgent != null)
        {
            // Передаем слово упомянутому специалисту
            return Task.FromResult(mentionedAgent);
        }
        
        // Сбрасываем счетчик — начинаем цикл заново с PM
        _currentStep = 0;
        return Task.FromResult(GetAgent(agents, "Project Manager"));
    }

    // 2. ЗАЩИТА ОТ БЕСКОНЕЧНОГО ЦИКЛА
    if (_currentStep >= _workflowOrder.Length)
    {
        return Task.FromResult(GetAgent(agents, "Project Manager"));
    }

    // 3. СТАНДАРТНЫЙ ПОТОК
    string nextAgentName = _workflowOrder[_currentStep];
    _currentStep++;
    return Task.FromResult(GetAgent(agents, nextAgentName));
}
```

**Workflow порядок:**
```
Project Manager → Product Owner → Architect → Developer → QA Engineer → Scrum Master → Project Manager [DONE]
```

**Примеры использования:**

| Сценарий | Поведение |
|----------|-----------|
| Пользователь пишет "Добавьте авторизацию через Google" | Сброс цикла → PM начинает обсуждение заново |
| Пользователь пишет "@Architect, какую БД выбрать?" | Слово передаётся Architect напрямую |
| Агенты отработали по кругу | Fallback на PM для финального [DONE] |

#### 2. Улучшена стратегия завершения (TokenSavingTerminationStrategy)

**Файл:** `Api/Services/TokenSavingTerminationStrategy.cs`

**Ключевое улучшение:** Проверка [DONE] только в последнем сообщении от PM:

```csharp
protected override Task<bool> ShouldAgentTerminateAsync(...)
{
    var lastMessage = history.LastOrDefault();
    
    // Завершаем только если сейчас отработал PM, 
    // и именно в его ТЕКУЩЕМ сообщении есть [DONE]
    bool isDone = lastMessage != null && 
                  lastMessage.AuthorName == "Project Manager" && 
                  lastMessage.Content != null && 
                  lastMessage.Content.Contains("[DONE]", StringComparison.OrdinalIgnoreCase);

    return Task.FromResult(isDone);
}
```

**Защита от преждевременного завершения:**
- ❌ Не завершает, если [DONE] был в старой истории
- ❌ Не завершает, если [DONE] написал не PM
- ✅ Завершает только при [DONE] в последнем сообщении от PM

#### 3. Увеличен лимит итераций

**Было:** `MaximumIterations = agentsToRun.Count * 2 + 2`
**Стало:** `MaximumIterations = agentsToRun.Count * 3 + 3`

Для 6 агентов: было 14 итераций, стало 21 итерация.

---

## Сценарии работы

### Сценарий 1: Стандартное обсуждение

```
User: Todo приложение с быстрым добавлением заметок

→ Project Manager (устанавливает повестку)
→ Product Owner (требования)
→ Architect (архитектура)
→ Developer (реализация)
→ QA Engineer (тестирование)
→ Scrum Master (план спринта)
→ Project Manager [DONE] (финальный SRS)
```

### Сценарий 2: Прерывание пользователем

```
[PM отработал] → [Product Owner отработал]
User: "А добавьте еще авторизацию через Google"

→ Project Manager (сброс цикла, координация новых требований)
→ Product Owner (анализ новых user stories)
→ Architect (интеграция OAuth)
→ ...
→ Project Manager [DONE]
```

### Сценарий 3: Прямое обращение (@упоминание)

```
[Идет обсуждение]
User: "@Architect, какую базу данных лучше взять для этого проекта?"

→ Architect (отвечает на вопрос напрямую, без сброса цикла)
→ [Продолжение стандартного workflow с текущей позиции]
```

### Сценарий 4: Прерывание после завершения

```
[PM написал [DONE], чат завершен]
User: "А добавьте еще раздел с рисками"

→ Project Manager (сброс цикла, добавление нового раздела)
→ Scrum Master (обновление рисков)
→ Project Manager [DONE] (обновленный финальный документ)
```

---

## Ожидаемое поведение после исправления

```
User: Todo приложение с быстрым добавлением заметок и удобной сортировкой

System: [Project Manager gives floor to Project Manager...]

Project Manager:
[Устанавливает повестку, определяет scope проекта, вызывает специалистов...]

System: [Project Manager gives floor to Product Owner...]

Product Owner:
[Определяет бизнес-требования, user stories, acceptance criteria...]

System: [Project Manager gives floor to Architect...]

Architect:
[Предлагает tech stack, архитектуру системы...]

System: [Project Manager gives floor to Developer...]

Developer:
[Описывает схему БД, API, ключевые алгоритмы...]

System: [Project Manager gives floor to QA Engineer...]

QA Engineer:
[Определяет стратегию тестирования, edge cases...]

System: [Project Manager gives floor to Scrum Master...]

Scrum Master:
[Определяет спринты, roadmap, риски...]

System: [Project Manager gives floor to Project Manager...]

Project Manager:
[Финальный SRS документ со всеми разделами]
...
[DONE]

[Чат завершён, доступна кнопка "Download Report"]
```

---
## Технические детали

### Зависимости

```xml
<PackageReference Include="Microsoft.SemanticKernel" Version="..." />
<PackageReference Include="Microsoft.SemanticKernel.Agents" Version="..." />
<PackageReference Include="Microsoft.SemanticKernel.Connectors.Google" Version="..." />
```

### Ключевые классы Semantic Kernel

- `AgentGroupChat` — группа агентов для обсуждения
- `ChatCompletionAgent` — отдельный агент с инструкциями
- `KernelFunctionSelectionStrategy` — LLM-модератор для выбора следующего спикера
- `KernelFunctionTerminationStrategy` — LLM-модератор для решения о завершении

### Поддерживаемые провайдеры

- OpenAI (GPT-4, GPT-3.5-Turbo)
- Azure OpenAI
- Google Gemini

---

## Контакты

**Автор анализа:** [Ваше имя]  
**Дата:** 23.02.2026  
**Проект:** AiAgileTeam — Виртуальная команда разработки ПО
