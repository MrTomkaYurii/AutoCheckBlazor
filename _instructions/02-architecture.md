# Архітектура

## Структура проекту

```
AutoCheckBlazor/
  Components/
    Pages/          ← Blazor сторінки (студент + викладач)
    Layout/         ← MainLayout, LoginLayout, Sidebar, TopBar
    Shared/         ← LabCard, Badge, Icon, Progress, ScorePill...
    Dialogs/        ← GradeDialog, LabDialog, CommentDialog, TaskDialog...
  Data/
    AppDbContext.cs   ← EF Core контекст + індекси/зв'язки
    Entities.cs       ← всі сутності БД
    DatabaseSeeder.cs ← seed при старті (лаби з MD, групи, демо-студенти)
    LabMdParser.cs    ← парсер MD файлів лаб
  Models/
    Models.cs         ← DTO для UI (не сутності БД)
  Services/
    AppState.cs           ← per-circuit кеш поточного юзера
    AuthService.cs        ← Identity claims → StudentRecord/TeacherRecord
    DbDataService.cs      ← читання даних для UI
    GitHubService.cs      ← GitHub API (гілки, коміти)
    GitBranchService.cs   ← локальний git (для grading pipeline)
    GradingService.cs     ← авто-перевірка (pipeline)
    NotificationService.cs
    CommentService.cs
    LabManagementService.cs
  Grading/
    Pipeline/       ← кроки пайплайну перевірки
    Models/         ← GradingContext, GradingResult
  content/
    labs/           ← MD файли лаб + checks.json
  _instructions/    ← цей каталог
  _design/          ← прототипи UI (виключені з компіляції)
```

## Потік даних

```
Login.razor (/) або Register.razor (/register)
    ↓ form POST → /account/login | /account/register | Google OAuth
ASP.NET Core Identity (cookie, ролі teacher/student у SQLite)
    ↓
MainLayout.OnInitializedAsync()
    ├─ AuthService.EnsureLinkedAsync()  ← пов'язує AppUser.Id → StudentRecord
    └─ AppState.PreloadAsync()          ← кешує дані поточного юзера в скоупі
    ↓
Blazor компоненти
    ├─ AppState.Student / AppState.Teacher  (ім'я, email, github...)
    └─ IDataService / ILabManagementService  (запити до БД)
```

## Blazor Server

- Все рендериться на сервері, SignalR між браузером і сервером.
- Немає WebAssembly — всі C# сервіси доступні напряму з компонентів.
- `AppDbContext` зареєстрований як `Scoped` → один екземпляр на SignalR circuit.
- **Важливо:** для збереження даних використовувати `ExecuteUpdateAsync` або окремо отримувати сутність через той самий `DbContext` що і `SaveChangesAsync`.

## БД

- SQLite файл `autocheck.db` в кореневій теці.
- При старті `DatabaseSeeder.SeedAsync()` перевіряє чи є дані та сідує якщо немає.
- Умови сідування: `if (!await db.Labs.AnyAsync())`, `if (!await db.Students.AnyAsync())` тощо.
- При зміні схеми (нова колонка, новий індекс) — видалити `autocheck.db` і перезапустити.
- У dev-режимі seeder автоматично виявляє застарілу схему і пересоздає БД.

## Автентифікація

- ASP.NET Core Identity в тій самій SQLite базі — без контейнерів і зовнішніх сервісів.
- Google OAuth опційний (Authentication:Google у appsettings.json).
- Деталі: [08-auth](08-auth.md).

## GitHub API

Використовується для UI (без локального git):
- `GitHubService.GetBranchesAsync()` — список гілок репозиторію студента
- `GitHubService.GetBranchCommitInfosAsync()` — коміти гілки з SHA, батьками, автором

Для grading pipeline (локальне клонування):
- `GitBranchService.GetCommitsAsync()` — клонує/фетчить репо, читає `git log`
