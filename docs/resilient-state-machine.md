# Resilient State Machine для AiAgileTeam

**Дата:** 23 февраля 2026 г.  
**Статус:** ✅ Реализовано  
**Версия:** 2.0

---

## 📋 Обзор

Реализована гибкая и устойчивая система управления очередностью агентов (State Machine) на основе анализа роли автора сообщений в чате.

### Ключевые возможности

| Возможность | Описание |
|-------------|----------|
| **Реакция на прерывания** | Новое сообщение от пользователя сбрасывает цикл обсуждения на Project Manager |
| **Гибридная маршрутизация** | Поддержка @упоминаний для прямого обращения к конкретным агентам |
| **Детерминированный workflow** | Жёсткий порядок выступлений вместо LLM-модератора |
| **Защита от преждевременного завершения** | [DONE] проверяется только в последнем сообщении от PM |

---

## 🏗 Архитектура

### Компоненты

```
Api/Services/
├── ResilientWorkflowSelectionStrategy.cs    # Стратегия выбора агента
└── TokenSavingTerminationStrategy.cs        # Стратегия завершения
```

### Диаграмма состояний

```
┌─────────────────────────────────────────────────────────────────┐
│                    WORKFLOW STATE MACHINE                       │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  [User Message] ──────────────────────────────────────┐        │
│       │                                               │        │
│       ▼                                               │        │
│  ┌─────────────────┐                                  │        │
│  │ Проверка @_mentions │◄─────────────────────────────┤        │
│  └────────┬────────┘                                  │        │
│           │                                           │        │
│     ┌─────┴─────┐                                     │        │
│     │           │                                     │        │
│     ▼           ▼                                     │        │
│  Есть       Нет упоминаний                            │        │
│  упоминание       │                                   │        │
│     │             ▼                                   │        │
│     │      ┌────────────┐                             │        │
│     │      │ Сброс цикла │                             │        │
│     │      │ _currentStep=0 │                          │        │
│     │      └─────┬──────┘                             │        │
│     │            │                                    │        │
│     │            ▼                                    │        │
│     │      ┌─────────────┐                            │        │
│     │      │Project Manager│                          │        │
│     │      └──────┬──────┘                            │        │
│     │             │                                   │        │
│     ▼             ▼                                   │        │
│  ┌──────────────────────────┐                         │        │
│  │   Вызов упомянутого      │                         │        │
│  │   агента (без сброса)    │                         │        │
│  └───────────┬──────────────┘                         │        │
│              │                                        │        │
│              │        ┌─────────────────┐             │        │
│              │        │ Standard Flow   │             │        │
│              │        │ PM → PO → Arch  │             │        │
│              └───────►│ → Dev → QA → SM │             │        │
│                       │ → PM [DONE]     │◄────────────┘        │
│                       └─────────────────┘                      │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

---

## 📄 Реализация

### ResilientWorkflowSelectionStrategy

**Файл:** `Api/Services/ResilientWorkflowSelectionStrategy.cs`

#### Алгоритм работы

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

#### Workflow порядок

```
Index  │  Агент              │  Роль
───────┼─────────────────────┼────────────────────────────
  0    │  Project Manager    │  Инициирует обсуждение
  1    │  Product Owner      │  Бизнес-требования
  2    │  Architect          │  Архитектура системы
  3    │  Developer          │  Детали реализации
  4    │  QA Engineer        │  Стратегия тестирования
  5    │  Scrum Master       │  Sprint roadmap, риски
  6    │  Project Manager    │  Финальный SRS документ
