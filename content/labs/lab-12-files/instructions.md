# Лаба 12 — File I/O (Робота з файлами)

## Мета

Навчитись читати та писати файли в C#: від простих `File.WriteAllText` до `StreamWriter` з форматуванням, CSV-парсингу з обробкою помилок та збереження стану між запусками програми.

## Контекст

Після Lab 11 система вміє валідувати дані через рефлексію. Але все, що ввів користувач, зникає при закритті програми. Ця лаба додає **персистентність**: логування дій у файл, експорт звітів, імпорт даних з CSV та збереження сесії між запусками.

## Гілка

```bash
git checkout main
git pull
git checkout -b feature/files
```

---

## Завдання 1 — ClinicLogger: пишемо у файл ⭐⭐

### Умова

Клініці потрібен журнал подій — файл `clinic.log`, куди записуються дії системи з часовою міткою. Треба також вміти переглянути останні N рядків без відкриття файлу вручну.

### Що реалізувати

**`Utils/ClinicLogger.cs`**:

```csharp
public class ClinicLogger
{
    private readonly string _logPath;

    public ClinicLogger(string logPath = "clinic.log") { ... }

    public void LogInfo(string message)    => Write("INFO ", message);
    public void LogWarning(string message) => Write("WARN ", message);
    public void LogError(string message)   => Write("ERROR", message);

    private void Write(string level, string message)
    {
        string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";
        File.AppendAllText(_logPath, line + Environment.NewLine, Encoding.UTF8);
    }

    public string[] GetLastLines(int n)
    {
        if (!File.Exists(_logPath)) return Array.Empty<string>();
        string[] all = File.ReadAllLines(_logPath, Encoding.UTF8);
        // повернути останні n рядків з масиву all
        ...
    }

    public void Clear() { ... }
    public bool Exists() => File.Exists(_logPath);
}
```

Додати `Logger` до `Clinic.cs` та підключити до меню пункт "10. Файли" з підпунктами перегляду та очищення лога.

### Підказки

