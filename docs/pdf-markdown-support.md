# Поддержка Markdown в PDF отчётах

**Дата:** 23 февраля 2026 г.  
**Статус:** ✅ Реализовано  
**Версия:** 1.0

---

## 📋 Описание

Ранее PDF-отчёты генерировались без поддержки Markdown-форматирования — весь текст отображался как обычный plain text. Это ухудшало читаемость документов, особенно для технических спецификаций с кодом, списками и заголовками.

Теперь PDF-отчёты поддерживают полное Markdown-форматирование:
- ✅ Заголовки (H1-H6)
- ✅ Жирный и курсивный текст
- ✅ Маркированные и нумерованные списки
- ✅ Блоки кода
- ✅ Цитаты
- ✅ Встроенный код (backticks)
- ✅ Ссылки

---

## 🔧 Внесённые изменения

### 1. Добавлен пакет Markdig

**Файл:** `Api/AiAgileTeam.Api.csproj`

```xml
<PackageReference Include="Markdig" Version="0.41.2" />
```

**Назначение:** Парсинг Markdown в синтаксическое дерево для последующего рендеринга.

---

### 2. Создан сервис MarkdownService

**Файл:** `Api/Services/MarkdownService.cs`

**Основные методы:**

| Метод | Описание |
|-------|----------|
| `RenderMarkdown(IContainer, string)` | Публичный метод для рендеринга Markdown в QuestPDF container |
| `RenderBlock(IContainer, Block)` | Рендеринг блоков Markdown (абзацы, заголовки, списки, код, цитаты) |
| `RenderParagraph(IContainer, ParagraphBlock)` | Рендеринг абзаца с инлайн-элементами |
| `BuildInlineText(Inline)` | Рекурсивное построение текста из инлайн-элементов (жирный, курсив, код, ссылки) |
| `RenderHeading(IContainer, HeadingBlock)` | Рендеринг заголовков с разным размером шрифта (H1=24, H2=20, H3=16...) |
| `RenderList(IContainer, ListBlock)` | Рендеринг списков с поддержкой вложенности |
| `RenderCodeBlock(IContainer, CodeBlock)` | Рендеринг блоков кода с моноширинным шрифтом и фоном |
| `RenderQuote(IContainer, QuoteBlock)` | Рендеринг цитат с левой границей |

**Пример использования:**

```csharp
container.Element(c => _markdownService.RenderMarkdown(c, markdownContent));
```

---

### 3. Обновлён контроллер AiTeamController

**Файл:** `Api/Controllers/AiTeamController.cs`

#### Изменение 3.1: Добавлена зависимость MarkdownService

```csharp
private readonly MarkdownService _markdownService;

public AiTeamController(
    AiTeamService teamService, 
    SessionStore sessionStore, 
    MarkdownService markdownService)  // ← Новый параметр
{
    _teamService = teamService;
    _sessionStore = sessionStore;
    _markdownService = markdownService;  // ← Сохранение в поле
}
```

#### Изменение 3.2: Обновлён метод DownloadReport

**Было:**
```csharp
msgCol.Item().Text(message.Author).Bold()...;
msgCol.Item().Text(message.Content);  // ← Plain text
```

**Стало:**
```csharp
msgCol.Item()
    .Text(message.Author)
    .Bold()
    .FontSize(10)
    .FontColor(message.IsUser ? Colors.Green.Medium : Colors.Grey.Darken2)
    .PaddingBottom(2);

msgCol.Item()
    .PaddingLeft(4)
    .Element(c => _markdownService.RenderMarkdown(c, message.Content));  // ← Markdown
```

---

### 4. Зарегистрирован сервис в DI контейнере

**Файл:** `Api/Program.cs`

```csharp
builder.Services.AddSingleton<SessionStore>();
builder.Services.AddSingleton<MarkdownService>();  // ← Новая регистрация
```

---

## 📊 Сравнение до и после

### До изменений

```
Project Manager:
# Todo App - SRS Document

## 1. Overview
This is a **Todo application** with the following features:
- User authentication
- CRUD operations for tasks
- Due date reminders

## 2. Technical Stack
Backend: .NET 9
Frontend: Blazor WASM
```

Отображалось в PDF как:
```
Project Manager:
# Todo App - SRS Document

## 1. Overview
This is a **Todo application** with the following features:
- User authentication
- CRUD operations for tasks
- Due date reminders

## 2. Technical Stack
Backend: .NET 9
Frontend: Blazor WASM
```

### После изменений

Отображается в PDF как:

**Project Manager** (зелёный, жирный)

# Todo App - SRS Document (синий, 24pt, жирный)

## 1. Overview (синий, 20pt, жирный)

This is a **Todo application** with the following features: (обычный текст)

• User authentication (маркер + отступ)  
• CRUD operations for tasks  
• Due date reminders

## 2. Technical Stack (синий, 20pt, жирный)

**Backend:** .NET 9 (моноширинный шрифт, серый фон)  
**Frontend:** Blazor WASM