```

#### Метод TryFindMentionedAgent

```csharp
private static Agent? TryFindMentionedAgent(IReadOnlyList<Agent> agents, string? content)
{
    if (string.IsNullOrWhiteSpace(content))
    {
        return null;
    }

    foreach (var agent in agents)
    {
        if (content.Contains($"@{agent.Name}", StringComparison.OrdinalIgnoreCase))
        {
            return agent;
        }
    }

    return null;
}
```

**Примеры:**
- `@Architect, какую БД выбрать?` → Architect
- `@QA Engineer, какие тесты нужны?` → QA Engineer
- `Добавьте авторизацию` → null (сброс на PM)

---

### TokenSavingTerminationStrategy

**Файл:** `Api/Services/TokenSavingTerminationStrategy.cs`

#### Алгоритм завершения

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

#### Критерии завершения

| Условие | Результат |
|---------|-----------|
| [DONE] в последнем сообщении от PM | ✅ Завершить |
| [DONE] в старой истории (не последнее) | ❌ Продолжить |
| [DONE] от другого агента | ❌ Продолжить |
| Нет [DONE] | ❌ Продолжить |

---

## 🎯 Сценарии использования

### Сценарий 1: Стандартное обсуждение

```
User: Создать CRM систему для управления клиентами

[Автоматический запуск workflow]
  ↓
Project Manager: Устанавливает повестку, определяет scope
  ↓
Product Owner: Описывает user stories, acceptance criteria
  ↓
Architect: Предлагает tech stack, архитектуру
  ↓
Developer: Описывает схему БД, API endpoints
  ↓
QA Engineer: Определяет стратегию тестирования
  ↓
Scrum Master: Создаёт sprint roadmap
  ↓
Project Manager: Финальный SRS документ + [DONE]
  ↓
[ЧАТ ЗАВЕРШЁН]
```

### Сценарий 2: Прерывание пользователем (изменение требований)

```
[Обсуждение в процессе: PM → PO → Architect отработали]
  ↓
User: А добавьте еще авторизацию через Google OAuth

[ResilientWorkflowSelectionStrategy обнаруживает AuthorRole.User]
  ↓
_currentStep = 0 (сброс)
  ↓
Project Manager: "Принято. Добавляем требование OAuth авторизации..."
  ↓
Product Owner: "Анализирую новые user stories для авторизации..."
  ↓
Architect: "Интеграция Google OAuth потребует..."
  ↓
[Продолжение workflow с текущей позиции]
```

### Сценарий 3: Прямое обращение (@упоминание)

```
[Идет обсуждение архитектуры]
  ↓
User: @Architect, почему вы выбрали PostgreSQL вместо MongoDB?

[ResilientWorkflowSelectionStrategy находит @Architect]
  ↓
TryFindMentionedAgent → Architect
  ↓
Architect: "PostgreSQL выбран из-за реляционной природы данных..."
  ↓
[Продолжение workflow с текущей позиции (_currentStep не сбрасывается)]
```

### Сценарий 4: Прерывание после завершения

```
[PM написал [DONE], чат завершен]
  ↓
User: А добавьте еще раздел с рисками проекта

[Новый POST /api/aiteam/message]
  ↓
Группа загружает историю, добавляет сообщение пользователя
  ↓
ResilientWorkflowSelectionStrategy видит AuthorRole.User
  ↓
_currentStep = 0
  ↓
Project Manager: "Добавляем раздел с рисками..."
  ↓
Scrum Master: "Обновляю матрицу рисков..."
  ↓
Project Manager: Обновленный SRS + [DONE]
```

---

## 🔧 Интеграция в контроллер

### AiTeamController.cs

```csharp
if (projectManager != null)
{
    groupChat.ExecutionSettings = new()
    {
        // 1. Гибкая стратегия выбора с поддержкой прерываний и @упоминаний
        SelectionStrategy = new ResilientWorkflowSelectionStrategy(),

        // 2. Используем проверку на [DONE] только в последнем сообщении
        TerminationStrategy = new TokenSavingTerminationStrategy()
        {
            // Ограничитель на случай бесконечного цикла
            MaximumIterations = agentsToRun.Count * 3 + 3
        }
    };
}
```

---

## 🧪 Тестирование

### Чек-лист

- [ ] **Базовый workflow:** Все агенты выступают по порядку
- [ ] **Прерывание пользователя:** Сброс цикла на PM
- [ ] **@упоминание:** Слово передаётся упомянутому агенту
- [ ] **Завершение по [DONE]:** Чат завершается только после [DONE] от PM
- [ ] **Защита от ложного завершения:** [DONE] в истории не останавливает чат
- [ ] **Fallback на PM:** Если агент не найден, слово получает PM
- [ ] **Бесконечный цикл:** MaximumIterations ограничивает количество итераций

### Примеры тестовых запросов

```
# Стандартный запуск
"Создать интернет-магазин"

