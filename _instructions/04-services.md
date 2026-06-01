# Сервіси

## IAuthService / AuthService
Робота з Keycloak claims.
- `EnsureLinkedAsync()` — при першому вході пов'язує Keycloak user з StudentRecord або TeacherRecord
- `GetStudentRecordAsync()` / `GetTeacherRecordAsync()` — по ClaimsPrincipal
- Якщо студент новий — створює StudentRecord + Submission (Locked) для всіх лаб

## IDataService / DbDataService
Читання даних для UI.
- `GetStudentLabsAsync(studentId)` → List<Lab>
- `GetLabDetailAsync(labNumber, studentId)` → LabDetail з TaskResult
- `GetRosterAsync()` → журнал студентів для викладача
- `GetReviewQueueAsync()` → черга на перевірку (Status == Review)
- `GetStatsAsync()` / `GetLabStatsAsync()` → статистика

## ILabManagementService / LabManagementService
CRUD лаб і завдань для викладача.
- `CreateAsync(dto)` / `UpdateAsync(id, dto)` / `DeleteAsync(id)`
- При створенні лаби — автоматично додає Submission (Locked) для всіх студентів
- `PreviewImportAsync()` / `ImportAsync()` — імпорт лаб з MD файлів

## IGradingService / GradingService
Авто-перевірка здачі.

**Поточний стан:** симуляція + заглушка для реального грейдингу.

Флоу:
1. Отримує Submission з БД
2. Якщо є Github URL студента → клонує репо, checkout гілки
3. dotnet build → dotnet test
4. Claude API для аналізу коду (якщо є ApiKey в appsettings)
5. Якщо щось не так → fallback на симуляцію
6. Зберігає TaskResult в БД
7. Оновлює Submission.AutoScore, Status → Review
8. Відправляє Notification студенту

**Конфіг (appsettings.json):**
```json
"Anthropic": { "ApiKey": "", "Model": "claude-haiku-4-5-20251001" },
"Grading": { "WorkRoot": "", "MaxLinesPerFile": 250 }
```

## INotificationService / NotificationService
- `SendAsync(studentId, title, body, type)`
- `GetUnreadAsync(studentId)` / `GetUnreadCountAsync(studentId)`
- `MarkAllReadAsync(studentId)`

## ICommentService / CommentService
Коментарі між викладачем і студентом до конкретної здачі.

## GitHubService
Читання даних з GitHub API.
- `ParseUrl(url)` → (owner, repo)
- `GetBranchesAsync(repoUrl, token?)` → List<GitHubBranch>
- `GetCommitsAsync(repoUrl, branch, token?, count)` → List<GitHubCommit>
- Публічні репо — без токену (60 req/год)
- З токеном — 5000 req/год

## AppState
Singleton в скоупі — кешує поточного користувача.
- `Student` — поточний студент
- `StudentDbId` — Id в БД
- `IsTeacher(user)` — перевірка ролі
- `PreloadAsync(user)` — перезавантажити дані після оновлення профілю
