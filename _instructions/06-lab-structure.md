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

Перелік **вимог** на кожне завдання — саме їх Gemini перевіряє й розкладає у
`done`/`issues` (детальніше: [05-grading-pipeline](05-grading-pipeline.md)).
Заповнені для всіх 22 лаб.

```json
{
  "tasks": [
    {
      "n": 1,
      "requirements": [
        "Простір імен: namespace ClinicApp; (файловий стиль)",
        "Поле private static int _nextId = 1;",
        "Властивість Id: public int Id { get; } — лише гетер",
        "ToString() перевизначений у форматі [Id] FullName | ..."
      ]
    }
  ]
}
```

- `n` — номер задачі (відповідає `LabTask.Number`)
- `requirements[]` — конкретні перевірні вимоги; якщо блок є, Gemini перевіряє ТІЛЬКИ їх

> Історично тут були I/O-кейси (`input`/`expect`/`commitPattern`/`sourceDir`) для
> запуску коду — від цього відмовились на користь requirements-перевірки через Gemini.

## Як викладач керує лабами

Сторінка `/teacher/labs`:
- Переглядає список всіх лаб зі статистикою
- Редагує: назва, гілка, дедлайн (datetime-local picker), ціль
- Додає/редагує/видаляє завдання через TaskDialog
- Імпортує лаби з MD файлів (PreviewImportAsync → ImportAsync)

## Структура студентського репо

```
студент/repo/            (гілка Lab-01…Lab-04; з лаби 03 зливається в main)
  .gitignore
  oop-course.sln
  Lab01/                        ← лаба 01 (окремий проєкт-тренажер)
    Lab01.csproj
    Program.cs
    Task1.cs ... Task8.cs
  Lab02/                        ← лаба 02 (масиви, окремий проєкт)
  ClinicApp/                    ← основний проєкт, починається з лаби 03
    ClinicApp.csproj
    Patient.cs, Doctor.cs...
```

Grading клонує це репо локально й дивиться `git show {sha}` за маппінгом коміт→завдання
(файли `taskN`), або шукає файл евристично по назві завдання. GitHub Actions **не**
використовується — build/тести на сервері не запускаються, оцінює Gemini за вимогами.
