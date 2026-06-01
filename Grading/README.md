# Grading Pipeline

Ізольований модуль автоматичної перевірки лабораторних робіт.

---

## Ідея

Студент пушить код і здає конкретний коміт через UI.
Система перевіряє цей коміт і повертає результат.

**Головний принцип:** весь важкий білд і тести виконуються на GitHub Actions,
наш сервер тільки читає результат через API. Сервер не навантажується.

---

## Флоу

```
Студент пушить коміт
    ↓
GitHub Actions запускається автоматично (dotnet build, dotnet test)
    ↓
Студент натискає "Здати" в UI, вказує хеш коміту
    ↓
GradingPipeline запускається на нашому сервері:
    1. Clone/Checkout  — перевіряємо що коміт існує
    2. Build           — dotnet build локально (швидка перевірка)
    3. GitHubActions   — читаємо результат GitHub через API
    ↓
Результат зберігається в БД
Студент бачить: ✓ або ✗ + посилання на GitHub Actions run
```

---

## Структура

```
Grading/
  Pipeline/
    GradingPipeline.cs          ← оркестратор, запускає кроки послідовно
    Steps/
      IGradingStep.cs           ← інтерфейс кроку
      CloneStep.cs              ← git clone / git fetch
      CheckoutStep.cs           ← git checkout {commitSha}
      BuildStep.cs              ← dotnet build {sourceDir}
      GitHubActionsStep.cs      ← GET GitHub API → статус run
  Models/
    GradingContext.cs           ← передається між кроками
    GradingResult.cs            ← фінальний результат у БД
  README.md                     ← цей файл
```

---

## Кроки пайплайну

### CloneStep
Клонує репо студента у тимчасову папку `/tmp/autocheck/{submissionId}`.
Якщо папка вже є — робить `git fetch`.
Після перевірки — папка видаляється.

### CheckoutStep
Переключається на конкретний коміт: `git checkout {commitSha}`.
Якщо коміт не існує — помилка, пайплайн зупиняється.

### BuildStep
Запускає `dotnet build` у папці `sourceDir` (визначається з `LabDef.SourceDir`).

| Результат | Що означає |
|-----------|-----------|
| exit 0 | Код компілюється ✓ |
| exit 1 | Синтаксичні помилки, помилки типів тощо |

### GitHubActionsStep
Читає результат GitHub Actions для цього коміту:
```
GET /repos/{owner}/{repo}/commits/{sha}/check-runs
→ conclusion: "success" | "failure" | "neutral"
```
Студент отримує пряме посилання на run де видно деталі.

---

## GitHub Actions у студента

Студент додає файл `.github/workflows/check.yml` **один раз** на початку курсу:

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
      - name: Build Lab01
        run: dotnet build sandbox/intro
```

Далі GitHub сам запускає при кожному пуші.

---

## Статуси результату

| Статус | Причина |
|--------|---------|
| `Passed` | Білд пройшов + GitHub Actions зелений |
| `Failed` | Білд впав або GitHub Actions червоний |
| `Pending` | GitHub Actions ще виконується |
| `Error` | Репо недоступне, коміт не знайдено тощо |

---

## Що реалізовано зараз

- [x] Структура папок і файлів
- [x] `GradingContext` — модель що передається між кроками
- [x] `GradingResult` — фінальний результат
- [x] `IGradingStep` — інтерфейс кроку
- [x] `GradingPipeline` — оркестратор
- [ ] `CloneStep` — TODO
- [ ] `CheckoutStep` — TODO
- [ ] `BuildStep` — TODO
- [ ] `GitHubActionsStep` — TODO
- [ ] Черга здач (щоб 100 студентів не лягли сервер)
- [ ] `checks.json` парсер для визначення `sourceDir`
