# Структура лаб

## Файлова система

```
content/labs/
  lab-01-intro/
    instructions.md   ← умова лаби
    checks.json       ← параметри перевірки (вхід/вихід)
    tests/            ← xUnit тести (НЕ компілюються в основний проект)
      Lab01Tests.csproj
      TaskTestBase.cs
      Task1Tests.cs ... Task8Tests.cs
  lab-02-arrays/
    instructions.md
  lab-03-classes/
    instructions.md
    checks.json       ← TODO
  ...
```

## instructions.md — формат

```markdown
# Лаба NN — Назва

## Мета
...

## Гілка для цієї лаби
```bash
git checkout -b sandbox/intro   ← звідси парситься BranchName
```

## Задача 1. Назва ⭐⭐     ← парситься Number, Title, Difficulty
### Умова
...                              ← парситься як Brief
```

## Що парситься LabMdParser

- `Title` — з H1 (без "Лаба NN —")
- `Goal` — секція "## Мета"
- `BranchName` — з `git checkout -b {name}`
- `MergesMain` — якщо є "зливається в `main`"
- `Tasks[]` — з `## Задача N. Title ⭐⭐` заголовків
  - `Brief` — з підсекції `### Умова` або перші 800 символів

## checks.json — формат

```json
{
  "sourceDir": "sandbox/intro",
  "tasks": [
    {
      "n": 1,
      "commitPattern": "Task1",
      "cases": [
        { "input": "70\n1.75",  "expect": "22.86" },
        { "input": "90\n1.80",  "expect": "27.78" }
      ]
    }
  ]
}
```

Поля:
- `sourceDir` — де знаходиться .csproj студента відносно кореня репо
- `n` — номер задачі (відповідає LabTask.Number)
- `commitPattern` — підрядок у commit message для ідентифікації задачі
- `input` — stdin (рядки розділені \n)
- `expect` — що має бути у stdout (перевірка через Contains, не equals)

## Як лаби потрапляють в БД

При старті DatabaseSeeder:
1. Сканує `content/labs/lab-*/`
2. Парсить `instructions.md` через LabMdParser
3. Створює LabDef + LabTask записи
4. `Deadline = null` (встановлює викладач через UI)

## Як викладач керує лабами

Сторінка `/teacher/labs`:
- Переглядає список всіх лаб
- Редагує: назва, гілка, дедлайн (datetime-local picker), ціль
- Додає/редагує/видаляє задачі
- Імпортує лаби з MD файлів

## Структура студентського репо (приклад лаба 01)

```
студент/repo/
  .github/
    workflows/
      check.yml         ← GitHub Actions
  .gitignore
  sandbox/
    intro/
      SandboxIntro.csproj
      Program.cs        ← Task1.Run() або Task8.Run()
      Task1.cs
      Task2.cs
      ...
      Task8.cs
  src/                  ← починається з лаби 03
    Patient.cs
    Doctor.cs
    ...
```

## Лаба 01 — особливості

- Гілка: `sandbox/intro`
- SourceDir: `sandbox/intro`
- Кожна задача в окремому файлі `TaskN.cs`
- Клас `Task1`, метод `Run()` — конвенція, але НЕ вимога
- Program.cs викликає одну задачу (студент змінює номер)
- При здачі: студент здає коміт після кожної задачі
- commitPattern для ідентифікації: "Task1", "Task2"...

## Лаба 03 — особливості

- Гілка: `feature/catalog`
- SourceDir: `src`
- Всі класи в одному проекті `src/ClinicApp`
- Namespace: `ClinicApp`
- Класи: Patient, Doctor, PatientManager, DoctorManager, Appointment, AppointmentManager, Clinic, GrowablePatientManager
