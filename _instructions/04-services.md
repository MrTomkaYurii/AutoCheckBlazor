# Сервіси

## AppState (Services/AppState.cs)
Per-circuit singleton. Кешує поточного юзера.
- `Student` — дані поточного студента (з StudentRecord)
- `Teacher` — дані поточного викладача
- `StudentDbId` / `TeacherDbId` — Id в БД
- `IsTeacher(user)` — перевірка ролі по claim
- `PreloadAsync(user)` — перезавантажити після оновлення профілю

## IAuthService / AuthService (Services/AuthService.cs)
Робота з Identity claims та зв'язком з БД.

**`EnsureLinkedAsync(user)`** — викликається в MainLayout при кожному вході (страхувальна сітка;
зазвичай лінк уже створений при реєстрації):
1. Шукає UserLink по `UserId`
2. Якщо знайдено — виходить (вже зв'язано)
3. Якщо ні — бере AppUser з БД, лінкує як викладача або студента (по email)
4. Якщо запис вже має UserLink зі старим UserId → **оновлює** UserId і Email
5. Якщо студента немає → створює новий StudentRecord + Submission(Locked) для всіх лаб
6. Зберігає UserLink
7. Race condition (подвійний рендер SSR+SignalR): catch DbUpdateException → `ChangeTracker.Clear()`

**`LinkStudentAsync(appUser, group)`** / **`LinkTeacherAsync(appUser)`** — створення зв'язку
при реєстрації / Google-вході / сідуванні.

**`GetStudentRecordAsync(user)`** / **`GetTeacherRecordAsync(user)`** — завантаження запису по UserId.

**`GetSub(user)`** — витягує Id користувача (ClaimTypes.NameIdentifier).

## IDataService / DbDataService (Services/DbDataService.cs)
Читання даних для UI-компонентів.
- `GetStudentLabsAsync(studentId)` → `List<Lab>`
- `GetLabDetailAsync(labNumber, studentId)` → `LabDetail` з TaskResult і TaskItems
- `GetRosterAsync()` → журнал студентів для викладача
- `GetReviewQueueAsync()` → черга на перевірку (Status == Review)
- `GetStatsAsync()` / `GetLabStatsAsync()` → статистика для Monitoring

## ILabManagementService / LabManagementService
CRUD лаб і завдань для викладача.
- `CreateAsync(dto)` / `UpdateAsync(id, dto)` / `DeleteAsync(id)`
- При створенні лаби → автоматично додає `Submission(Locked)` для всіх студентів
- `PreviewImportAsync()` / `ImportAsync()` → імпорт з MD файлів через LabMdParser

## GitHubService (Services/GitHubService.cs)
Читання даних з **GitHub REST API**. Не клонує репо.

```
ParseUrl(url)                      → (owner, repo)? — нормалізує різні формати URL
GetBranchesAsync(repoUrl, token?)  → List<GitHubBranch> — всі гілки з пагінацією
GetBranchCommitsAsync(...)         → List<GitHubCommit> — коміти (спрощена модель)
GetBranchCommitInfosAsync(...)     → List<CommitInfo>   ← використовується в Lab.razor
GetCommitTreeAsync(...)            → List<GitHubCommitNode> — для дерева гілок
```

`GetBranchCommitInfosAsync` повертає `CommitInfo` з **повними SHA** і **parents[]** — необхідно для побудови git-графу. Без токену: 60 req/год, з токеном: 5000 req/год.

## IGitBranchService / GitBranchService (Services/GitBranchService.cs)
Локальні git-операції для **grading pipeline** (не для UI).
- `GetCommitsAsync(repoUrl, branch)` → клонує/фетчить репо, читає `git log`
- Fallback: якщо git недоступний → `GenerateMockCommits()` (7 демо-комітів)
- Репо кешується в `WorkRoot` (temp або `Grading:WorkRoot` з конфігу)

## IGradingService / GradingService
Авто-перевірка здачі. **Pipeline кроки — заглушки**, реальна логіка не реалізована.

Флоу:
1. Отримує Submission з БД
2. Клонує репо (GitBranchService), checkout коміту
3. `dotnet build` → `dotnet test` (TODO: реалізувати)
4. Claude API для аналізу (якщо є ApiKey)
5. Fallback на симуляцію
6. Зберігає TaskResult, оновлює Submission.Status → Review
7. Notification студенту

## INotificationService / NotificationService
- `SendAsync(studentId, title, body, type)`
- `GetUnreadAsync(studentId)` / `GetUnreadCountAsync(studentId)`
- `MarkAllReadAsync(studentId)`

## ICommentService / CommentService
Коментарі між викладачем і студентом до конкретної здачі.

## Profile — збереження даних
В `Profile.razor` для збереження використовується **`ExecuteUpdateAsync`** (прямий SQL UPDATE) — минає EF change tracker, уникає конфліктів зі спільним `AppDbContext` у circuit:

```csharp
await Db.Students
    .Where(s => s.Id == studentId)
    .ExecuteUpdateAsync(s => s
        .SetProperty(x => x.FirstName, _firstName.Trim())
        .SetProperty(x => x.Email, _email.Trim())
        ...);
```
