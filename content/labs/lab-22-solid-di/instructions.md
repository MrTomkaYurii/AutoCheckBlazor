# Лабораторна робота 22 — SOLID + Dependency Injection

## Проблема

Після Labs 03-21 у нас є ~5 300 рядків коду, що працює. Але є прихована проблема — **жорсткі залежності**:

```csharp
// Clinic.cs — 16 залежностей hardcoded у конструкторі
public Clinic(string name)
{
    Patients   = new PatientManager();     // ← hardcoded
    Logger     = new ClinicLogger();       // ← hardcoded
    Exporter   = new ClinicExporter(this); // ← hardcoded
    // ...ще 13 рядків...
}
```

Що з цим не так?
- Неможливо підмінити `ClinicLogger` на `TestLogger` для тестів
- Неможливо додати новий тип ціноутворення без зміни `AppointmentProcessor`
- `Clinic` знає про 16 конкретних класів — при зміні будь-якого треба змінювати Clinic

**SOLID** — 5 принципів що вирішують ці проблеми.  
**Dependency Injection** — механізм що робить залежності керованими.

---

## Ключові концепції

### S — Single Responsibility Principle

**"Клас повинен мати тільки одну причину для зміни."**

Клас `Clinic` порушує SRP — у нього п'ять причин змінитись:
1. Змінилась конфігурація клініки → міняємо Clinic
2. Додали новий менеджер → міняємо Clinic
3. Змінилась логіка подій → міняємо Clinic
4. Змінився формат звіту → міняємо Clinic
5. Змінився формат розкладу → міняємо Clinic

**Рішення:** виділяємо окремий `ClinicConfig` record для конфігурації.

```csharp
// Було:
public class Clinic { public string Name { get; } ... }

// Стало:
public record ClinicConfig(string Name, string Address = "", DateTime? Founded = null);
public class Clinic { public ClinicConfig Config { get; } ... }
```

### O — Open/Closed Principle

**"Класи відкриті для розширення, закриті для змін."**

Проблема без OCP:
```csharp
// AppointmentProcessor — щоб додати нову ставку, треба змінити існуючий клас:
if (appointment is UrgentAppointment) cost *= 1.5m;
else if (appointment is SpecialistAppointment) cost *= 1.3m;
// Новий тип → новий if → зміна існуючого коду → ризик регресій
```

**Рішення — Strategy pattern:**
```csharp
public interface ICostStrategy
{
    string Description { get; }
    decimal Calculate(Appointment appointment);
}

// Новий тип ціноутворення = новий клас, без змін AppointmentProcessor:
public class NightShiftCostStrategy : ICostStrategy { ... }
```

### L — Liskov Substitution Principle

**"Підтипи повинні бути замінними для своїх базових типів."**

У нашому проєкті вже реалізовано: `RegularAppointment`, `UrgentAppointment`, `SpecialistAppointment` — всі замінні для `Appointment`. Метод що приймає `Appointment` — працює з будь-яким підтипом.

Порушення LSP (для аналізу):
```csharp
// ❌ Порушення: підклас кидає виняток де базовий — не кидає
class ReadOnlyCollection : Collection {
    public override void Add(T item) => throw new NotSupportedException(); // LSP порушено
}
```

### I — Interface Segregation Principle

**"Клієнти не повинні залежати від методів, які вони не використовують."**

Погано (один великий інтерфейс):
```csharp
interface IClinicService {
    Task<List<Patient>> GetPatients();
    Task<List<Doctor>>  GetDoctors();
    Task AddPatient(Patient p);
    Task BookAppointment(Appointment a);
    Task ExportCsv(string path);
    Task GenerateReport();
    // ... 20+ методів
}
// Клас що потребує тільки GetDoctors() — знає про все інше
```

Добре (ISP):
```csharp
interface IPatientService     { Task<List<Patient>> GetAllAsync(); ... }
interface IDoctorService      { Task<List<Doctor>>  GetAllAsync(); ... }
interface IAppointmentService { Task<List<Appointment>> GetUpcomingAsync(); ... }
```

### D — Dependency Inversion Principle

