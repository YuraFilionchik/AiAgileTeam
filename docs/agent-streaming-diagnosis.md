# Диагностика проблемы "недожидания" ответов агентов

**Дата:** 23 февраля 2026 г.  
**Проблема:** Агенты переключаются слишком быстро, не дожидаясь полного ответа от предыдущего

---

## 📋 Описание проблемы

Из лога видно:
```
Project Manager: [длинный текст с призывом к PO]
System: [Project Manager gives floor to Product Owner...]
Product Owner: Я жду вашего видения, чтобы мы могли двигаться дальше!
System: [Project Manager gives floor to Architect...]
System: [Project Manager gives floor to Developer...]
...
```

**Product Owner** ответил только "Я жду вашего видения..." и сразу переключился на следующего агента.

---

## 🔍 Возможные причины

### Причина 1: Semantic Kernel планирует весь чат заранее

SK's `AgentGroupChat.InvokeStreamingAsync()` может **планировать** всех спикеров вперёд, а не ждать реального ответа от API.

**Диагностика:**
Проверьте логи сервера:
```
[AiTeamController] SK stream chunk: AuthorName='...', Content='...', HasContent=...
```

Если вы видите быструю смену `AuthorName` без контента между ними — это оно.

**Решение:** Использовать `InvokeAsync()` вместо `InvokeStreamingAsync()` для каждого агента отдельно.

---

### Причина 2: Агенты не получают правильный системный промпт

Product Owner может не понимать, что он должен отвечать на вопрос PM, а не задавать свои вопросы.

**Диагностика:**
Проверьте системные промпты агентов в `SettingsService.cs`.

**Решение:**
Убедитесь, что промпты содержат инструкции отвечать на вопросы других агентов.

---

### Причина 3: Стриминг не работает корректно

Бэкенд может отправлять `IsComplete=true` до того, как агент закончил ответ.

**Диагностика:**
Проверьте логи браузера (F12 → Console):
```
[Session.razor] Received message #1: Author='Project Manager', ContentPiece='...', IsComplete=False
[Session.razor] Received message #2: Author='Project Manager', ContentPiece='...', IsComplete=False
...
[Session.razor] Received message #N: Author='Project Manager', ContentPiece='', IsComplete=True
[Session.razor] Agent 'Project Manager' completed their turn
```

Если между `IsComplete=True` и следующим агентом нет задержки — проблема в бэкенде.

---

## 🛠 Пошаговая диагностика

### Шаг 1: Проверьте логи сервера

Ищите:
```
[AiTeamController] SK stream chunk: AuthorName='...', Content='...', HasContent=...
```

**Нормальный паттерн:**
```
SK stream chunk: AuthorName='Project Manager', Content='Отлично!', HasContent=True
SK stream chunk: AuthorName='Project Manager', Content=' Мы получили', HasContent=True
SK stream chunk: AuthorName='Project Manager', Content=' запрос...', HasContent=True
...
SK stream chunk: AuthorName='Product Owner', Content='Принято', HasContent=True
```

**Проблемный паттерн:**
```
SK stream chunk: AuthorName='Project Manager', Content='', HasContent=False
SK stream chunk: AuthorName='Product Owner', Content='', HasContent=False
SK stream chunk: AuthorName='Architect', Content='', HasContent=False
```

---

### Шаг 2: Проверьте логи браузера

Откройте консоль браузера (F12) и ищите:
```
[Session.razor] Received message #...
```

**Нормальный паттерн:**
- Много сообщений с `ContentPiece` и `IsComplete=False`
- Одно сообщение с `IsComplete=True` после завершения ответа агента

**Проблемный паттерн:**
- Мало сообщений с контентом
- Быстрая смена авторов
- `IsComplete=True` приходит слишком рано

---

### Шаг 3: Проверьте длину ответов

В логах браузера:
```
[Session.razor] Finalizing message for 'Project Manager' (length: 1234)
[Session.razor] Finalizing message for 'Product Owner' (length: 56)  ← Слишком мало!
```

Если длина сообщения Product Owner всего 56 символов — это проблема.

---

## 💡 Временное решение

Если проблема в SK `InvokeStreamingAsync`, попробуйте использовать `InvokeAsync` для каждого агента отдельно:

```csharp
// Вместо groupChat.InvokeStreamingAsync()
// Использовать groupChat.InvokeAsync(agent) для каждого агента
```

Это может замедлить работу, но обеспечит корректное получение ответов.

---

## 🧪 Тестовый сценарий

### 1. Запустите сессию с простым запросом

```
User: Создать простое приложение для заметок
```

### 2. Наблюдайте за логами

**Сервер:**
```
[AiTeamController] Creating agents. Total selected: 6
[ResilientWorkflow] SelectAgentAsync called. History count: 1, _currentStep: 0
[ResilientWorkflow] Selecting agent: Project Manager (step 0)
[AiTeamController] Now processing: 'Project Manager'
[AiTeamController] SK stream chunk: AuthorName='Project Manager', Content='...', HasContent=True
...
```

**Браузер:**
```
[Session.razor] Received message #1: Author='Project Manager', ContentPiece='...', IsComplete=False
...
[Session.razor] Agent 'Project Manager' completed their turn
[Session.razor] Switched to author 'Product Owner'
```

### 3. Проверьте ответы агентов

**Project Manager** должен:
- Установить повестку
- Вызвать Product Owner с конкретными вопросами

**Product Owner** должен:
- **Ответить на вопросы PM**
- Описать бизнес-требования
- Создать user stories

Если Product Owner говорит "Я жду вашего видения..." — это проблема!

---

## 📞 Что делать дальше

Если после проверки логов проблема сохраняется:

1. **Скопируйте полные логи сервера и браузера**
2. **Проверьте длину ответов каждого агента**
3. **Убедитесь, что агенты понимают контекст диалога**

Возможно, потребуется:
- Изменить системные промпты агентов
- Использовать `InvokeAsync` вместо `InvokeStreamingAsync`
- Добавить историю диалога в запросы к агентам

---

**Автор:** AiAgileTeam  
**Дата:** 23.02.2026  
**Версия:** 1.0