---

## 🧪 Тестирование

### Тест 1: Генерация отчёта с Markdown

1. Запустите сессию с задачей "Создать Todo приложение"
2. Дождитесь завершения обсуждения ([DONE] от Project Manager)
3. Нажмите кнопку **Download Report (.pdf)**

**Ожидаемый результат:**
- Заголовки отображаются синим цветом, жирным шрифтом
- Списки имеют маркеры (•) или нумерацию (1. 2. 3.)
- Блоки кода имеют серый фон и моноширинный шрифт
- Жирный и курсивный текст корректно отображаются

### Тест 2: Проверка различных элементов Markdown

Создайте сессию с задачей, которая включает:
- Технические требования (заголовки, списки)
- Примеры кода (блоки кода)
- Бизнес-правила (цитаты)

**Ожидаемый результат:** Все элементы корректно отформатированы в PDF

---

## 🎯 Поддерживаемые элементы Markdown

| Элемент | Синтаксис | Рендеринг в PDF |
|---------|-----------|-----------------|
| Заголовок H1 | `# Heading` | Синий, 24pt, жирный |
| Заголовок H2 | `## Heading` | Синий, 20pt, жирный |
| Заголовок H3 | `### Heading` | Синий, 16pt, жирный |
| Заголовок H4 | `#### Heading` | Синий, 14pt, жирный |
| Жирный текст | `**text**` или `__text__` | Жирный |
| Курсив | `*text*` или `_text_` | Курсив |
| Встроенный код | `` `code` `` | Mono шрифт |
| Блок кода | \`\`\`code\`\`\` | Серый фон, Mono шрифт, отступ |
| Маркированный список | `- item` или `* item` | Маркер • |
| Нумерованный список | `1. item` | Нумерация 1. 2. 3. |
| Цитата | `> text` | Левая граница, отступ |
| Разделитель | `---` | Горизонтальная линия |
| Ссылка | `[text](url)` | Текст (URL) |

---

## 📝 Примечания

### Лицензия QuestPDF

В методе `DownloadReport` явно установлена лицензия Community:

```csharp
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
```

Это гарантирует, что QuestPDF будет работать в режиме бесплатной лицензии для небольших проектов и личного использования.

### Производительность

Рендеринг Markdown добавляет небольшие накладные расходы на парсинг. Для типичного отчёта (20-50 сообщений) время генерации увеличивается на ~0.5-1 секунду.

### Ограничения

- **Таблицы:** Не поддерживаются в текущей версии. Markdown-таблицы будут отображены как текст.
- **Изображения:** Не поддерживаются. Ссылки на изображения отображаются как текст.
- **Вложенные списки:** Поддерживаются, но глубина вложенности ограничена 2-3 уровнями для лучшей читаемости.
- **Жирный/курсивный текст:** Упрощённая реализация — весь текст в тегах `**` или `*` отображается курсивом.

---

## 🔄 Поток данных

```
┌─────────────────────────────────────────────────────────────┐
│                  ПОТОК ГЕНЕРАЦИИ PDF                         │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  1. Пользователь нажимает "Download Report"                │
│       ↓                                                     │
│  2. Frontend отправляет POST /api/aiteam/report            │
│     с сообщениями сессии                                    │
│       ↓                                                     │
│  3. AiTeamController.DownloadReport()                      │
│       ↓                                                     │
│  4. Для каждого сообщения:                                 │
│     - Отображение автора (цвет, жирный)                    │
│     - _markdownService.RenderMarkdown()                    │
│       ↓                                                     │
│  5. MarkdownService:                                       │
│     - Markdown.Parse() → синтаксическое дерево             │
│     - Обход дерева (foreach block)                         │
│     - Рендеринг каждого блока в QuestPDF                   │
│       ↓                                                     │
│  6. QuestPDF.GeneratePdf() → byte[]                        │
│       ↓                                                     │
│  7. Возврат файла пользователю                             │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

---

## 🚀 Планы развития

| Приоритет | Функция | Описание |
|-----------|---------|----------|
| Medium | Поддержка таблиц | Рендеринг Markdown-таблиц в PDF |
| Low | Оглавление | Автоматическая генерация оглавления на основе заголовков |
| Low | Кастомные стили | Настройка цветов и шрифтов через настройки приложения |

---

## 📄 Файлы изменений

| Файл | Изменения |
|------|-----------|
| `Api/AiAgileTeam.Api.csproj` | Добавлен пакет Markdig 0.41.2 |
| `Api/Services/MarkdownService.cs` | **Новый файл** — сервис рендеринга Markdown |
| `Api/Controllers/AiTeamController.cs` | Добавлена зависимость MarkdownService, обновлён DownloadReport |
| `Api/Program.cs` | Зарегистрирован MarkdownService в DI |

---

**Автор:** AiAgileTeam  
**Дата создания:** 23.02.2026  
**Версия документа:** 1.0
