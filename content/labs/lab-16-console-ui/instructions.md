# Лабораторна робота №16. Console UI — Spectre.Console

## Мета

Навчитися підключати сторонні NuGet-пакети і відокремлювати UI-логіку від бізнес-логіки. Замінити ручне малювання текстового інтерфейсу на готову бібліотеку Spectre.Console: інтерактивні меню, таблиці, панелі, дерева.

## Гілка

```
feature/console-ui
```

---

## Яка проблема зараз

Відкрийте `src/Program.cs`. Знайдіть будь-яку функцію меню — наприклад `PatientsMenu`. Порахуйте скільки разів там є `Console.WriteLine`. Кожен рядок меню виводиться вручну. Кожен список пацієнтів — це `for`-цикл із `Console.WriteLine`.

Дві проблеми:

1. **Дублювання.** Логіка показу списку пацієнтів є в `PatientsMenu`, в `WaitingRoomMenu`, в `AppointmentsMenu` — і скрізь однаково. Якщо треба додати нову колонку — шукаємо по всьому файлу.

2. **Змішування.** `Program.cs` одночасно: малює UI, читає ввід, викликає бізнес-методи. Це три різні обов'язки в одному місці. Будь-яка зміна у відображенні зачіпає логіку.

**Рішення:** виділити UI в окремий клас. `Program.cs` вирішує *що* показати — `ClinicRenderer` вирішує *як*.

---

## Що таке сторонній пакет

До цього лаби ви використовували тільки те що вбудовано в .NET: `Console`, `File`, `List<T>`, LINQ. Але .NET дозволяє підключати бібліотеки від інших розробників через **NuGet** — менеджер пакетів .NET.

**Spectre.Console** — бібліотека для красивого консольного інтерфейсу: кольори через ANSI-коди, таблиці з рамками, інтерактивні меню зі стрілками, прогрес-бари, ієрархічні дерева.

Встановлення — одна команда:

```
dotnet add package Spectre.Console
```

Після цього у `.csproj` з'являється рядок `<PackageReference>`. NuGet завантажує бібліотеку і C# компілятор "бачить" її класи так само як вбудовані.

---

## Ключова ідея: фасад над бібліотекою

Можна одразу писати `AnsiConsole.Write(...)` в `Program.cs`. Але тоді `Program.cs` стане залежним від Spectre.Console напряму — завтра захочете замінити бібліотеку і доведеться переписувати все.

Правильний підхід: створити `ClinicRenderer` — **статичний клас-фасад** між `Program.cs` і Spectre.Console.

```
Program.cs
    ↓ викликає
ClinicRenderer
    ↓ використовує
Spectre.Console
```

`Program.cs` не знає що всередині використовується Spectre.Console. Він просто просить: "покажи пацієнтів". А як саме — справа рендерера.

Це той самий **принцип єдиної відповідальності (SRP)** якого ми дотримуємось починаючи з Lab 03: кожен клас відповідає за одне.

---

## Завдання 1. Встановити Spectre.Console та базові утиліти ⭐⭐

### Що вводить Spectre.Console для простого тексту

`Console.WriteLine("Помилка: ...")` — текст без стилю.

`AnsiConsole.MarkupLine("[red]✗ Помилка[/]")` — той самий текст, але Spectre.Console розбирає `[red]...[/]` як розмітку і виводить червоним. Це схоже на HTML-теги але для консолі.

`Markup.Escape(text)` — обов'язковий захист: якщо в `text` є символи `[` або `]` — вони стануть розміткою і зламають відображення. `Markup.Escape` перетворює їх у безпечні послідовності.

`new Rule("Заголовок")` — горизонтальна лінія з текстом посередині або ліворуч. Замінює рядки `"──── Пацієнти ────"`.

### Де створити клас

Створіть папку `src/UI/` і в ній файл `src/UI/ClinicRenderer.cs`.

Простір імен — `ClinicApp.UI`. Клас — `public static class ClinicRenderer`.

### Що реалізувати

П'ять базових методів:

| Метод | Що робить |
|-------|-----------|
| `PrintHeader(string title)` | Горизонтальна лінія `Rule` із назвою розділу, ліворуч |
| `PrintSuccess(string message)` | Зелений текст із префіксом `✓` |
| `PrintError(string message)` | Червоний текст із префіксом `✗` |
| `PrintWarning(string message)` | Жовтий текст із префіксом `⚠` |
| `PrintInfo(string message)` | Приглушений (`[dim]`) сірий текст |

