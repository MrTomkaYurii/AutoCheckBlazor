# Grading Pipeline

## Концепція

Важка робота (білд, тести) виконується на GitHub Actions — наш сервер тільки читає результат.
Студент здає конкретний коміт, не "що зараз на гілці".

## Флоу здачі (як задумано)

```
1. Студент пушить код на свою гілку

2. GitHub Actions запускається автоматично:
   - dotnet build
   - dotnet test (якщо є тести)

3. Студент відкриває лабу в UI → натискає "Здати"
   - Система тягне список гілок з GitHub API
   - Якщо лаба має BranchName → гілка підставляється автоматично
   - Студент бачить список комітів
   - Обирає конкретний коміт

4. GradingPipeline запускається:
   CloneStep    → git clone/fetch репо
   CheckoutStep → git checkout {commitSha}
   BuildStep    → dotnet build (локальна швидка перевірка)
   GitHubActionsStep → читаємо результат GitHub API

5. Результат зберігається в БД → Submission.Status = Review
   Студент отримує Notification
   Викладач бачить в черзі на перевірку
```

## Структура Grading/

```
Grading/
  Pipeline/
    GradingPipeline.cs          ← оркестратор
    Steps/
      IGradingStep.cs           ← інтерфейс
      CloneStep.cs              ← TODO
      CheckoutStep.cs           ← TODO
      BuildStep.cs              ← TODO
      GitHubActionsStep.cs      ← TODO
  Models/
    GradingContext.cs           ← передається між кроками
    GradingResult.cs            ← фінальний результат
```

## GradingContext — що передається між кроками

```csharp
SubmissionId, StudentId
RepoUrl      ← https://github.com/user/repo
CommitSha    ← конкретний хеш коміту
Branch       ← наприклад sandbox/intro
SourceDir    ← де шукати .csproj (sandbox/intro або src)
LabNumber

// Заповнюється кроками:
BuildPassed, BuildOutput
TestsPassed, TestsOutput
GitHubRunStatus, GitHubRunUrl
HasError, ErrorMessage
```

## checks.json — визначення перевірок для кожної лаби

Файл `content/labs/lab-NN-slug/checks.json`:

```json
{
  "sourceDir": "sandbox/intro",
  "tasks": [
    {
      "n": 1,
      "commitPattern": "Task1",
      "cases": [
        { "input": "70\n1.75", "expect": "22.86" },
        { "input": "90\n1.80", "expect": "27.78" }
      ]
    }
  ]
}
```

- `sourceDir` — де знаходиться .csproj студента
- `commitPattern` — підрядок в повідомленні коміту для ідентифікації задачі
- `cases` — вхідні дані і що шукаємо у виводі (не точний збіг, а Contains)

## GitHub Actions у студента

Студент додає файл `.github/workflows/check.yml` один раз:

```yaml
name: AutoCheck
on: [push]
jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0'
      - run: dotnet build sandbox/intro
```

Далі GitHub сам запускає при кожному пуші.

## Статуси

| Статус | Опис |
|--------|------|
| Passed | Білд + GitHub Actions зелений |
| Failed | Білд впав або Actions червоний |
| Pending | Actions ще виконується |
| Error | Репо недоступне, коміт не знайдено |

## Що НЕ перевіряємо

- Назви класів і методів — у студентів варіації
- Точний текст виводу — домен може відрізнятись (готель, ресторан...)
- Внутрішню структуру коду

## Що перевіряємо

1. `dotnet build` — компілюється без помилок
2. `dotnet run` з вхідними даними — не крашиться
3. Вивід містить очікуване число (для лаб з консольним I/O)
4. GitHub Actions результат (для всіх лаб)

## Поточний стан реалізації

- [x] Структура папок Grading/
- [x] GradingContext, GradingResult моделі
- [x] GradingPipeline оркестратор (заглушки)
- [x] GitHubService (GetBranches, GetCommits)
- [x] UI вибору гілки і коміту в діалозі здачі
- [x] checks.json для lab-01-intro
- [ ] CloneStep реалізація
- [ ] CheckoutStep реалізація
- [ ] BuildStep реалізація
- [ ] GitHubActionsStep реалізація
- [ ] Черга здач (100+ студентів паралельно)
- [ ] Зберігання CommitSha в Submission
