# Отладка проблемы с порядком агентов

**Дата:** 23 февраля 2026 г.  
**Проблема:** Первым выступает Scrum Master вместо Project Manager

---

## 🔍 Диагностика

### Шаг 1: Проверьте консоль сервера

После запуска сессии вы должны увидеть следующие логи:

```
[AiTeamController] Creating agents. Total selected: 6
[AiTeamController]   - Project Manager (IsSelected=True, IsMandatory=True)
[AiTeamController]   - Product Owner (IsSelected=True, IsMandatory=False)
[AiTeamController]   - Architect (IsSelected=True, IsMandatory=False)
[AiTeamController]   - Developer (IsSelected=True, IsMandatory=False)
[AiTeamController]   - QA Engineer (IsSelected=True, IsMandatory=False)
[AiTeamController]   - Scrum Master (IsSelected=True, IsMandatory=False)
[AiTeamController] ExecutionSettings configured. Agents count: 6
[ResilientWorkflow] Reset() called, _currentStep set to 0
[ResilientWorkflow] SelectAgentAsync called. History count: 1, _currentStep: 0
[ResilientWorkflow] Available agents: Project Manager, Product Owner, Architect, Developer, QA Engineer, Scrum Master
[ResilientWorkflow] Last message: Role=User, Author=N/A
[ResilientWorkflow] Selecting agent: Project Manager (step 0)
[AiTeamController] Now processing: 'Project Manager'
```

### Шаг 2: Проверьте LocalStorage браузера

1. Откройте DevTools (F12)
2. Перейдите в **Application** → **Local Storage**
3. Найдите ключ `ai_team_settings`
4. Проверьте значение `Agents`:

```json
{
  "Agents": [
    {
      "Name": "Project Manager",
      "IsSelected": true,
      "IsMandatory": true
    },
    {
      "Name": "Product Owner",
      "IsSelected": true,
      "IsMandatory": false
    },
    ...
  ]
}
```

**Все агенты должны иметь `IsSelected: true`!**

### Шаг 3: Проверьте порядок агентов в настройках

На главной странице (`/`) проверьте таблицу агентов:

| Include | Name | Role |
|---------|------|------|
| ☑ | Project Manager | Project Manager |
| ☑ | Product Owner | Product Owner |
| ☑ | Architect | Architect |
| ☑ | Developer | Developer |
| ☑ | QA Engineer | QA |
| ☑ | Scrum Master | Scrum Master |

**Все галочки должны быть установлены!**

---

## 🛠 Возможные проблемы и решения

### Проблема 1: Первым выступает Scrum Master

**Логи:**
```
[ResilientWorkflow] Selecting agent: Scrum Master (step 5)
```

**Причина:** `_currentStep` не был сброшен в 0.

**Решение:**
1. Очистите LocalStorage браузера
2. Перезагрузите страницу
3. Запустите сессию заново

---

### Проблема 2: Project Manager не найден

**Логи:**
```
[AiTeamController] WARNING: Project Manager not found in selected agents!
[ResilientWorkflow] GetAgent: Agent 'Project Manager' not found. Available: Scrum Master, ...
[ResilientWorkflow] GetAgent: PM not found either, using fallback: Scrum Master
```

**Причина:** Project Manager не был добавлен в список агентов или имеет `IsSelected = false`.

**Решение:**
1. Проверьте LocalStorage — убедитесь, что PM есть в списке
2. Проверьте, что `IsSelected = true` у PM
3. Если проблема сохраняется, очистите LocalStorage и попробуйте снова

---

### Проблема 3: Только один агент в списке

**Логи:**
```
[AiTeamController] Creating agents. Total selected: 1
[AiTeamController]   - Scrum Master (IsSelected=True, IsMandatory=False)
```

**Причина:** Неверные настройки в LocalStorage.

**Решение:**
1. Очистите LocalStorage полностью
2. Обновите страницу — настройки должны сброситься к значениям по умолчанию
3. Проверьте, что все 6 агентов отображаются на главной странице

---

### Проблема 4: Стратегия не сбрасывается

**Симптомы:** После первой сессии следующие сессии начинаются не с PM.