Для `PrintHeader` підказка:
```csharp
AnsiConsole.Write(new Rule($"[bold steelblue1]{Markup.Escape(title)}[/]")
{
    Justification = Justify.Left
});
```

### Де застосувати

Знайдіть у `Program.cs` всі `Console.WriteLine("── Назва ──")` — замініть на `ClinicRenderer.PrintHeader(...)`.

Знайдіть всі `Console.WriteLine("Помилка: ...")` у `catch`-блоках — замініть на `ClinicRenderer.PrintError(...)`.

Знайдіть повідомлення про успіх — замініть на `ClinicRenderer.PrintSuccess(...)`.

### Ключові питання

- Чому використовується `Markup.Escape(text)` перед вставкою тексту в розмітку?
- Що станеться якщо прізвище пацієнта містить `[` — наприклад `"Іван [молодший]"`?
- Чим відрізняється `AnsiConsole.MarkupLine(...)` від `AnsiConsole.WriteLine(...)`?

---

## Завдання 2. Інтерактивні меню та введення даних ⭐⭐

### Проблема поточного введення

Зараз кожне меню виглядає так:
```
1. Показати всіх
2. Додати пацієнта
0. Назад
Оберіть:
```
Користувач бачить числа і вводить `1`. Якщо набрав `11` — `default`-гілка.

`SelectionPrompt` змінює парадигму: меню відображається списком, користувач рухається **стрілками** і вибирає `Enter`. Жодного введення чисел — жодних невалідних команд.

### SelectionPrompt — як влаштований

```csharp
string choice = AnsiConsole.Prompt(
    new SelectionPrompt<string>()
        .Title("Оберіть дію")
        .AddChoices("Показати всіх", "Додати", "← Назад"));
```

Повертає рядок — той самий що був у `.AddChoices(...)`. `switch (choice)` тепер перемикається по рядку, а не по числу.

### Що реалізувати в ClinicRenderer

```csharp
public static string SelectMenu(string title, string[] options)
```

Всередині: `SelectionPrompt<string>` з переданим заголовком і варіантами.

Підказка щодо стилю виділення:
```csharp
.HighlightStyle(new Style(Color.SteelBlue1, decoration: Decoration.Bold))
```

### TextPrompt — типізоване введення

`TextPrompt<int>` робить дві речі за вас:
1. Виводить підказку
2. Якщо введено не число — автоматично просить ввести знову з повідомленням про помилку

```csharp
int id = AnsiConsole.Prompt(
    new TextPrompt<int>("ID пацієнта:")
        .ValidationErrorMessage("[red]Введіть ціле число[/]"));
```

`TextPrompt<string>` для необов'язкового тексту потребує `.AllowEmpty()` (без параметрів).

`ConfirmationPrompt` — для запитань yes/no:
```csharp
bool confirmed = AnsiConsole.Prompt(new ConfirmationPrompt("Зберегти?"));
```

### Що реалізувати в ClinicRenderer

| Метод | Що повертає |
|-------|-------------|
| `PromptInt(string label)` | `int` — з авто-повтором при невалідному вводі |
| `PromptString(string label, bool allowEmpty = false)` | `string` |
| `PromptDecimal(string label)` | `decimal` |
| `PromptConfirm(string question)` | `bool` — через `ConfirmationPrompt` |

### Де застосувати в Program.cs

Замініть усі патерни `Console.Write("..."); int.TryParse(Console.ReadLine(), out int x)` на `int x = ClinicRenderer.PromptInt("...")`.

Замініть `Console.Write("y/n")` → `ClinicRenderer.PromptConfirm(...)`.

Замініть числові меню на `ClinicRenderer.SelectMenu(...)`. Для "назад" використовуйте опцію `"← Назад"` і перевіряйте `if (cmd == "← Назад") { inMenu = false; break; }`.

### Ключові питання

- Чому `TextPrompt<int>` краще за `int.TryParse(Console.ReadLine())`?
- `SelectionPrompt` повертає рядок — тому `switch` тепер порівнює рядки замість чисел. Що в цьому є перевагою, а що недоліком?
- `"← Назад"` як рядкова константа повторюється у кожному меню. Як це можна покращити?

---

## Завдання 3. Таблиці для списків ⭐⭐

### Проблема поточного відображення списків

```csharp
for (int i = 0; i < patients.Length; i++)
    Console.WriteLine(patients[i]);
```

