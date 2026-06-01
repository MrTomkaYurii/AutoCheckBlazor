# Прийняті рішення

## Архітектурні

### Чому Blazor Server а не WebAssembly
Прямий доступ до БД і сервісів без HTTP API шару. Простіша авторизація через стандартний ASP.NET Core middleware.

### Чому SQLite
Навчальний проект, не потребує окремого DB сервера. При переході на production — замінити на PostgreSQL одним рядком у `Program.cs`.

### Чому LabStatus — int а не enum в БД
EF Core зберігає enum як int за замовчуванням. Конвертація: `(LabStatus)sub.Status` і `(int)LabStatus.Review`.

### Чому ExecuteUpdateAsync для збереження профілю
`AppDbContext` scoped до SignalR circuit. `EnsureLinkedAsync` (в AuthService) і `Profile.razor` використовують один і той самий `DbContext`. Якщо AuthService завантажив сутність, вона відстежується його контекстом — `SaveChangesAsync` з іншого місця не зберігатиме зміни. `ExecuteUpdateAsync` — прямий SQL UPDATE, обходить change tracker повністю.

### Чому GitHub API для комітів у UI, а не git clone
- git clone блокує сервер на десятки секунд для великих репо
- Вимагає встановленого git
- GitHub API повертає коміти з SHA, батьками, авторами миттєво
- `GetBranchCommitInfosAsync` повертає `CommitInfo` з повними SHA і parents[] — достатньо для git-графу

### Чому кастомний dropdown замість нативного `<select>`
Нативний dropdown на Windows ігнорує CSS `background-color` на рівні OS. `color-scheme: dark` не дає повного контролю. Кастомний div-dropdown: повний контроль стилів, hover через CSS класи, скрол через `overflow-y: auto`.

## Grading

### Чому GitHub Actions а не локальний білд
100+ студентів одночасно знищили б сервер. GitHub Actions ізольовані, безкоштовні для публічних репо. Наш сервер тільки читає результат через API.

### Чому студент здає коміт а не "поточний стан гілки"
Захист від "я щось зламав після здачі". Студент явно вибирає що здає. CommitMappingJson зберігає маппінг sha→taskNumber.

### Чому не тестуємо назви методів/класів
Студенти роблять адаптації до різних доменів (клініка, готель, ресторан). Тестуємо поведінку (вхід → вивід через checks.json), не структуру.

### Чому checks.json а не тести в коді
Простіше редагувати для викладача без знання C#. I/O пари надійніші за перевірку назв методів.

## Keycloak

### Чому login-reset-password.ftl а не login-reset-credentials.ftl
Keycloak 24 використовує шаблон `login-reset-password.ftl` для першого кроку скидання пароля (введення email). В старших версіях він називався `login-reset-credentials.ftl`. Помилка в назві = Keycloak падає на дефолтну тему.

### Чому оновлюємо KeycloakSub замість створення нового UserLink
При `docker compose down -v` Keycloak видаляє своїх юзерів і створює нових з новими UUID (sub). SQLite не скидається. Нова спроба зв'язати студента по email → UNIQUE constraint на StudentId. Правильне рішення: оновити sub на існуючому UserLink.

## Відомі обмеження

1. GradingService кроки — заглушки (CloneStep, CheckoutStep, BuildStep, GitHubActionsStep)
2. GitHub token студента зберігається у відкритому вигляді в БД
3. Черга здач не реалізована (100 одночасних → проблема)
4. Тести в `content/labs/lab-01-intro/tests/` написані, але не підключені до пайплайну
5. GitBranchService.GetCommitsAsync (git clone) залишається для майбутнього grading pipeline

## TODO найближче

1. Реалізувати `BuildStep` (dotnet build в temp dir)
2. Реалізувати `GitHubActionsStep` (GitHub API check-runs)
3. Підключити `checks.json` до пайплайну
4. Черга через `Channel<T>` або `BackgroundService`
5. Зашифрувати `GithubToken` в БД (AES або sealed secrets)