1. `File.AppendAllText(path, text, encoding)` — дописує в кінець файлу, не перезаписує. Якщо файл не існує — створює автоматично.
2. `File.ReadAllLines(path, encoding)` — зчитує **всі** рядки у масив `string[]`. Для великих файлів це дорого, але для лога прийнятно.
3. `Encoding.UTF8` — обов'язково для кирилиці. `using System.Text;` потрібен.
4. `Environment.NewLine` — правильний перенос рядка для поточної ОС (`\r\n` на Windows, `\n` на Linux).
5. Щоб отримати останні N рядків: `int skip = Math.Max(0, all.Length - n)` → `Array.Copy(...)`.
6. [File.AppendAllText — docs](https://learn.microsoft.com/en-us/dotnet/api/system.io.file.appendalltext)
7. [File.ReadAllLines — docs](https://learn.microsoft.com/en-us/dotnet/api/system.io.file.readalllines)

### Адаптація до вашого домену

| Клініка | Готель | Ресторан | Університет | Прокат авто | Бібліотека | Спортзал |
|---------|--------|----------|-------------|-------------|------------|---------|
| `ClinicLogger` → `clinic.log` | `HotelLogger` → `hotel.log` | `RestaurantLogger` → `restaurant.log` | `UniversityLogger` → `uni.log` | `RentalLogger` → `rental.log` | `LibraryLogger` → `library.log` | `GymLogger` → `gym.log` |
| `LogInfo/Warning/Error` | однаково | однаково | однаково | однаково | однаково | однаково |

### Коміт

```bash
git add src/Utils/ClinicLogger.cs src/Clinic.cs src/Program.cs
git commit -m "Lab12 Task1: add ClinicLogger with AppendAllText and GetLastLines"
```

---

## Завдання 2 — ClinicExporter: StreamWriter і форматування ⭐⭐

### Умова

Адміністратор хоче отримувати готові текстові звіти у файлах — з заголовками, роздільниками, датою генерації. Файли мають зберігатись у теці `reports/2026-05-14/` (дата автоматично).

### Що реалізувати

**`Utils/ClinicExporter.cs`**:

```csharp
public class ClinicExporter
{
    private readonly Clinic _clinic;
    private readonly string _baseDir;

    public ClinicExporter(Clinic clinic, string baseDir = "reports") { ... }

    private string PrepareDir()
    {
        string dir = Path.Combine(_baseDir, DateTime.Today.ToString("yyyy-MM-dd"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    public string ExportPatients()
    {
        string path = Path.Combine(PrepareDir(), "patients.txt");

        using StreamWriter writer = new StreamWriter(path, false, Encoding.UTF8);
        // заголовок: назва, дата генерації, роздільник
        // цикл: для кожного пацієнта з clinic.Patients.GetAll() → writer.WriteLine(...)
        // підсумок: кількість пацієнтів
        return path;
    }

    // ExportAppointments(), ExportBilling(), ExportTreatmentPlans() — аналогічна структура

    public void ExportAll() { ... }
}
```

### Підказки

1. `StreamWriter(path, append: false, encoding)` — `false` означає перезаписати файл якщо існує.
2. `using StreamWriter writer = new StreamWriter(...)` — автоматично викликає `writer.Dispose()` (закриває файл) при виході з блоку. Без `using` файл може залишитись відкритим.
3. `Directory.CreateDirectory(dir)` — створює теку і всі батьківські теки. **Не кидає виняток** якщо тека вже існує.
4. `Path.Combine("reports", "2026-05-14", "patients.txt")` — правильно склеює частини шляху для будь-якої ОС. Не роби `"reports/" + date + "/patients.txt"` — на Windows і Linux різний роздільник.
5. `{i + 1,3}` — вирівнювання по правому краю в 3 символи: `  1.`, ` 10.`, `100.`.
6. [StreamWriter — docs](https://learn.microsoft.com/en-us/dotnet/api/system.io.streamwriter)
7. [Path.Combine — docs](https://learn.microsoft.com/en-us/dotnet/api/system.io.path.combine)
8. [Directory.CreateDirectory — docs](https://learn.microsoft.com/en-us/dotnet/api/system.io.directory.createdirectory)

### Адаптація до вашого домену

| Клініка | Готель | Ресторан | Університет | Прокат авто | Бібліотека | Спортзал |
|---------|--------|----------|-------------|-------------|------------|---------|
| `ClinicExporter` | `HotelExporter` | `RestaurantExporter` | `UniversityExporter` | `RentalExporter` | `LibraryExporter` | `GymExporter` |
| `ExportPatients/Appointments/Billing` | `ExportGuests/Bookings/Invoices` | `ExportCustomers/Reservations/Orders` | `ExportStudents/Courses/Grades` | `ExportClients/Rentals/Invoices` | `ExportReaders/Loans/Fines` | `ExportMembers/Sessions/Payments` |
| `reports/yyyy-MM-dd/` | однаково | однаково | однаково | однаково | однаково | однаково |

### Коміт

```bash
git add src/Utils/ClinicExporter.cs src/Clinic.cs src/Program.cs
git commit -m "Lab12 Task2: add ClinicExporter with StreamWriter, Path.Combine, Directory.CreateDirectory"
```

---

## Завдання 3 — ImportResult та CsvImporter: читаємо та парсимо ⭐⭐⭐

### Умова

Адміністратор отримав список нових пацієнтів у форматі CSV і хоче завантажити їх одним файлом. Але CSV може містити помилкові рядки — треба їх пропустити і повідомити про кожну окремо.

### Формат CSV

```
FirstName,LastName,DateOfBirth,BloodType,Phone
Іван,Петренко,15.03.1985,APositive,0501234567
Олена,Коваль,22.07.1992,BNegative,0672345678
НеПравильний рядок
,Ткач,01.01.2000,,
```

Перший рядок — заголовок, пропускається. Порожні рядки — пропускаються. Помилкові рядки — записуються в `ImportResult`, але не зупиняють імпорт.

### Що реалізувати

**`Utils/ImportResult.cs`**:

```csharp
public class ImportResult
{
    private readonly List<string> _errors = new();

    public int Imported { get; private set; }
    public int Skipped  { get; private set; }
    public IReadOnlyList<string> Errors => _errors;

    public void AddSuccess() => Imported++;

    public void AddError(int lineNumber, string reason)
    {
        Skipped++;
        _errors.Add($"Рядок {lineNumber}: {reason}");
    }

    public void Print() { ... }
}
```

**`Utils/CsvImporter.cs`**:

```csharp
public class CsvImporter
{
    public ImportResult ImportPatients(string filePath)
    {
        var result = new ImportResult();

        if (!File.Exists(filePath))
        {
            result.AddError(0, $"Файл не знайдено: {filePath}");
            return result;
        }

        string[] lines = File.ReadAllLines(filePath, Encoding.UTF8);

        for (int i = 1; i < lines.Length; i++) // i=1: пропускаємо заголовок
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            try
            {
                string[] parts = line.Split(',');
                // парсинг: parts[0]=FirstName, parts[1]=LastName, parts[2]=DateOfBirth...
                // DateTime.ParseExact(parts[2].Trim(), "dd.MM.yyyy", CultureInfo.InvariantCulture)
                // (BloodType)Enum.Parse(typeof(BloodType), parts[3].Trim())
                ...
                result.AddSuccess();
            }
            catch (Exception ex)
            {
                result.AddError(i + 1, ex.Message);
            }
        }

        return result;
    }
}
```

### Підказки

1. `try/catch` **навколо одного рядка** — не навколо всього циклу. Помилка в рядку 3 не повинна зупиняти рядки 4, 5, 6.
2. `line.Split(',')` — повертає `string[]`. Перевір `.Length` перед зверненням до `parts[2]` — рядок може мати менше полів.
3. `DateTime.ParseExact(str, "dd.MM.yyyy", CultureInfo.InvariantCulture)` — суворий парсинг формату. `using System.Globalization;` потрібен.
4. `Enum.Parse(typeof(BloodType), str)` — кидає `ArgumentException` якщо рядок не відповідає жодному значенню enum. Це нормально — `catch` перехопить.
5. Номер рядка у повідомленні: `i + 1` (бо `i` рахується з 0, а людина рахує з 1).
6. [File.ReadAllLines — docs](https://learn.microsoft.com/en-us/dotnet/api/system.io.file.readalllines)
7. [string.Split — docs](https://learn.microsoft.com/en-us/dotnet/api/system.string.split)
8. [Enum.Parse — docs](https://learn.microsoft.com/en-us/dotnet/api/system.enum.parse)

### Адаптація до вашого домену

| Клініка | Готель | Ресторан | Університет | Прокат авто | Бібліотека | Спортзал |
|---------|--------|----------|-------------|-------------|------------|---------|
| `CsvImporter.ImportPatients` | `CsvImporter.ImportGuests` | `CsvImporter.ImportCustomers` | `CsvImporter.ImportStudents` | `CsvImporter.ImportClients` | `CsvImporter.ImportReaders` | `CsvImporter.ImportMembers` |
| `ImportResult` | однаково | однаково | однаково | однаково | однаково | однаково |

### Коміт

```bash
git add src/Utils/ImportResult.cs src/Utils/CsvImporter.cs src/Clinic.cs src/Program.cs
git commit -m "Lab12 Task3: add ImportResult and CsvImporter with per-line error handling"
```

---

## Завдання 4 — SessionManager: зберігаємо стан між запусками ⭐⭐⭐

### Умова

Зараз при кожному запуску програми дані починаються з нуля — пацієнти, яких додав користувач, зникають. `SessionManager` зберігає список пацієнтів у файл `session.dat` при виході та відновлює їх при наступному старті.

### Формат файлу

```
[PATIENTS]
Іван,Петренко,15.03.1985,APositive,0501234567
Олена,Коваль,22.07.1992,BNegative,0672345678
[END]
```

Секції позначені `[НАЗВА]` — так можна легко розширити формат у майбутньому (додати `[APPOINTMENTS]` тощо).

### Що реалізувати

**`Utils/SessionManager.cs`**:

```csharp
public class SessionManager
{
    private readonly string _sessionPath;

    public bool Exists() => File.Exists(_sessionPath);

    public void Save(Clinic clinic)
    {
        using StreamWriter writer = new StreamWriter(_sessionPath, false, Encoding.UTF8);
        // Запишіть секцію [PATIENTS]
        // Для кожного пацієнта — один рядок у форматі:
        //   FirstName,LastName,DateOfBirth,BloodType,Phone
        // (DateOfBirth форматуйте через :dd.MM.yyyy)
        // Закрийте секцію [END]
    }

    public int Load(Clinic clinic)
    {
        if (!File.Exists(_sessionPath)) return 0;

        string[] lines = File.ReadAllLines(_sessionPath, Encoding.UTF8);
        string section = "";
        int loaded = 0;

        foreach (string line in lines)
        {
            if (line.StartsWith("[")) { section = line; continue; }
            if (string.IsNullOrWhiteSpace(line)) continue;

            if (section == "[PATIENTS]")
            {
                try
                {
                    // розпарсити рядок → створити Patient → додати в clinic.Patients
                    ...
                    loaded++;
                }
                catch { /* пошкоджений рядок — пропустити */ }
            }
        }

        return loaded;
    }
}
```

**`Program.cs`** — на старті:

```csharp
if (clinic.Session.Exists())
{
    Console.Write("Знайдено збережену сесію. Завантажити? (y/n): ");
    if (Console.ReadLine()?.Trim().ToLower() == "y")
    {
        int loaded = clinic.Session.Load(clinic);
        Console.WriteLine($"Завантажено {loaded} пацієнтів.");
    }
}
```

При виході (case "0"):

```csharp
Console.Write("Зберегти сесію? (y/n): ");
if (Console.ReadLine()?.Trim().ToLower() == "y")
    clinic.Session.Save(clinic);
```

### Підказки

1. `StreamWriter(path, append: false)` — другий параметр `false` = перезаписати. При збереженні сесії ми завжди хочемо свіжий файл.
2. `line.StartsWith("[")` — детектуємо секцію. Після цього `section = line` зберігає поточний контекст для наступних рядків.
3. `catch { }` без параметра — пропускаємо пошкоджений рядок без повідомлення. Це прийнятно: краще завантажити 9 з 10 пацієнтів, ніж впасти.
4. Зверни увагу: `_nextId` у `Patient` є `static`. При завантаженні пацієнтів з файлу їм призначаться **нові** ID (лічильник продовжує рахунок від поточного значення). Це нормально для даного рівня.
5. `{p.DateOfBirth:dd.MM.yyyy}` — форматування DateTime у рядок у потрібному форматі.
6. [StreamWriter — docs](https://learn.microsoft.com/en-us/dotnet/api/system.io.streamwriter)
7. [string.StartsWith — docs](https://learn.microsoft.com/en-us/dotnet/api/system.string.startswith)

### Адаптація до вашого домену

| Клініка | Готель | Ресторан | Університет | Прокат авто | Бібліотека | Спортзал |
|---------|--------|----------|-------------|-------------|------------|---------|
| `SessionManager` зберігає `[PATIENTS]` | зберігає `[GUESTS]` | зберігає `[CUSTOMERS]` | зберігає `[STUDENTS]` | зберігає `[CLIENTS]` | зберігає `[READERS]` | зберігає `[MEMBERS]` |
| `session.dat` | однаково | однаково | однаково | однаково | однаково | однаково |

### Коміт

```bash
git add src/Utils/SessionManager.cs src/Clinic.cs src/Program.cs
git commit -m "Lab12 Task4: add SessionManager, save/load session on exit/start"
```

---

## Перевірка перед здачею

```bash
cd src
dotnet build
dotnet run
```

Переконайтесь, що:

- [ ] `clinic.log` з'являється після першого LogInfo/LogWarning/LogError виклику
- [ ] Кожен рядок логу має формат `[yyyy-MM-dd HH:mm:ss] [LEVEL] message`
- [ ] Папка `reports/yyyy-MM-dd/` створюється автоматично при першому експорті
- [ ] Файли звітів мають заголовок, дату генерації та нижній підсумок
- [ ] CSV-файл з 1 помилковим рядком з 5: `Імпортовано: 4 | Пропущено: 1` + деталі
- [ ] Файл `session.dat` з'являється при збереженні сесії
- [ ] Після перезапуску і вибору "y" — пацієнти з попередньої сесії знову в системі
- [ ] Пошкоджений рядок у `session.dat` не зупиняє завантаження решти

---

## Питання для самоперевірки

1. Чим `File.WriteAllText` відрізняється від `File.AppendAllText`? Коли кожен з них доречний?
2. Чому `using StreamWriter writer = ...` важливіший за просто `StreamWriter writer = new ...`? Що станеться якщо не закрити потік?
3. Чому `Path.Combine` краще за конкатенацію рядків `"reports/" + date + "/file.txt"`?
4. У `CsvImporter` `try/catch` обгортає один рядок циклу, а не весь цикл. Яка різниця з точки зору поведінки?
5. `SessionManager.Load` повертає `int` (кількість завантажених), а не `bool`. Чому це краще?
6. `File.ReadAllLines` завантажує весь файл у пам'ять. Коли це стає проблемою і що використовувати натомість?

---

## Злиття

```bash
git checkout main
git merge --no-ff feature/files -m "Merge feature/files: Lab12 File I/O"
git push
```

> Наступна лаба: `git checkout -b feature/events` — `delegate`, `event`, `EventHandler<T>`, обробники що пишуть у вже знайомий `ClinicLogger`.
