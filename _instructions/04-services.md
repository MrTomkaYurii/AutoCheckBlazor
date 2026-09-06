# Сервіси

Усі — в `Services/`. Grading/plagiarism/notifications беруть `IDbContextFactory`
(власний короткоживучий контекст), бо працюють поза або довше за UI-circuit.

## AppState (scoped)
Per-circuit кеш поточного юзера.
- `Student` / `Teacher` — дані поточного користувача; `StudentDbId` / `TeacherDbId`
- `IsTeacher(user)` — перевірка ролі по claim
- `PreloadAsync(user)` — перезавантажити після оновлення профілю

## IAuthService / AuthService
Identity claims ↔ доменні записи.
- `EnsureLinkedAsync(user)` — у MainLayout при кожному вході: шукає UserLink по UserId;
  якщо запис має старий UserId → **оновлює** його; якщо студента нема → створює
  StudentRecord + `Submission(Locked)` для всіх лаб. Race (SSR+SignalR) → `catch DbUpdateException` → `ChangeTracker.Clear()`.
- `LinkStudentAsync(appUser, group)` / `LinkTeacherAsync(appUser)` — при реєстрації/Google/сідуванні
- `GetStudentRecordAsync` / `GetTeacherRecordAsync` / `GetSub(user)`

## IDataService / DbDataService
Читання для UI.
- `GetStudentLabsAsync` → `List<Lab>`; `GetLabDetailAsync` → `LabDetail`
- `GetRosterAsync` → журнал; `GetReviewQueueAsync` → черга (Status == Review)
- `GetStatsAsync` / `GetLabStatsAsync` → аналітика

## ILabManagementService / LabManagementService
CRUD лаб/завдань. `CreateAsync`/`UpdateAsync`/`DeleteAsync`; створення лаби додає
`Submission(Locked)` усім студентам; `PreviewImportAsync`/`ImportAsync` (LabMdParser).

## IGradingService / GradingService  ← ядро перевірки
Повністю реалізований pipeline (git clone + Gemini). Детально: [05-grading-pipeline](05-grading-pipeline.md).
Коротко `RunAsync(submissionId, studentId)`:
1. Pre-flight: ліміт спроб, дедлайн (сервером), денна квота Gemini, наявність ApiKey
2. Guard підміни репо (`FindSharedRepoAsync`) → hard-fail
3. Черга (`GradingQueueService.EnterAsync`) — одна перевірка за раз
4. `git clone/fetch/reset` репо студента в `WorkRoot` (private-репо — через розшифрований токен)
5. Витяг коду per-task: `git show {sha}` за маппінгом або евристика по назві файлу
6. Plagiarism-гейт (`FindExactMatchAsync`) — до Gemini, квота не витрачається
7. **Один** batch-запит до Gemini на всі завдання → done/issues/analysis
8. score = done/(done+issues); Auto = `Scoring.Weighted`; статус Review/Rejected (поріг 50)
9. Зберігає TaskResult+DiffLines, нотифікація студенту

Системні збої (Gemini впав / некоректна відповідь) — **кидають**, спроба не витрачається.

## GitHubService
GitHub **REST API** (не клонує). `ParseUrl`, `GetBranchesAsync`, `GetBranchCommitsAsync`,
`GetBranchCommitInfosAsync` (повні SHA + parents[] для git-графу), `GetCommitTreeAsync`.
Без токену 60 req/год, з токеном 5000.

## PlagiarismService
Пошук збігів коду між студентами (`IDbContextFactory`).
- `CheckLabAsync(labNumber, threshold)` → `List<PlagPair>` — попарні збіги для звіту викладача
- `FindSharedRepoAsync(studentId, repoUrl)` → `RepoConflict?` — інший студент із тим самим репо
- `FindExactMatchAsync(labId, studentId, lines)` → `ExactMatch?` — повний збіг рядків із уже перевіреною роботою (containment)

## CodeSimilarityService
Деталізація схожості для UI викладача.
- `FindSimilarAsync(labNumber, studentId, topN)` → `List<SimilarStudent>` (percent, shared fragments)
- `GetOverlapAsync(labNumber, studentId, otherStudentId)` → `CodeOverlap?` — порядкове порівняння з підсвіткою

## GradingQueueService (singleton)
Серіалізує перевірки: `EnterAsync(progress, ct)` → `IDisposable` (звільняє слот у Dispose).
Тим, хто чекає, репортить позицію в черзі.

## GeminiQuotaService (singleton)
Денний ліміт викликів Gemini. `RecordCall()`, `IsExhausted`, `DailyLimit`, `Remaining`, `TodayCount` (скидається щодня).

## TokenProtector (singleton)
Шифрування GitHub-токенів через DataProtection: `Protect(plain)` / `Unprotect(stored)`.

## NotificationService
In-app + email нотифікації (через `EmailService`).
`SendAsync(studentId, title, body, type, emailBody?)` — `emailBody` необовʼязковий: якщо заданий, у лист іде він (in-app завжди `body`), щоб тримати рядок сповіщення коротким, а лист — повнішим. `GetUnreadAsync`, `GetUnreadCountAsync`, `MarkReadAsync`, `MarkAllReadAsync`.
Спільний блок листів — `EmailText.Greeting(first, last)` («Шановний(-а) …!» / «Доброго дня!»). Формальні листи: дедлайн (DeadlineReminderService) і «Lab зарахована» (GradeDialog — № + назва + фінал/авто/захист).

## TeacherNotificationService (singleton)
In-memory стрічка подій для викладача: `Add(title, body, type)`, `GetAll`, `MarkAllRead`.

## EmailService (singleton)
SMTP-розсилка; `Enabled` лише коли задано `Email:SmtpHost`.
`SendAsync(to, subject, body, attachments?)` → `bool` (чи реально надіслано — форма звернень
показує чесний статус). Підтримує вкладення (`EmailAttachment`, напр. скріншот).

## ICommentService / CommentService
Коментарі викладач↔студент до здачі/завдання. `GetForSubmissionAsync`, `AddAsync`, `GetAllThreadsAsync`, `DeleteAsync`.

## Фонові сервіси (HostedService)
- **DeadlineReminderService** — щогодини: нагадування про дедлайн у двох вікнах (≈72 год «за 3 дні» і ≈24 год «завтра») тим, хто не здав; кожне вікно one-time по Title. In-app — короткий рядок, email — офіційний лист зі зверненням на ім'я з профілю (fallback «Доброго дня!») через `NotificationService.SendAsync(..., emailBody:)`.
- **BackupService** — щоденний бекап SQLite (`BackupHelper`, VACUUM INTO) + ротація + git-дзеркало.
- **RepoCleanupService** — щоденне видалення клонів у `WorkRoot`, що простоюють > `Grading:RepoRetentionDays` (default 7); `PrepareRepoAsync` штампує mtime при кожному використанні.

## Профіль — збереження через ExecuteUpdateAsync
`Profile.razor` зберігає прямим SQL UPDATE (`ExecuteUpdateAsync`) — обходить change tracker,
уникає конфліктів зі спільним контекстом (див. [09-decisions](09-decisions.md)).
