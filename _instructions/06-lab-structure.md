# Структура лаб

## Файлова система

```
content/labs/
  lab-01-intro/
    instructions.md   ← умова лаби (парситься LabMdParser)
    checks.json       ← параметри перевірки (вхід/вихід)
    tests/            ← xUnit тести (не компілюються в основний проект)
  lab-02-arrays/
    instructions.md
  lab-03-classes/
    instructions.md
    checks.json
  ... (22 лаби всього)
```

## instructions.md — підтримувані формати заголовків завдань

LabMdParser підтримує кілька форматів на рівнях `##` та `###`:

```markdown
## Задача 1. Назва ⭐⭐          ← крапка після номера
## Завдання 1. Назва ⭐          ← Завдання з крапкою
## Завдання 1 — Назва ⭐⭐        ← тире після номера (лаби 7-22)
### Завдання 1. Назва ⭐         ← H3 рівень (лаба 18+)
```

Regex: `^#{2,3}\s+(?:Задача|Завдання)\s+(\d+)(?:\.|\s+[—–])\s+(.+?)(\s+[⭐]+)?\s*$`

## Що парсить LabMdParser

- `Title` — з H1, прибирає "Лаба NN —" / "Лабораторна робота №NN"
- `Goal` — секція `## Мета`
- `BranchName` — з `git checkout -b {name}`
- `MergesMain` — якщо є "зливається в `main`" (без "не зливається")
- `Tasks[]` — з заголовків завдань (обидва формати, H2 і H3)
  - `Brief` — **весь вміст** між заголовками завдань (без обмеження символів)
  - `Difficulty` — кількість ⭐, default 1

## Як лаби потрапляють в БД

При старті `DatabaseSeeder`:
1. Сканує `content/labs/lab-*/` у порядку сортування
2. Парсить кожен `instructions.md` через `LabMdParser`
3. Створює `LabDef` + `LabTask` записи
4. Пропускає лаби у яких немає `instructions.md`
5. `Deadline = null` (встановлює викладач через UI)

**Умова:** `if (!await db.Labs.AnyAsync())` — сідується тільки якщо таблиця порожня.
При зміні лаб — видалити `autocheck.db` і перезапустити.

## checks.json — формат

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

- `sourceDir` — де знаходиться `.csproj` відносно кореня репо
- `n` — номер задачі (відповідає `LabTask.Number`)
- `commitPattern` — підрядок у commit message для ідентифікації задачі
- `input` — stdin (рядки розділені `\n`)
- `expect` — що має бути у stdout (перевірка через `Contains`, не `Equals`)

## Як викладач керує лабами

Сторінка `/teacher/labs`:
- Переглядає список всіх лаб зі статистикою
- Редагує: назва, гілка, дедлайн (datetime-local picker), ціль
- Додає/редагує/видаляє завдання через TaskDialog
- Імпортує лаби з MD файлів (PreviewImportAsync → ImportAsync)

## Структура студентського репо (лаба 01)

```
студент/repo/
  .github/workflows/check.yml   ← GitHub Actions (запускає build при пуші)
  .gitignore
  sandbox/intro/
    SandboxIntro.csproj
    Program.cs
    Task1.cs ... Task8.cs
  src/                           ← починається з лаби 03
    Patient.cs, Doctor.cs...
```