Виводить рядки один під одним без вирівнювання. Якщо ім'я коротке — колонки "розповзаються". Немає заголовків колонок. Немає підрахунку.

### Що таке Table у Spectre.Console

`Table` сам обчислює ширину кожної колонки за найдовшим вмістом і малює рамку:

```
┌────┬──────────────────┬─────┐
│ ID │ Ім'я             │ Вік │
├────┼──────────────────┼─────┤
│  1 │ Іван Петренко    │  41 │
│  2 │ Олена Коваль     │  33 │
└────┴──────────────────┴─────┘
```

Базова структура:
```csharp
var table = new Table()
    .Border(TableBorder.Rounded)
    .AddColumn("[bold]ID[/]")
    .AddColumn("[bold]Ім'я[/]");

table.AddRow("1", "Іван Петренко");
// ...

AnsiConsole.Write(table);
```

Для вирівнювання колонки по центру або правому краю:
```csharp
.AddColumn(new TableColumn("[bold]ID[/]").Centered())
.AddColumn(new TableColumn("[bold]Вартість[/]").RightAligned())
```

**Увага:** у `AddRow` кожна комірка — це рядок, але він може містити розмітку. Для значень що прийшли від користувача — обгортайте `Markup.Escape(...)`.

### Що реалізувати

Три методи в `ClinicRenderer`:

**`RenderPatients(IEnumerable<Patient> patients)`** — колонки: ID, Ім'я, Вік, Група крові, Телефон. Неповнолітні: вік виводити `[yellow]жовтим[/]`.

**`RenderDoctors(IEnumerable<Doctor> doctors)`** — колонки: ID, Ім'я, Спеціальність, Доступний, Розклад. Доступний зараз: `[green]Так[/]`, недоступний: `[dim]Ні[/]`.

**`RenderAppointments(IEnumerable<Appointment> appointments)`** — колонки: ID, Тип, Пацієнт, Лікар, Дата/час, Вартість, Статус. Логіка кольорів:

| Стан | Колір |
|------|-------|
| Скасовано | `[red]` |
| Оплачено | `[green]` |
| Прострочено | `[dim]` |
| Заплановано | `[yellow]` |
| Тип Urgent | `[bold red]` |

Для перевірки типу запису: `a switch { UrgentAppointment => "...", SpecialistAppointment => "...", _ => "..." }`.

### Де застосувати в Program.cs

Замініть всі виклики `clinic.Patients.DisplayAll()` → `ClinicRenderer.RenderPatients(clinic.Patients.GetAll())`.

Аналогічно для лікарів і записів.

### Ключові питання

- `DisplayAll()` у менеджерах також виводить список. Тепер його роль виконує рендерер. Чи варто залишити `DisplayAll()` у менеджерах?
- `Table` отримує `IEnumerable<Patient>`, а не `Patient[]`. Яка перевага від прийому `IEnumerable<T>` замість масиву?
- Що трапиться якщо ім'я пацієнта містить символ `]`?

---

## Завдання 4. Картки деталей і ієрархія ⭐⭐⭐

### Panel — рамка навколо тексту

Таблиця добре показує список. Але для детальної картки однієї сутності — краще `Panel`:

```
╭─── Пацієнт: Іван Петренко ──────────╮
│ ID: 1                                │
│ Дата народження: 15.03.1985          │
│ Вік: 41 рр. (Повнолітній)           │
│ Група крові: APositive               │
╰──────────────────────────────────────╯
```

Вміст — це рядок з розміткою. Заголовок — `PanelHeader`.

Підказка до структури:
```csharp
var panel = new Panel(content)
{
    Header  = new PanelHeader("[bold] Заголовок [/]"),
    Border  = BoxBorder.Rounded,
    Padding = new Padding(1, 0)
};
AnsiConsole.Write(panel);
```

### Tree — ієрархічне відображення

`Tree` ідеально підходить для медичної картки де є три типи записів:

```
Медична картка: Іван Петренко
├── Діагнози (2)
│   ├── I10 — Гіпертонічна хвороба ⟳ хронічне
│   └── J06.9 — Гострий ринофарингіт
├── Аналізи (2)
│   ├── ✓ Гемоглобін: 145 г/л (норма: 120–160)
│   └── ⚠ Холестерин: 6.2 ммоль/л (норма: < 5.2)
└── Рецепти (1)
    └── Лізиноприл 10 мг × 30 днів [активний]
```

