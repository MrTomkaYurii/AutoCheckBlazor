# Grading Pipeline

## Концепція

Важка робота (білд, тести) виконується на **GitHub Actions** студента — наш сервер тільки читає результат через API. Студент здає конкретний коміт, не "що зараз на гілці".

## Флоу здачі

```
1. Студент пушить код на свою гілку
   GitHub Actions запускається автоматично (dotnet build + test)

2. Студент відкриває лабу → натискає "Здати"
   ├─ GitHub API тягне гілки репозиторію
   ├─ Режим "Список комітів": вибір гілки + коміти через API
   └─ Режим "Граф гіта": main гілка, граф з fork/merge арками

3. Студент обирає коміт і маппінг коміт → завдання (кастомний dropdown)

4. GradingPipeline (TODO: реалізувати):
   CloneStep      → git clone/fetch репо
   CheckoutStep   → git checkout {commitSha}
   BuildStep      → dotnet build
   TestStep       → checks.json I/O тести
   GitHubActionsStep → читає check-runs через GitHub API

5. Submission.Status = Review → Notification студенту
   Викладач бачить в черзі на перевірку → GradeDialog
```

## Структура Grading/

```
Grading/
  Pipeline/
    GradingPipeline.cs       ← оркестратор (основна логіка)
    Steps/
      IGradingStep.cs        ← інтерфейс кроку
      CloneStep.cs           ← TODO
      CheckoutStep.cs        ← TODO
      BuildStep.cs           ← TODO
      GitHubActionsStep.cs   ← TODO
  Models/
    GradingContext.cs        ← передається між кроками
    GradingResult.cs         ← фінальний результат
```

## GradingContext — що передається між кроками

```csharp
SubmissionId, StudentId
RepoUrl      // https://github.com/user/repo
CommitSha    // конкретний хеш коміту
Branch       // sandbox/intro
SourceDir    // де шукати .csproj (sandbox/intro або src)
LabNumber
CommitMapping // Dictionary<string, int> sha → taskNumber

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

- `input` — stdin (рядки розділені `\n`)
- `expect` — що має бути у stdout (`Contains`, не `Equals`)
- `commitPattern` — підрядок у commit message

## GitHub Actions у студента

Студент додає `.github/workflows/check.yml` один раз:

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
          dotnet-version: '10.0'
      - run: dotnet build sandbox/intro
```

## Що НЕ перевіряємо
- Назви класів і методів — у студентів різні домени
- Точний текст виводу — `Contains`, не `Equals`
- Внутрішню структуру коду

## Поточний стан реалізації

- [x] GradingContext, GradingResult моделі
- [x] GradingPipeline оркестратор (структура)
- [x] GitHubService (GetBranches, GetBranchCommitInfos)
- [x] UI вибору гілки і коміту з маппінгом
- [x] CommitMappingJson зберігається в Submission.CommitMappingJson
- [x] BranchOverride зберігається в Submission.BranchOverride
- [x] checks.json для lab-01-intro
- [ ] CloneStep реалізація
- [ ] CheckoutStep реалізація
- [ ] BuildStep реалізація
- [ ] GitHubActionsStep реалізація
- [ ] Черга здач (BackgroundService або Channel<T>)
