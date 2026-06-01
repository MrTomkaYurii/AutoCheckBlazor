# Архітектура

## Структура проекту

```
AutoCheckBlazor/
  Components/
    Pages/          ← Blazor сторінки (студент + викладач)
    Layout/         ← MainLayout, LoginLayout, Sidebar, TopBar
    Shared/         ← LabCard, Badge, Icon, Progress...
    Dialogs/        ← GradeDialog, LabDialog, CommentDialog...
  Data/
    AppDbContext.cs ← EF Core контекст
    Entities.cs     ← всі сутності БД
    DatabaseSeeder.cs ← початкові дані при старті
    LabMdParser.cs  ← парсер MD файлів лаб
  Models/
    Models.cs       ← DTO моделі для UI (не сутності БД)
  Services/
    *.cs            ← бізнес-логіка
  Grading/
    Pipeline/       ← кроки пайплайну перевірки
    Models/         ← GradingContext, GradingResult
  content/
    labs/           ← MD файли лаб + checks.json
  keycloak/
    themes/         ← кастомна тема (login.ftl, register.ftl)
    realm-autocheck.json ← конфіг realm
  _instructions/    ← цей каталог
  _design/          ← прототипи (виключені з компіляції)
```

## Потік даних

```
Keycloak (SSO)
    ↓ OIDC
ASP.NET Core Authentication
    ↓
AuthService.EnsureLinkedAsync()   ← пов'язує Keycloak user з StudentRecord/TeacherRecord
    ↓
AppState                          ← кешує поточного студента/викладача в скоупі
    ↓
Blazor компоненти                 ← читають з AppState та сервісів
```

## Blazor Server

Все рендериться на сервері. SignalR з'єднання між браузером і сервером.
Немає WebAssembly. Всі C# сервіси доступні напряму з компонентів.

## БД

SQLite файл `autocheck.db` в кореневій теці.
При старті `DatabaseSeeder` перевіряє чи є дані і сідує якщо немає.
При зміні схеми — видалити `autocheck.db` і перезапустити.

## Keycloak

Запускається через `docker compose up -d`.
Realm `autocheck` імпортується з `keycloak/realm-autocheck.json`.
Кастомна тема `autocheck` монтується як volume.