**"Модулі верхнього рівня залежать від абстракцій, не від конкретних реалізацій."**

```csharp
// ❌ Погано — пряма залежність від конкретного класу:
public class ReportGenerator
{
    private readonly PatientService _service;  // конкретний клас
    public ReportGenerator() { _service = new PatientService(new ClinicDbContext()); }
}

// ✅ Добре — залежність від абстракції:
public class ReportGenerator
{
    private readonly IPatientService _service;  // інтерфейс
    public ReportGenerator(IPatientService service) { _service = service; }
    // DI-контейнер підставить реалізацію автоматично
}
```

### Dependency Injection — IServiceCollection

```csharp
var services = new ServiceCollection();

// Реєстрація з lifetime:
services.AddSingleton<ClinicLogger>();           // один на весь застосунок
services.AddScoped<ClinicDbContext>();           // новий на кожен scope
services.AddScoped<IPatientService, PatientService>(); // interface → implementation

var provider = services.BuildServiceProvider();

// Отримання сервісу:
var logger = provider.GetRequiredService<ClinicLogger>(); // кидає якщо не зареєстровано
var svc    = provider.GetService<IPatientService>();       // null якщо не зареєстровано
```

**Lifetimes:**
| Lifetime | Новий екземпляр | Підходить для |
|----------|----------------|---------------|
| `Singleton` | Один раз | Logger, HttpClient, конфігурація |
| `Scoped` | На кожен scope | DbContext, Repository, Service |
| `Transient` | На кожен запит | Легкі stateless об'єкти |

**Singleton + Scoped — небезпечна комбінація:**
```csharp
// ❌ Singleton не може залежати від Scoped!
services.AddSingleton<MyService>(sp =>
    new MyService(sp.GetRequiredService<ClinicDbContext>())); // DbContext — Scoped
// При першому використанні: DbContext буде "захоплений" назавжди → memory leak
```

### Паттерн Decorator

Decorator реалізує той самий інтерфейс і делегує виклики до "справжнього" об'єкта, додаючи поведінку:

```csharp
public class LoggingPatientService(IPatientService inner, ClinicLogger logger) : IPatientService
{
    public async Task<List<Patient>> GetAllAsync(CancellationToken ct = default)
    {
        logger.LogInfo("GetAllAsync викликано");
        var result = await inner.GetAllAsync(ct);  // делегування
        logger.LogInfo($"GetAllAsync → {result.Count} пацієнтів");
        return result;
    }
    // ...
}
```

DI реєстрація Decorator:
```csharp
services.AddScoped<IPatientService>(sp =>
    new LoggingPatientService(
        new PatientService(sp.GetRequiredService<ClinicDbContext>()),
        sp.GetRequiredService<ClinicLogger>()));
```

---

## Завдання

### Завдання 1. S — Single Responsibility: ClinicConfig

**Задача:** виділити конфігурацію клініки в окремий record.

Створіть `ClinicConfig` у `src/Models/`:
```csharp
public record ClinicConfig(string Name, string Address = "", DateTime? Founded = null)
{
    public string FoundedYear => Founded.HasValue ? ... : "невідомо";
}
```

Модифікуйте `Clinic`:
- Додайте `public ClinicConfig Config { get; }` 
- Додайте конструктор `Clinic(ClinicConfig config)`
- Залиште `Clinic(string name) : this(new ClinicConfig(name))` для зворотної сумісності
- `public string Name => Config.Name;` — делегат замість прямого поля

Задокументуйте (XML-коментарі або `//`): скільки ще відповідальностей залишилось у `Clinic`.

Чому використано `record` а не `class` для ClinicConfig? Що дає незмінність конфігурації?

**Ключові питання:**
- Скільки причин змінитись у вашій поточній `Clinic.cs`?
- Де проходить межа між "це одна відповідальність" і "це дві"?

---

### Завдання 2. O — Open/Closed: ICostStrategy

**Задача:** додати підтримку стратегій ціноутворення без зміни `AppointmentProcessor`.

