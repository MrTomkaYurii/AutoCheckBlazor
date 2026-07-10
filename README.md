# AutoCheck

Веб-платформа автоматичної перевірки лабораторних робіт з курсу **«ООП на C#»**.
Кафедра комп'ютерних наук, ЧНУ ім. Ю. Федьковича.

Студент здає коміт зі свого GitHub-репозиторію → сервер клонує репо, витягує код
кожного завдання й аналізує його через **Gemini** → викладач переглядає й виставляє
оцінку захисту.

## Стек

| | |
|---|---|
| **Runtime** | .NET 10, Blazor Server |
| **БД** | SQLite + EF Core 9 (`EnsureCreated`, без міграцій) |
| **UI** | MudBlazor 9, CSS glassmorphism (dark) |
| **Auth** | ASP.NET Core Identity (+ Google OAuth, опційно) |
| **AI-перевірка** | Gemini 2.5 Flash через REST API |
| **Git** | локальний `git` CLI (clone/fetch) для перевірки + GitHub REST API для UI |

## Запуск

```bash
dotnet run
# → http://localhost:5186
```

Для dev — жодних контейнерів чи зовнішніх сервісів: акаунти, оцінки й лаби живуть
у тій самій SQLite базі. Для авто-перевірки потрібні встановлений `git` та ключ
Gemini у конфізі (без ключа кнопка «Здати» повертає зрозумілу помилку).

### Конфіг (`appsettings.json`)

```json
"Gemini":  { "ApiKey": "", "Model": "gemini-2.5-flash", "DailyLimit": 50 },
"Grading": { "WorkRoot": "", "MaxFileSizeKb": 64, "MaxLinesPerFile": 250, "RepoRetentionDays": 7 },
"Authentication": { "Google": { "ClientId": "", "ClientSecret": "" } }
```

- `Gemini:ApiKey` порожній → авто-перевірка вимкнена; `DailyLimit` — денний ліміт викликів.
- `Grading:WorkRoot` порожній → тимчасова тека ОС; `RepoRetentionDays` — через скільки днів простою клон репо видаляється.
- `Authentication:Google:ClientId` порожній → кнопки Google приховані.

Redirect URI для Google в Cloud Console: `http://localhost:5186/signin-google`.

Тестові акаунти (сідяться при першому старті):

| Email | Пароль | Роль |
|-------|--------|------|
| `student@test.com` | `Test1234!` | Студент |
| `teacher@test.com` | `Test1234!` | Викладач |

## Функціонал

### Студент
- Реєстрація на `/register` (email+пароль) або через Google (вибір групи на онбордингу)
- Дашборд з картками лаб, прогресом і дедлайнами
- Здача лаб: вибір гілки + комітів через GitHub API, маппінг комітів до завдань
- Авто-перевірка: git clone → витяг коду per-task → Gemini → pass/warn/fail + фідбек + diff
- Ліміт спроб (3 за замовч.), серверна перевірка дедлайну, черга перевірок
- Таблиця всіх оцінок, редагування профілю (ім'я, email, GitHub URL/токен)

### Викладач
- Черга на перевірку з авто-оновленням, GradeDialog з live-розрахунком фінальної оцінки
- Журнал оцінок (матриця студенти × лаби), детальні результати per-student/lab
- CRUD лаб і завдань, імпорт з Markdown, управління групами та студентами
- Аналітика: розподіл оцінок, топ студентів
- Anti-plagiarism: сигнали про збіг коду / підміну репозиторію, ручне схвалення

## Формула оцінки

```
AutoScore = зважена за складністю (⭐) середня по завданнях, найкраща спроба
Фінал     = 0.4 × AutoScore + 0.6 × DefenseScore
```

Поріг 50: AutoScore < 50 → лаба відхиляється до захисту.

## Структура

```
AutoCheckBlazor/
├── Components/        # Blazor: Pages, Layout, Shared, Dialogs
├── Infrastructure/
│   ├── Data/          # AppDbContext, *Entities.cs, DatabaseSeeder
│   └── Helpers/       # LabMdParser, Scoring, BackupHelper, KyivTime, GradingPaths…
├── Models/            # UI DTO (CommitModels, LabModels, PeopleModels, RosterModels)
├── Services/          # Бізнес-логіка: grading, GitHub, plagiarism, notifications…
├── content/labs/      # 22 лаби: instructions.md + checks.json + (lab-01) tests/
├── wwwroot/css/       # Dark glassmorphism стилі
├── deploy/            # Docker/compose для production
└── _instructions/     # Документація для розробки
```

## База даних

SQLite файл `autocheck.db` у кореневій теці (включно з Identity-акаунтами).
Схема створюється через `EnsureCreated` (без міграцій). Щоденний авто-бекап
(`BackupService`, VACUUM INTO).

```bash
# Повний скид до seed-даних (демо-студенти, 22 лаби, тестові акаунти):
rm autocheck.db && dotnet run
```

## Лаби

22 лабораторних, парсяться при першому старті з `content/labs/lab-*/instructions.md`:

| Лаби | Теми |
|------|------|
| 01-06 | Основи C#, масиви, класи, члени класу, інкапсуляція, успадкування |
| 07-12 | Інтерфейси, поліморфізм, generics, ітератори, рефлексія, файли |
| 13-16 | Events/Delegates, LINQ, функціональне програмування, Console UI |
| 17-22 | Entity Framework Core (4 лаби), Async/Await, SOLID+DI |

Кожна лаба має `checks.json` з переліком **вимог** на кожне завдання — саме їх
Gemini перевіряє (див. [05-grading-pipeline](_instructions/05-grading-pipeline.md)).

## Документація

Детальна документація для розробки: [`_instructions/`](_instructions/README.md)

## Production

Docker-образ і compose — у [`deploy/`](deploy/README.md); TLS термінується reverse-proxy,
`ForwardedHeaders:KnownNetworks` задає довірену підмережу.
