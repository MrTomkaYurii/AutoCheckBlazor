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
    AuthService.cs        ← Keycloak → StudentRecord/TeacherRecord
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
  keycloak/
    themes/autocheck/login/  ← кастомна тема (FTL файли + CSS)
    realm-autocheck.json     ← конфіг realm (імпортується при старті)
  _instructions/    ← цей каталог
  _design/          ← прототипи UI (виключені з компіляції)
```

## Потік даних

```
Keycloak (SSO) :8080
    ↓ OIDC / redirect
ASP.NET Core Authentication (cookie + OIDC middleware)
    ↓
MainLayout.OnInitializedAsync()
    ├─ AuthService.EnsureLinkedAsync()  ← пов'язує Keycloak sub → StudentRecord
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

## Keycloak

- `docker compose up -d` — піднімає Keycloak на :8080.
- Realm `autocheck` імпортується з `keycloak/realm-autocheck.json` при першому старті.
- Кастомна тема `autocheck` монтується як Docker volume → зміни в FTL файлах підхоплюються одразу.
- `cacheThemes=false` і `cacheTemplates=false` в `theme.properties`.

## GitHub API

Використовується для UI (без локального git):
- `GitHubService.GetBranchesAsync()` — список гілок репозиторію студента
- `GitHubService.GetBranchCommitInfosAsync()` — коміти гілки з SHA, батьками, автором

Для grading pipeline (локальне клонування):
- `GitBranchService.GetCommitsAsync()` — клонує/фетчить репо, читає `git log`
