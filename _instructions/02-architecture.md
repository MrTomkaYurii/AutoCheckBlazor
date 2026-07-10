# Архітектура

## Структура проекту

```
AutoCheckBlazor/
  Components/
    Pages/          ← Blazor сторінки (студент + викладач)
    Layout/         ← MainLayout, Sidebar, TopBar…
    Shared/         ← LabCard, Badge, Icon, Progress, ScorePill…
    Dialogs/        ← GradeDialog, LabDialog, CommentDialog, TaskDialog…
  Infrastructure/
    Data/
      AppDbContext.cs      ← EF Core + Identity контекст, індекси/зв'язки
      LabEntities.cs       ← LabDef, LabTask, GroupRecord
      PeopleEntities.cs    ← AppUser, StudentRecord, TeacherRecord, UserLink
      SubmissionEntities.cs← Submission, TaskResult, DiffEntry, LabComment
      AuditEntities.cs     ← GradeAudit, Notification
      DatabaseSeeder.cs    ← seed при старті (лаби з MD, групи, демо-студенти, тест-акаунти)
    Helpers/
      LabMdParser.cs   ← парсер instructions.md
      Scoring.cs       ← зважена за складністю оцінка
      BackupHelper.cs  ← гарячий бекап SQLite (VACUUM INTO) + ротація
      GitBackupSync.cs ← опційне дзеркалювання бекапів у git-remote
      KyivTime.cs      ← UTC → Europe/Kyiv для відображення
      StatusMeta.cs / FeedbackJson.cs / GradingPaths.cs
  Models/
    CommitModels.cs / LabModels.cs / PeopleModels.cs / RosterModels.cs  ← UI DTO (не сутності БД)
  Services/           ← бізнес-логіка (див. 04-services)
  content/
    labs/             ← instructions.md + checks.json (+ lab-01 tests/)
  wwwroot/            ← css, статичні файли
  deploy/             ← Docker/compose для production
  _instructions/      ← ця документація
```

> Історична примітка: раніше існував каталог `Grading/` з кроками-заглушками
> (CloneStep/BuildStep/…) і сервіс `GitBranchService`. Їх **немає** — уся логіка
> перевірки живе в `GradingService`.

## Потік даних

```
Login.razor (/) або Register.razor (/register)
    ↓ form POST → /account/login | /account/register | Google OAuth
ASP.NET Core Identity (cookie, ролі teacher/student у SQLite)
    ↓
MainLayout.OnInitializedAsync()
    ├─ AuthService.EnsureLinkedAsync()  ← пов'язує AppUser.Id → StudentRecord/TeacherRecord
    └─ AppState.PreloadAsync()          ← кешує дані поточного юзера в circuit-скоупі
    ↓
Blazor компоненти
    ├─ AppState.Student / AppState.Teacher
    └─ IDataService / ILabManagementService / IGradingService  (робота з БД + grading)
```

## Blazor Server

- Рендер на сервері, SignalR між браузером і сервером; C#-сервіси доступні напряму.
- **DbContext через фабрику:** `AddDbContextFactory<AppDbContext>` — кожна логічна
  операція бере свій короткоживучий контекст (інакше довгий grading і фонові таймери
  «зіштовхнулись» би на спільному scoped-контексті → *"A second operation started"*).
  Додатково зареєстровано scoped-shim `AppDbContext` (резолвиться з фабрики) — лише
  щоб ASP.NET Identity працював у per-request auth-ендпоінтах.
- Для збереження з UI використовується `ExecuteUpdateAsync` (прямий SQL UPDATE),
  щоб не конфліктувати з відстежуваними сутностями (див. 09-decisions).

## Фонові сервіси (HostedService)

- `DeadlineReminderService` — щогодини: нагадування про дедлайн (<24 год) тим, хто не здав.
- `BackupService` — щоденний бекап SQLite (VACUUM INTO) + ротація + опційне git-дзеркало.
- `RepoCleanupService` — щоденне видалення кешованих клонів репо, що простоюють > `Grading:RepoRetentionDays`.

## HTTP-клієнти

Іменовані `IHttpClientFactory`-клієнти (уникають socket exhaustion):
`gemini` (timeout 120с), `gemini-health` (10с), `github`.

## БД

- SQLite `autocheck.db` у кореневій теці; схема через `EnsureCreated` (без міграцій).
- При старті `DatabaseSeeder.SeedAsync()` сідує лаби/групи/демо-дані, якщо порожньо.
- **Зміна схеми** (нова колонка/індекс) → видалити `autocheck.db` і перезапустити.

## Автентифікація

- ASP.NET Core Identity у тій самій SQLite базі; Google OAuth опційний.
- Деталі: [08-auth](08-auth.md).

## GitHub

- **UI:** `GitHubService` — REST API для гілок/комітів (без локального git).
- **Grading:** `GradingService` — локальний `git clone/fetch/show` у `Grading:WorkRoot`.