Створіть у `src/Strategies/`:
```
ICostStrategy           — interface: Description, Calculate(Appointment)
RegularCostStrategy     — базова ставка: DurationMinutes × 10 грн
UrgentCostStrategy      — коефіцієнт: базова × multiplier (default 1.5)
DiscountCostStrategy    — знижка: базова × (1 - discountPercent)
```

Розширте `AppointmentProcessor` (не переписуйте!):
```csharp
private ICostStrategy? _costStrategy;

public AppointmentProcessor WithCostStrategy(ICostStrategy strategy) { ... return this; }
public decimal CalculateCost(Appointment a) => _costStrategy?.Calculate(a) ?? a.GetCost();
public static (decimal Regular, decimal WithStrategy) CompareCost(Appointment a, ICostStrategy s) => ...
```

**Тест OCP:** додайте `NightShiftCostStrategy` (ставка ×1.2 після 18:00) — без жодних змін у `AppointmentProcessor`, `Appointment`, або існуючих стратегіях.

**Ключові питання:**
- Чому `ICostStrategy?` (nullable) а не обов'язковий параметр конструктора?
- `_costStrategy?.Calculate(a) ?? a.GetCost()` — що відбудеться якщо стратегія не встановлена?

---

### Завдання 3+4. I + D — ISP + DIP: інтерфейси та реалізації

**Задача:** визначити три сервісних інтерфейси і реалізувати їх поверх EF Core.

Створіть у `src/Services/`:

**Інтерфейси (ISP):**
```
IPatientService     — GetAllAsync, GetByIdAsync, SearchAsync, AddAsync, SoftDeleteAsync, CountAsync
IDoctorService      — GetAllAsync, GetByIdAsync, GetBySpecialityAsync, CountAsync
IAppointmentService — GetUpcomingAsync, GetByPatientAsync, BookAsync, CancelAsync, CompleteAsync, GetTotalRevenueAsync
```

**Реалізації (DIP — залежать від ClinicDbContext через конструктор):**