Кожна гілка — `root.AddNode(...)`, кожен елемент у гілці — `branch.AddNode(...)`.

Щоб розділити записи на типи: `.OfType<Diagnosis>()`, `.OfType<LabResult>()`, `.OfType<Prescription>()` — це LINQ-методи що ви вже знаєте.

### BarChart — для звітів

```csharp
var chart = new BarChart()
    .Width(60)
    .Label("[bold]Виручка по місяцях[/]")
    .CenterLabel();

chart.AddItem("2026/05", 1350.0, Color.SteelBlue1);
```

Корисно для методу `GetMonthlyRevenue()` з Lab 14.

### Що реалізувати

**`RenderPatientCard(Patient patient)`** — Panel з полями: ID, дата народження, вік (з кольоровою міткою повнолітній/ні), група крові, телефон.

**`RenderDoctorCard(Doctor doctor)`** — Panel з полями: ID, спеціальність, ліцензія, телефон, розклад, статус доступності.

**`RenderMedicalRecord(Patient patient, IEnumerable<MedicalRecord> records)`** — Tree з трьома гілками: Діагнози, Аналізи, Рецепти. Якщо гілка порожня — показує `[dim]немає[/]`.

**`RenderSpecialityStats(IEnumerable<SpecialityReport> reports)`** — Table.

**`RenderMonthlyRevenue(IEnumerable<(int Year, int Month, decimal Total)> data)`** — BarChart.

**`WithSpinner(string message, Action action)`** — загортає будь-яку операцію у спіннер:
```csharp
AnsiConsole.Status()
    .Spinner(Spinner.Known.Dots)
    .Start(message, _ => action());
```

### Де застосувати

У `MedicalRecordsMenu`, пункт "Картка пацієнта": знайти пацієнта → `ClinicRenderer.RenderMedicalRecord(patient, records)`.

У `WaitingRoomMenu`, пункт "Хто перший?": `ClinicRenderer.RenderPatientCard(first)` замість `Console.WriteLine(first)`.

У `FilesMenu`, "Експортувати всі звіти": обгорніть виклик у `ClinicRenderer.WithSpinner("Експортую...", () => { ... })`.

У `ReportsMenu`: `RenderSpecialityStats` для спеціальностей, `RenderMonthlyRevenue` для місячної виручки.

### Ключові питання

- Чому `Panel` краще за звичайний `Console.WriteLine` для деталей однієї сутності?
- `Tree` отримує `IEnumerable<MedicalRecord>` і сам розкладає по типах через `.OfType<T>()`. Де ще в проєкті ми бачили такий підхід?
- `WithSpinner` приймає `Action` — тобто будь-яку дію. Це той самий механізм що ви вивчали в Lab 15. Як це пов'язано з `Action<T>`?

---

## Перевірка

```
dotnet run --project src
```

Після запуску:

- [ ] Головне меню — стрілки замість цифр
- [ ] Пункт "Пацієнти → Показати всіх" — таблиця з рамкою і кольоровим віком
- [ ] Пункт "Записи → Майбутні" — таблиця з кольоровими статусами
- [ ] Пункт "Медична картка → Картка пацієнта (Tree)" — ієрархічне дерево
- [ ] Пункт "Черга → Хто перший?" — Panel із деталями пацієнта
- [ ] Пункт "Файли → Експорт" — видно спіннер під час операції
- [ ] Пункт "Звіти → Виручка по місяцях" — стовпчаста діаграма
- [ ] Помилки — червоним, успіх — зеленим, заголовки — горизонтальна лінія

---

## Питання для самоперевірки

1. Що таке NuGet-пакет? Чим він відрізняється від стандартних бібліотек .NET?
2. Навіщо `ClinicRenderer` якщо можна писати `AnsiConsole.Write(...)` безпосередньо в `Program.cs`?
3. `Markup.Escape(text)` — обов'язковий для всіх рядків від користувача. Чому? Що це за атака?
4. `SelectionPrompt<string>` повертає рядок з `AddChoices`. Тому `switch` порівнює рядки, а не числа. Що відбудеться якщо опціонально змінити текст одного пункту — скільки місць у коді треба оновити?
5. Чим `TextPrompt<int>` кращий за `int.TryParse(Console.ReadLine())`? Що ще він дає?
6. `WithSpinner` приймає `Action`. Як це пов'язано з патерном з Lab 15?
