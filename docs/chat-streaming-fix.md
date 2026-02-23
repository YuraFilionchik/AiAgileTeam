# Исправление проблемы остановки чата после первого сообщения агента

**Дата:** 23 февраля 2026 г.  
**Статус:** ✅ Исправлено  
**Версия:** 3.0

---

## 🐛 Описание проблемы

Чат останавливался после первого сообщения любого агента (чаще всего Scrum Master'а). Остальные участники команды не получали возможности высказаться.

### Симптомы

1. После запуска сессии первый агент начинает говорить
2. Сообщение отображается полностью
3. Чат замолкает, остальные агенты не активируются
4. При ручном вводе сообщения пользователем следующий агент отвечает, но затем снова тишина

---

## 🔍 Найденные проблемы

### Проблема 1: Отсутствие сигнала завершения для каждого агента

**Файл:** `Api/Controllers/AiTeamController.cs`

**Описание:** Бэкенд отправлял `IsComplete = true` только один раз в конце ВСЕГО обсуждения, а не после ответа каждого агента.

**Было:**
```csharp
// Финальный IsComplete отправлялся только после завершения всего цикла
else if (!firstChunk)
{
    yield return new StreamingMessageDto { Author = currentAuthor, ContentPiece = "", IsComplete = true, ServerSessionId = serverSessionId };
}
```

**Стало:**
```csharp
// Отправляем IsComplete = true КАЖДЫЙ раз, когда меняется автор
if (!firstChunk && !string.IsNullOrEmpty(currentAuthor))
{
    Console.WriteLine($"[AiTeamController] Sending IsComplete=true for '{currentAuthor}'");
    yield return new StreamingMessageDto { Author = currentAuthor, ContentPiece = "", IsComplete = true, ServerSessionId = serverSessionId };
}
```

---

### Проблема 2: Фронтенд не обрабатывал завершение ответа агента

**Файл:** `Pages/Session.razor`

**Описание:** `ProcessApiStream` не распознавал момент, когда ответ агента завершен, и не переходил к обработке следующего агента.

**Было:**
```csharp
if (message.IsComplete)
{
    await SaveCurrentSessionAsync();
    if (uiMessage.Content.Contains("[DONE]")) { /* завершить сессию */ }
    // Нет явного разделения между завершением агента и завершением сессии
}

if (currentAuthor != message.Author || uiMessage == null)
{
    // Создавалось новое сообщение, но без явного завершения предыдущего
}
```

**Стало:**
```csharp
// Явная обработка завершения ответа агента
if (message.IsComplete && !string.IsNullOrEmpty(message.Author) && message.Author != "System")
{
    lastMessageWasComplete = true;
    await SaveCurrentSessionAsync();

    if (uiMessage != null && uiMessage.Content.Contains("[DONE]"))
    {
        _isSessionComplete = true;
        StateHasChanged();
        return; // Stop processing - session is complete
    }
    
    continue; // Skip further processing for completion messages
}

// Обработка смены автора с финализацией предыдущего сообщения
if (currentAuthor != message.Author || uiMessage == null)
{
    if (uiMessage != null && !lastMessageWasComplete)
    {
        await SaveCurrentSessionAsync();
    }
    
    currentAuthor = message.Author;
    uiMessage = new ChatMessage { Author = currentAuthor, Content = "", IsUser = false };
    _messages.Add(uiMessage);
    lastMessageWasComplete = false;
}
```

---

### Проблема 3: Отсутствие `IsSelected = true` у агентов по умолчанию

**Файл:** `Services/SettingsService.cs`

**Описание:** Агенты по умолчанию создавались без флага `IsSelected = true`, из-за чего они могли не добавляться в обсуждение.

**Было:**
```csharp
new AgentConfig {
    Name = "Product Owner",
    // ...
    // IsSelected отсутствует
}
```

**Стало:**
```csharp
new AgentConfig {
    Name = "Product Owner",
    // ...
    IsSelected = true  // Явно указано для всех агентов
}
```

---

## ✅ Внесённые изменения

### 1. Api/Controllers/AiTeamController.cs

#### Изменение 1.1: Отправка IsComplete после каждого агента

```csharp
// Detect author change - this means previous agent finished their turn
bool authorChanged = !firstChunk && !string.IsNullOrEmpty(message.AuthorName) && message.AuthorName != currentAuthor;

if (authorChanged)
{
    Console.WriteLine($"[AiTeamController] Author changed from '{currentAuthor}' to '{message.AuthorName}'");
}

// Only switch author if the new author is non-null and different from current
if (firstChunk || (!string.IsNullOrEmpty(message.AuthorName) && message.AuthorName != currentAuthor))
{
    // Signal completion of previous agent's message BEFORE switching
    if (!firstChunk && !string.IsNullOrEmpty(currentAuthor))
    {
        Console.WriteLine($"[AiTeamController] Sending IsComplete=true for '{currentAuthor}'");
        yield return new StreamingMessageDto { Author = currentAuthor, ContentPiece = "", IsComplete = true, ServerSessionId = serverSessionId };
    }

    // ... rest of code
}
```

#### Изменение 1.2: Добавлено логирование

```csharp
Console.WriteLine($"[AiTeamController] Author changed from '{currentAuthor}' to '{message.AuthorName}'");
Console.WriteLine($"[AiTeamController] Now processing: '{currentAuthor}'");
```

---

### 2. Pages/Session.razor

#### Изменение 2.1: Полная переработка ProcessApiStream

```csharp
private async Task ProcessApiStream(HttpResponseMessage response, CancellationToken cancellationToken)
{
    response.EnsureSuccessStatusCode();
    var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

    string currentAuthor = "";
    ChatMessage uiMessage = null;
    bool lastMessageWasComplete = false;  // Новый флаг

    try
    {
        await foreach (var message in System.Text.Json.JsonSerializer.DeserializeAsyncEnumerable<StreamingMessageDto>(stream, ...))
        {
            if (message == null) continue;

            if (!string.IsNullOrEmpty(message.ServerSessionId))
            {
                _serverSessionId = message.ServerSessionId;
            }

            // Явная обработка завершения ответа агента
            if (message.IsComplete && !string.IsNullOrEmpty(message.Author) && message.Author != "System")
            {
                lastMessageWasComplete = true;
                await SaveCurrentSessionAsync();

                if (uiMessage != null && uiMessage.Content.Contains("[DONE]"))
                {
                    _isSessionComplete = true;
                    StateHasChanged();
                    return; // Stop processing - session is complete
                }

                // ... clarification logic

                continue; // Skip further processing for completion messages
            }

            // Обработка смены автора
            if (currentAuthor != message.Author || uiMessage == null)
            {
                if (uiMessage != null && !lastMessageWasComplete)
                {
                    await SaveCurrentSessionAsync();
                }
                
                currentAuthor = message.Author;
                uiMessage = new ChatMessage { Author = currentAuthor, Content = "", IsUser = false };
                _messages.Add(uiMessage);
                lastMessageWasComplete = false;
            }

            if (!string.IsNullOrEmpty(message.ContentPiece))
            {
                uiMessage.Content += message.ContentPiece;
                StateHasChanged();
            }
        }
    }
    catch (OperationCanceledException)
    {
        _messages.Add(new ChatMessage { Author = "System", Content = "[Stopped by user]", IsUser = false });
        StateHasChanged();
    }
}
```

---

### 3. Services/SettingsService.cs

#### Изменение 3.1: Добавлен IsSelected для всех агентов

```csharp
private List<AgentConfig> GetDefaultAgents()
{
    return new List<AgentConfig>
    {
        new AgentConfig {
            Name = "Project Manager",
            IsMandatory = true,
            IsSelected = true  // Добавлено
        },
        new AgentConfig {
            Name = "Product Owner",
            IsSelected = true  // Добавлено
        },
        // ... и так далее для всех агентов
    };
}
```

---

### 4. Api/Services/ResilientWorkflowSelectionStrategy.cs

#### Изменение 4.1: Добавлено логирование выбора агента

```csharp
protected override Task<Agent> SelectAgentAsync(...)
{
    var lastMessage = history.LastOrDefault();

    if (lastMessage != null && lastMessage.Role == AuthorRole.User)
    {
        var mentionedAgent = TryFindMentionedAgent(agents, lastMessage.Content);
        
        if (mentionedAgent != null)
        {
            Console.WriteLine($"[ResilientWorkflow] User mentioned @{mentionedAgent.Name}, selecting directly");
            return Task.FromResult(mentionedAgent);
        }
        
        _currentStep = 0;
        Console.WriteLine($"[ResilientWorkflow] User interruption detected, resetting to step 0 (PM)");
        
        string firstAgent = _workflowOrder[_currentStep];
        _currentStep++;
        return Task.FromResult(GetAgent(agents, firstAgent));
    }

    if (_currentStep >= _workflowOrder.Length)
    {
        Console.WriteLine($"[ResilientWorkflow] Max steps reached, fallback to PM");
        return Task.FromResult(GetAgent(agents, "Project Manager"));
    }

    string nextAgentName = _workflowOrder[_currentStep];
    Console.WriteLine($"[ResilientWorkflow] Selecting agent: {nextAgentName} (step {_currentStep})");
    _currentStep++;

    return Task.FromResult(GetAgent(agents, nextAgentName));
}
```

---

## 🧪 Как проверить исправление

### Тест 1: Стандартное обсуждение

1. Очистите LocalStorage браузера (F12 → Application → Local Storage → Clear)
2. Запустите приложение
3. Введите задачу: "Создать Todo приложение"
4. Нажмите "Start Discussion"

**Ожидаемый результат:**
```
User: Создать Todo приложение
↓
Project Manager: [устанавливает повестку]
↓
Product Owner: [определяет требования]
↓
Architect: [предлагает архитектуру]
↓
Developer: [описывает реализацию]
↓
QA Engineer: [определяет тестирование]
↓
Scrum Master: [создает план спринта]
↓
Project Manager: [финальный SRS + DONE]
```

### Тест 2: Проверка логики переключения

Откройте консоль браузера (F12) и консоль сервера. Вы должны увидеть:

**На сервере:**
```
[ResilientWorkflow] Selecting agent: Project Manager (step 0)
[AiTeamController] Now processing: 'Project Manager'
[AiTeamController] Author changed from 'Project Manager' to 'Product Owner'
[AiTeamController] Sending IsComplete=true for 'Project Manager'
[AiTeamController] Now processing: 'Product Owner'
...
```

**На клиенте (в консоли браузера):** Нет ошибок SSE

### Тест 3: Прерывание пользователем

1. Запустите обсуждение
2. После ответа 2-3 агентов введите: "Добавьте авторизацию через Google"
3. Обсуждение должно продолжиться с Project Manager

**Ожидаемый результат:**
```
[ResilientWorkflow] User interruption detected, resetting to step 0 (PM)
Project Manager: [координирует новые требования]
Product Owner: [анализирует новые user stories]
...
```

### Тест 4: @упоминание

1. Запустите обсуждение
2. Введите: "@Architect, какую базу данных лучше взять?"
3. Должен ответить именно Architect

**Ожидаемый результат:**
```
[ResilientWorkflow] User mentioned @Architect, selecting directly
Architect: [отвечает на вопрос]
[Продолжение workflow с текущей позиции]
```

---

## 📊 Диагностика проблем

### Если чат всё равно останавливается после первого агента

1. **Проверьте консоль сервера:**
   ```
   [ResilientWorkflow] Selecting agent: ???
   [AiTeamController] Now processing: ???
   ```

2. **Проверьте консоль браузера (F12):**
   - Есть ли ошибки SSE?
   - Приходит ли `IsComplete=true` для первого агента?

3. **Проверьте LocalStorage:**
   - Очистите `ai_team_settings` и попробуйте снова
   - Убедитесь, что все агенты имеют `IsSelected = true`

### Если агенты говорят не по порядку

1. Проверьте логи `[ResilientWorkflow]`
2. Убедитесь, что `_currentStep` увеличивается корректно
3. Проверьте, что нет прерываний от пользователя

---

## 🔄 Поток данных после исправления

```
┌─────────────────────────────────────────────────────────────────┐
│                    ИСПРАВЛЕННЫЙ ПОТОК                           │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  1. User отправляет запрос                                     │
│       ↓                                                         │
│  2. Бэкенд: ResilientWorkflow выбирает PM (step 0)             │
│       ↓                                                         │
│  3. Бэкенд: PM генерирует ответ (стриминг)                     │
│       ↓                                                         │
│  4. Бэкенд: Обнаружена смена автора → IsComplete=true для PM   │
│       ↓                                                         │
│  5. Бэкенд: ResilientWorkflow выбирает PO (step 1)             │
│       ↓                                                         │
│  6. Бэкенд: PO генерирует ответ (стриминг)                     │
│       ↓                                                         │
│  7. Фронтенд: Получает IsComplete=true → сохраняет сообщение   │
│       ↓                                                         │
│  8. Фронтенд: Получает смену автора → создаёт новое сообщение  │
│       ↓                                                         │
│  ... цикл повторяется для всех агентов ...                     │
│       ↓                                                         │
│  N. PM пишет [DONE] → Фронтенд завершает сессию                │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

---

## 📝 Чек-лист исправлений

- [x] Бэкенд отправляет `IsComplete=true` после каждого агента
- [x] Фронтенд обрабатывает `IsComplete=true` как сигнал завершения хода агента
- [x] Фронтенд создаёт новое сообщение при смене автора
- [x] Все агенты по умолчанию имеют `IsSelected = true`
- [x] Добавлено логирование на бэкенде для отладки
- [x] ResilientWorkflow корректно переключает агентов
- [x] TokenSavingTerminationStrategy завершает только по [DONE] от PM

---

## 🎯 Итог

После применения всех исправлений чат должен работать корректно:
- ✅ Все агенты выступают по очереди
- ✅ Переключение между агентами плавное
- ✅ Фронтенд отображает всех участников
- ✅ Сессия завершается только после [DONE] от PM
- ✅ Прерывания пользователем обрабатываются корректно

---

**Автор:** AiAgileTeam  
**Дата обновления:** 23.02.2026  
**Версия документа:** 1.0