Використайте **primary constructor** (C# 12):
```csharp
public class PatientService(ClinicDbContext context) : IPatientService
{
    public async Task<List<Patient>> GetAllAsync(CancellationToken ct = default)
        => await context.Patients.AsNoTracking().OrderBy(p => p.LastName).ToListAsync(ct);
    // ...
}
```

Primary constructor — параметри доступні як поля без явного оголошення.

**DIP перевірка:** напишіть метод що приймає `IPatientService` (не `PatientService`):
```csharp
static async Task PrintPatientCount(IPatientService service)
    => Console.WriteLine($"Пацієнтів: {await service.CountAsync()}");
```

Цей метод може працювати з PatientService, LoggingPatientService, або будь-яким mock.

**Ключові питання:**
- В чому різниця між ISP і звичайним розбиттям на кілька класів?
- Чому primary constructor зручніший для DIP ніж звичайний constructor?

---

### Завдання 5. IServiceCollection: DI-контейнер і Decorator

**Задача А — ServiceContainer:**

Створіть `src/Infrastructure/ServiceContainer.cs`:

```csharp
public static class ServiceContainer
{
    public static IServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.AddDbContext<ClinicDbContext>();       // Scoped
        services.AddSingleton<ClinicLogger>();          // Singleton
        services.AddScoped<IDoctorService,      DoctorService>();
        services.AddScoped<IAppointmentService, AppointmentService>();
        // IPatientService через Decorator — зареєструйте через фабрику (lambda sp => ...):
        // new LoggingPatientService(new PatientService(...), ...)
        // Отримуйте залежності через sp.GetRequiredService<T>()
        services.AddScoped<IPatientService>(sp => /* ваша фабрика */);
        return services.BuildServiceProvider();
    }
}
```

**Задача Б — Decorator (LoggingPatientService):**

```csharp
public class LoggingPatientService(IPatientService inner, ClinicLogger logger) : IPatientService
{
    public async Task<List<Patient>> GetAllAsync(CancellationToken ct = default)
    {
        // 1. logger.LogInfo — фіксуємо виклик
        // 2. await inner.GetAllAsync(ct) — делегуємо до "справжнього" сервісу
        // 3. logger.LogInfo — фіксуємо результат (кількість записів)
        // 4. return result
    }
    // інші методи — реалізуйте аналогічно (з логуванням або без)
}
```

**Задача В — lifetime перевірка:**

```csharp
var a = provider.GetRequiredService<ClinicLogger>();
var b = provider.GetRequiredService<ClinicLogger>();
Console.WriteLine(ReferenceEquals(a, b));  // true — Singleton

using var s1 = provider.CreateScope();
using var s2 = provider.CreateScope();
var svc1 = s1.ServiceProvider.GetRequiredService<IAppointmentService>();
var svc2 = s2.ServiceProvider.GetRequiredService<IAppointmentService>();
Console.WriteLine(ReferenceEquals(svc1, svc2)); // false — Scoped, різні scope
```

**Ключові питання:**
- Що відбудеться якщо зареєструвати `ClinicDbContext` як Singleton?
- Навіщо `provider.CreateScope()` в консольному застосунку?

---

### Завдання 6. GetRequiredService vs GetService + L: Liskov

**Задача А — DI resolution:**

```csharp
// GetRequiredService<T> — кидає InvalidOperationException якщо не зареєстровано
var logger = provider.GetRequiredService<ClinicLogger>();

// GetService<T> — повертає null якщо не зареєстровано
var opt = provider.GetService<ClinicLogger>();       // не null (зареєстровано)
var missing = provider.GetService<SessionManager>(); // null (не зареєстровано)
```

Правило: `GetRequiredService` — коли сервіс обов'язковий. `GetService` — коли опціональний.

**Задача Б — L: Liskov Substitution аналіз:**

Перевірте що наша ієрархія `Appointment` дотримується LSP:
```csharp
// Метод приймає базовий тип
static void ProcessAppointment(Appointment a)
{
    Console.WriteLine(a.GetDescription()); // поліморфний виклик
    Console.WriteLine($"Вартість: {a.GetCost()}");
}

// Всі підтипи замінні:
ProcessAppointment(new RegularAppointment(...));    // ✅
ProcessAppointment(new UrgentAppointment(...));     // ✅
ProcessAppointment(new SpecialistAppointment(...)); // ✅
```

Знайдіть у кодовій базі приклад де LSP **могло б бути порушено** (наприклад, якби `UrgentAppointment.Cancel()` кидав виняток замість повернення `false`). Задокументуйте.

**Ключові питання:**
- Де в нашому проєкті можна замінити `PatientService` на `LoggingPatientService` — без зміни коду що їх використовує?
- `GetRequiredService` vs `ActivatorUtilities.CreateInstance` — коли потрібен другий варіант?

---

## Рефлексійні питання

1. **SOLID як єдине ціле.** Покажіть як порушення одного принципу призводить до порушення інших. Наприклад: якщо `Clinic` порушує SRP → чи стає складніше дотриматись DIP?

2. **Decorator vs Inheritance для логування.** Чому `LoggingPatientService` реалізований як Decorator (композиція), а не як `class LoggingPatientService : PatientService` (спадкування)? Коли спадкування було б кращим вибором?

3. **Singleton DbContext — чому небезпечно.** `DbContext` тримає в пам'яті стан змінених об'єктів (Change Tracker). Якщо він Singleton — що відбудеться при конкурентних запитах? Де стан одного запиту "просочиться" в інший?

4. **Primary constructor і DI.** `public class PatientService(ClinicDbContext context)` — як компілятор C# 12 перетворює цей запис? Які є обмеження primary constructor порівняно зі звичайним?

5. **OCP і кількість файлів.** OCP зменшує ризик регресій але збільшує кількість файлів (`RegularCostStrategy`, `UrgentCostStrategy`, `DiscountCostStrategy`...). Як знайти баланс? Коли варто відмовитись від стратегій і залишити простий if/switch?

6. **ISP в реальних проєктах.** ASP.NET Core's `ILogger<T>` — великий чи маленький інтерфейс? Чи порушує він ISP? Порівняйте з нашим `IPatientService`.