**Логи:**
```
[ResilientWorkflow] SelectAgentAsync called. History count: 1, _currentStep: 5
```

**Причина:** Стратегия переиспользуется между сессиями.

**Решение:** Убедитесь, что создаётся новая стратегия для каждой сессии:

```csharp
// В AiTeamController.cs
var selectionStrategy = new ResilientWorkflowSelectionStrategy();
groupChat.ExecutionSettings = new()
{
    SelectionStrategy = selectionStrategy,
    ...
};
```

---

## 🧪 Тестовый сценарий

### 1. Очистка LocalStorage

```javascript
// В консоли браузера (F12)
localStorage.clear();
location.reload();
```

### 2. Проверка настроек

```javascript
// В консоли браузера
const settings = JSON.parse(localStorage.getItem('ai_team_settings'));
console.log('Agents:', settings.Agents.map(a => `${a.Name}: IsSelected=${a.IsSelected}`));
```

**Ожидаемый вывод:**
```
Agents: [
  "Project Manager: IsSelected=true",
  "Product Owner: IsSelected=true",
  "Architect: IsSelected=true",
  "Developer: IsSelected=true",
  "QA Engineer: IsSelected=true",
  "Scrum Master: IsSelected=true"
]
```

### 3. Запуск сессии

1. Введите задачу: "Создать Todo приложение"
2. Нажмите "Start Session"
3. Откройте консоль сервера

### 4. Проверка логов

**Ожидаемые логи:**
```
[AiTeamController] Creating agents. Total selected: 6
[AiTeamController]   - Project Manager (IsSelected=True, IsMandatory=True)
[AiTeamController]   - Product Owner (IsSelected=True, IsMandatory=False)
[AiTeamController]   - Architect (IsSelected=True, IsMandatory=False)
[AiTeamController]   - Developer (IsSelected=True, IsMandatory=False)
[AiTeamController]   - QA Engineer (IsSelected=True, IsMandatory=False)
[AiTeamController]   - Scrum Master (IsSelected=True, IsMandatory=False)
[AiTeamController] ExecutionSettings configured. Agents count: 6
[ResilientWorkflow] SelectAgentAsync called. History count: 1, _currentStep: 0
[ResilientWorkflow] Available agents: Project Manager, Product Owner, Architect, Developer, QA Engineer, Scrum Master
[ResilientWorkflow] Last message: Role=User, Author=N/A
[ResilientWorkflow] Selecting agent: Project Manager (step 0)
[AiTeamController] Now processing: 'Project Manager'
```

---

## 📋 Чек-лист успешной настройки

- [ ] Все 6 агентов имеют `IsSelected = true`
- [ ] Project Manager имеет `IsMandatory = true`
- [ ] LocalStorage содержит ключ `ai_team_settings`
- [ ] В консоли сервера видно: `Creating agents. Total selected: 6`
- [ ] Первым выбирается Project Manager: `Selecting agent: Project Manager (step 0)`
- [ ] Агенты выступают по порядку: PM → PO → Architect → Developer → QA → SM → PM
- [ ] Сессия завершается после [DONE] от PM

---

## 🔧 Быстрое исправление

Если ничего не помогает, выполните полное сбрасывание:

### 1. Очистка LocalStorage браузера

```javascript
localStorage.clear();
sessionStorage.clear();
location.reload();
```

### 2. Перезапуск сервера

```bash
# Остановите сервер (Ctrl+C)
dotnet run --project Api
```

### 3. Запуск сессии

1. Откройте `http://localhost:5139` (или ваш порт)
2. Проверьте, что все 6 агентов отображаются на главной
3. Введите задачу
4. Нажмите "Start Session"
5. Проверьте консоль сервера

---

## 📞 Контакты

Если проблема сохраняется, проверьте:

1. **Версию Semantic Kernel:** Должна быть 1.72.0
2. **Версию .NET:** Должна быть 9.0
3. **Логи сервера:** Полный вывод консоли
4. **Логи клиента:** Консоль браузера (F12)

---

**Автор:** AiAgileTeam  
**Дата:** 23.02.2026  
**Версия:** 1.0
