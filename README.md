# AutoCheck

Веб-платформа автоматичної перевірки лабораторних робіт з курсу **«ООП на C#»**.
Кафедра комп'ютерних наук, ЧНУ ім. Ю. Федьковича.

## Стек

| | |
|---|---|
| **Runtime** | .NET 10, Blazor Server |
| **БД** | SQLite + EF Core 9 |
| **UI** | MudBlazor 9, CSS glassmorphism |
| **Auth** | ASP.NET Core Identity (+ Google OAuth, опційно) |

## Запуск

```bash
dotnet run
# → http://localhost:5186
```

Жодних контейнерів чи зовнішніх сервісів — акаунти зберігаються в тій самій SQLite базі.

Для входу через Google додайте ключі у `appsettings.json`:

```json
"Authentication": {
  "Google": { "ClientId": "…", "ClientSecret": "…" }
}
```

(Google Cloud Console → OAuth 2.0 Client ID, redirect URI: `http://localhost:5186/signin-google`.)
Якщо ключі порожні — кнопка Google просто не показується.

Тестові акаунти:

| Email | Пароль | Роль |
|-------|--------|------|
| `student@test.com` | `Test1234!` | Студент |
| `teacher@test.com` | `Test1234!` | Викладач |

## Функціонал

### Студент
- Реєстрація на `/register` (email+пароль) або через Google (вибір групи на онбордингу)
- Дашборд з картками лаб, прогресом і дедлайнами
- Здача лаб: вибір гілки + коміту через GitHub API, маппінг комітів до завдань
- Перегляд результатів авто-перевірки (pass/warn/fail по завданнях, diff коду)
- Таблиця всіх оцінок
- Редагування профілю (ім'я, email, GitHub URL/токен)

### Викладач
- Черга на перевірку з авто-оновленням
- Журнал оцінок (матриця студенти × лаби)
- GradeDialog: виставлення оцінки захисту, live розрахунок фінальної
- CRUD лаб і завдань, імпорт з Markdown файлів
- Управління групами та студентами
- Аналітика: розподіл оцінок, топ студентів

## Структура

```
AutoCheckBlazor/
├── Components/        # Blazor сторінки, layout, dialogs, shared
├── Data/              # EF Core, entities, seeder, LabMdParser
├── Models/            # UI DTO моделі
├── Services/          # Бізнес-логіка, GitHub API, grading
├── Grading/           # Pipeline перевірки (частково реалізований)
├── content/labs/      # 22 лаби: instructions.md + checks.json
├── wwwroot/css/       # Dark glassmorphism стилі
└── _instructions/     # Документація для розробки
```

## База даних

SQLite файл `autocheck.db` у кореневій теці.

```bash
# Скид до seed-даних (14 демо-студентів, 22 лаби, тестові акаунти):
rm autocheck.db && dotnet run
```

## Лаби

22 лабораторних з курсу ООП на C#, парсяться при старті з `content/labs/`:

| Лаби | Теми |
|------|------|
| 01-06 | Основи C#, масиви, класи, члени класу, інкапсуляція, успадкування |
| 07-12 | Інтерфейси, поліморфізм, generics, ітератори, рефлексія, файли |
| 13-16 | Events/Delegates, LINQ, функціональне програмування, Console UI |
| 17-22 | Entity Framework Core (4 лаби), Async/Await, SOLID+DI |

## Grading Pipeline

Концепція: важка робота (build, тести) → GitHub Actions студента.
Наш сервер читає результат через API.

**Реалізовано:** UI здачі, CommitMapping, BranchOverride, структура pipeline.

**TODO:** CloneStep, BuildStep, GitHubActionsStep, черга здач.

## Документація

Детальна документація для розробки: [`_instructions/`](_instructions/README.md)