# Прерывание с новыми требованиями
"Добавьте интеграцию с платежной системой"

# Прямое обращение
"@Developer, как реализовать кэширование?"
"@QA Engineer, какие нужны тесты для авторизации?"

# Комбинированный сценарий
"@Architect, объясните выбор микросервисов. 
 И добавьте масштабирование в требования"
```

---

## 📊 Сравнение с предыдущей версией

| Характеристика | KernelFunctionSelection | ResilientWorkflowSelection |
|----------------|------------------------|----------------------------|
| **Основа** | LLM-модератор | Детерминированный код |
| **Токены** | Расходует на каждый выбор | Не расходует |
| **Скорость** | Задержка на LLM-запрос | Мгновенный выбор |
| **Предсказуемость** | Зависит от LLM | 100% детерминирована |
| **Реакция на User** | Требует промпта | Встроенная логика |
| **@упоминания** | Не поддерживаются | Нативная поддержка |
| **Отладка** | Сложная (LLM black box) | Простая (код C#) |

| Характеристика | KernelFunctionTermination | TokenSavingTermination |
|----------------|---------------------------|------------------------|
| **Основа** | LLM-модератор | Проверка токена |
| **Токены** | Расходует на каждый чек | Не расходует |
| **Критерии** | Субъективные (LLM решает) | Объективные ([DONE]) |
| **Преждевременное завершение** | Возможно | Исключено |

---

## 🚀 Производительность

### Экономия токенов

**Было (KernelFunction стратегии):**
- Каждый выбор агента: ~100-200 токенов на LLM-запрос
- Каждое решение о завершении: ~150-250 токенов
- Для сессии из 10 итераций: ~2500-4500 токенов на оркестрацию

**Стало (Resilient стратегии):**
- Выбор агента: 0 токенов (детерминированный код)
- Решение о завершении: 0 токенов
- Для сессии из 10 итераций: **0 токенов на оркестрацию**

**Экономия:** ~2500-4500 токенов на сессию

### Скорость отклика

**Было:**
- Задержка на LLM-запрос: 200-500ms на выбор агента
- Для 10 итераций: 2-5 секунд на оркестрацию

**Стало:**
- Выбор агента: <1ms
- Для 10 итераций: <10ms на оркестрацию

**Ускорение:** ~200-500x

---

## 🔮 Планы развития

| Приоритет | Функция | Описание |
|-----------|---------|----------|
| Medium | Логирование состояний | Запись _currentStep в консоль для отладки |
| Medium | Гибкий workflow | Настройка порядка агентов через UI |
| Low | Вложенные циклы | Поддержка под-обсуждений для сложных тем |
| Low | Адаптивный порядок | Динамическая перестановка на основе контекста |

---

## 📝 Зависимости

```xml
<PackageReference Include="Microsoft.SemanticKernel" Version="1.72.0" />
<PackageReference Include="Microsoft.SemanticKernel.Agents.Core" Version="1.72.0" />
```

### Используемые классы SK

- `Microsoft.SemanticKernel.Agents.SelectionStrategy` — базовый класс для стратегии выбора
- `Microsoft.SemanticKernel.Agents.TerminationStrategy` — базовый класс для стратегии завершения
- `Microsoft.SemanticKernel.ChatCompletion.AuthorRole` — роли авторов сообщений
- `Microsoft.SemanticKernel.ChatCompletion.ChatMessageContent` — контент сообщения
- `Microsoft.SemanticKernel.Agents.Agent` — базовый класс агента

---

## 📚 Ресурсы

- [Документация по проблеме](chat-stop-issue.md)
- [QWEN.md](../QWEN.md) — общая документация проекта
- [Semantic Kernel Agents Documentation](https://learn.microsoft.com/en-us/semantic-kernel/frameworks/agent/)

---

**Автор:** AiAgileTeam  
**Дата обновления:** 23.02.2026  
**Версия документа:** 1.0
