# Лабораторна робота 21 — Async / Await

## Проблема

В Labs 17-20 ви писали синхронний код навмисно — щоб зрозуміти, що відбувається.

```csharp
var patients = context.Patients.ToList();       // потік ЗАБЛОКОВАНИЙ ~ 50мс
var doctors  = context.Doctors.ToList();        // потік ЗАБЛОКОВАНИЙ ~ 50мс
// Загальний час: ~100мс, протягом яких потік нічого не робить
```

Тепер уявіть 100 одночасних запитів у веб-застосунку. Кожен блокує потік. Пул потоків вичерпується. Нові запити стають у чергу. Застосунок "провисає".

**Рішення: async/await** — не блокувати потік, а звільнити його під час очікування I/O.

```csharp
var patients = await context.Patients.ToListAsync();  // потік ВІЛЬНИЙ під час очікування
var doctors  = await context.Doctors.ToListAsync();   // потік ВІЛЬНИЙ під час очікування
```

---

## Ключові концепції

### async / await — основи

`async` — модифікатор методу: "цей метод може містити `await`".  
`await` — "зупини виконання цього методу, звільни потік, продовж коли Task завершиться".

```csharp
// Синхронний:
public List<Patient> GetPatients() => context.Patients.ToList();

// Асинхронний:
public async Task<List<Patient>> GetPatientsAsync(CancellationToken ct = default)
    => await context.Patients.ToListAsync(ct);
```

**Типи повернення async методів:**
| Синхронний | Async еквівалент |
|-----------|-----------------|
| `void`    | `async Task`    |
| `T`       | `async Task<T>` |
| `void` (event handler) | `async void` ← єдиний допустимий `async void` |

**`async void` — чому зло:**
- Виключення з `async void` не можна перехопити ззовні → крашить процес
- Неможливо `await` — не можна дочекатись завершення
- Неможливо передати `CancellationToken`
- **Правило**: завжди `async Task`, ніколи `async void`

### CancellationToken

Дозволяє ззовні сигналізувати про скасування операції:

```csharp
// Автоматичний таймаут:
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
await service.SearchPatientsAsync("Коваль", cts.Token);

// При скасуванні кидається OperationCanceledException
// Ловимо окремо від Exception:
try
{
    await service.SearchPatientsAsync(query, cts.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("Скасовано.");
}
```

### Task.WhenAll — паралельне виконання

```csharp
// ❌ Послідовно (час = t1 + t2 + t3):
int patients = await context.Patients.CountAsync();
int doctors  = await context.Doctors.CountAsync();
decimal rev  = await context.Appointments.SumAsync(a => a.Cost);

// ✅ Паралельно (час = max(t1, t2, t3)):
var t1 = context.Patients.CountAsync();
var t2 = context.Doctors.CountAsync();
var t3 = context.Appointments.SumAsync(a => a.Cost);
await Task.WhenAll(t1, t2, t3);
// Результати: t1.Result, t2.Result, t3.Result
```

### Task.WhenAny — race між задачами

```csharp
var apiTask     = GetDataAsync(ct);
var timeoutTask = Task.Delay(3000, ct);

var winner = await Task.WhenAny(apiTask, timeoutTask);
if (winner == timeoutTask)
    Console.WriteLine("Таймаут!");
else
    var data = apiTask.Result;  // безпечно — Task вже завершений
```

### Parallel.ForEachAsync — паралельна обробка колекції

```csharp
await Parallel.ForEachAsync(items, new ParallelOptions
{
    MaxDegreeOfParallelism = Environment.ProcessorCount,
    CancellationToken = ct
}, async (item, token) =>
{
    await ProcessItemAsync(item, token);
});
```

**ВАЖЛИВО**: `DbContext` не є thread-safe!  
Не викликайте `SaveChangesAsync()` всередині `Parallel.ForEachAsync`.  
Правильний патерн: завантажити → обробити паралельно в пам'яті → зберегти одним викликом.

### AggregateException

`Task.WhenAll` при помилці в кількох задачах:
- `await Task.WhenAll(...)` → розгортає `AggregateException` і кидає тільки **першу** помилку
- Щоб отримати **всі** помилки → `ContinueWith` з `TaskContinuationOptions.OnlyOnFaulted`

```csharp
var allTasks = Task.WhenAll(t1, t2, t3);

_ = allTasks.ContinueWith(t =>
{
    foreach (var ex in t.Exception!.InnerExceptions)
        Console.WriteLine($"Помилка: {ex.Message}");
}, TaskContinuationOptions.OnlyOnFaulted);

try { await allTasks; }
catch (Exception ex) { /* тільки перша помилка */ }
```

**Перевірка статусу Task:**
| Властивість | Значення |
|------------|---------|
| `task.IsCompletedSuccessfully` | завершився без помилок |
| `task.IsFaulted` | завершився з винятком |
| `task.IsCanceled` | скасований |

### IProgress\<T\> — звітування про прогрес

Дозволяє повідомляти UI про прогрес без прямого зв'язку з UI-шаром:

```csharp
// Визначення: метод нічого не знає про Console або UI
public async Task ProcessAsync(IProgress<int>? progress = null, ...)
{
    for (int i = 0; i < items.Count; i++)
    {
        await ProcessItemAsync(items[i]);
        progress?.Report(i + 1);  // ? — якщо null, нічого не відбувається
    }
}

// Використання:
var p = new Progress<int>(n => Console.WriteLine($"Оброблено {n}"));
await service.ProcessAsync(p, ct);
```

`Progress<T>` автоматично маршалює `Report()` на UI-потік (SynchronizationContext).

### HttpClient + async

```csharp
// ПРАВИЛЬНО: один статичний HttpClient на весь застосунок
private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };

// GetFromJsonAsync<T> = GET + JSON десеріалізація в один виклик
var data = await _http.GetFromJsonAsync<MyDto>(url, ct);
```

**Обробка помилок:**
```csharp
catch (HttpRequestException ex)    // мережева помилка (немає з'єднання, DNS fail)
catch (TaskCanceledException) when (!ct.IsCancellationRequested)  // таймаут HttpClient
// TaskCanceledException від ct → НЕ ловимо, дозволяємо спливати
```

---

## Завдання

### Завдання 1. Async Task Main та SeedAsync

**Задача:** зрозуміти, як `async/await` проникає від точки входу вниз по стеку викликів.

У `DbSeeder` існує синхронний `Seed(context)`. Напишіть `SeedAsync(context, ct)`:

```
public static async Task SeedAsync(ClinicDbContext context, CancellationToken ct = default)
```

Принцип: кожен синхронний приватний метод `SeedPatients` → `SeedPatientsAsync`:
- `context.Patients.Any()` → `await context.Patients.AnyAsync(ct)`
- `context.SaveChanges()` → `await context.SaveChangesAsync(ct)`

**Демо в Program.cs:**  
Додайте `await` до виклику `SeedAsync`. Оскільки `Program.cs` використовує top-level statements, компілятор автоматично генерує `async Task Main` — можна використовувати `await` прямо на верхньому рівні.

**Ключові питання:**
- `async Task` vs `async void` — яка різниця при виключенні всередині?
- `CancellationToken ct = default` — що означає `default` для структури `CancellationToken`?

---

### Завдання 2. AsyncClinicService — базові async методи EF Core

**Задача:** створити клас `AsyncClinicService` у `src/Data/` з async версіями типових операцій.

Реалізуйте методи:

```
GetAllPatientsAsync(CancellationToken ct) → Task<List<Patient>>
GetPatientByIdAsync(int id, CancellationToken ct) → Task<Patient?>
GetUpcomingAppointmentsAsync(CancellationToken ct) → Task<List<Appointment>>
SaveAppointmentAsync(Appointment a, CancellationToken ct) → Task<int>
```

Принцип: кожен метод `await`-ить EF Core async метод (`ToListAsync`, `FirstOrDefaultAsync`, `SaveChangesAsync`).

**Також додайте async варіанти до `ClinicRepository`:**

```
GetPatientWithAppointmentsAsync(int id, ct) → Task<Patient?>
GetUpcomingAppointmentsAsync(ct)            → Task<List<Appointment>>
```

Тут доцільно додати `.ConfigureAwait(false)` — поясніть у коментарі: навіщо і коли це актуально.

**Ключові питання:**
- Якщо `async Task<T>` метод не містить жодного `await` — що видасть компілятор?
- Різниця між `Task.Result` і `await task` — коли перший варіант небезпечний?

---

### Завдання 3. Task.WhenAll, Parallel.ForEachAsync, CancellationToken

**Задача А — GetDashboardAsync:**

Реалізуйте метод `GetDashboardAsync() → Task<ClinicDashboard>`.

`ClinicDashboard` — record із полями: `PatientCount`, `DoctorCount`, `TotalRevenue`, `UpcomingCount`, `TodayCount`.

Всі 5 значень отримайте **паралельно** через `Task.WhenAll`. Порівняйте з послідовним варіантом (можна виміряти час через `Stopwatch`).

**Задача Б — GetDashboardWithTimeoutAsync:**

Реалізуйте варіант із таймаутом через `Task.WhenAny`:

```csharp
var winner = await Task.WhenAny(dashboardTask, Task.Delay(timeoutMs));
if (winner == timeoutTask) { /* таймаут */ }
```

Що відбувається з `dashboardTask` після того, як `timeoutTask` виграла race?

**Задача В — MarkAppointmentsAsPaidAsync:**

```
MarkAppointmentsAsPaidAsync(IEnumerable<int> ids, ct) → Task<int>
```

Використайте `Parallel.ForEachAsync` з `MaxDegreeOfParallelism`. Дотримайтесь патерну:
1. Завантажити все одним запитом (`ToListAsync`)
2. Обробити паралельно в пам'яті (`Parallel.ForEachAsync` + `Interlocked.Increment`)
3. Зберегти одним `SaveChangesAsync`

Чому не можна викликати `FindAsync` всередині `Parallel.ForEachAsync` для того самого `_context`?

**Задача Г — SearchPatientsAsync з CancellationToken:**

```
SearchPatientsAsync(string query, CancellationToken ct) → Task<List<Patient>>
```

Додайте `await Task.Delay(200, ct)` для симуляції затримки. Протестуйте зі скороченим токеном:

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
await service.SearchPatientsAsync("...", cts.Token);  // кине OperationCanceledException
```

**Ключові питання:**
- `Interlocked.Increment` vs `count++` в паралельному коді — яка різниця?
- `Task.WhenAll` vs `Task.WhenAny` — коли використовувати кожен?

---

### Завдання 4. AggregateException та обробка помилок паралельних задач

**Задача:** реалізувати `BuildPatientReportAsync(int patientId) → Task<PatientReport>`.

`PatientReport` — record із Patient, RecentAppointments, MedicalRecords, Dashboard.

Запустіть 4 задачі паралельно. Обробіть помилки двома способами:

**Спосіб 1 (ContinueWith — всі помилки):**
```csharp
var allTasks = Task.WhenAll(t1, t2, t3, t4);

_ = allTasks.ContinueWith(t =>
{
    foreach (var ex in t.Exception!.InnerExceptions)
        Console.WriteLine(ex.Message);
}, TaskContinuationOptions.OnlyOnFaulted);
```

**Спосіб 2 (await — перша помилка):**
```csharp
try { await allTasks; }
catch (Exception ex) { /* тільки перша InnerException */ }
```

Перевірте статус окремих задач через `task.IsCompletedSuccessfully`.

Поекспериментуйте: навмисно передайте неіснуючий `patientId` і спостерігайте, яке виключення виникне і де.

**Ключові питання:**
- Чому `await Task.WhenAll(...)` розгортає `AggregateException` до першої `InnerException`?
- `TaskContinuationOptions.OnlyOnFaulted` vs `OnlyOnRanToCompletion` — де кожен корисний?
- Якщо скасувати задачу — вона `IsFaulted` чи `IsCanceled`?

---

### Завдання 5. IProgress\<T\> — звітування про прогрес

**Задача:** реалізувати `BulkProcessAppointmentsAsync` з підтримкою `IProgress<T>`.

Тип прогресу: `(int Current, int Total, string Message)` — кортеж з поточним, загальним і повідомленням.

```
BulkProcessAppointmentsAsync(
    AppointmentStatus newStatus,
    IProgress<(int Current, int Total, string Message)>? progress = null,
    CancellationToken ct = default
) → Task<int>
```

Ключові моменти:
- `progress?.Report(...)` — null-safe виклик (якщо `progress == null` — нічого)
- `ct.ThrowIfCancellationRequested()` — явна перевірка на кожній ітерації
- `await Task.Delay(80, ct)` — симуляція async обробки

Виклик у Program.cs:

```csharp
var progress = new Progress<(int, int, string)>(p =>
    Console.WriteLine($"[{p.Item1}/{p.Item2}] {p.Item3}"));

await service.BulkProcessAppointmentsAsync(AppointmentStatus.Completed, progress, ct);
```

Після того як навчились — перепишіть виклик з Spectre.Console `AnsiConsole.Progress()` для кращого UI.

**Ключові питання:**
- Навіщо `IProgress<T>` замість прямого `Console.WriteLine` всередині методу?
- `Progress<T>` маршалює `Report()` на UI-потік — що це означає в WinForms? У консольному застосунку?
- Як зупинити `BulkProcessAppointmentsAsync` після 50% без `CancellationToken`?

---

### Завдання 6. HttpClient + async + JSON десеріалізація

**Задача:** створити `ClinicHttpClient` для запитів до FDA Open API (https://open.fda.gov/apis/).

API не потребує реєстрації. Запит: `GET /drug/label.json?search=openfda.brand_name:Aspirin&limit=1`.

Реалізуйте:

```
GetDrugInfoAsync(string drugName, CancellationToken ct) → Task<DrugInfo?>
IsApiAvailableAsync(CancellationToken ct) → Task<bool>
GetDrugInfoWithRaceAsync(string drugName, int timeoutMs) → Task<DrugInfo?>
```

`DrugInfo` — record: `Name`, `Purpose`, `Warnings`, `Dosage`.

**Для десеріалізації JSON** визначте private record-и з `[JsonPropertyName("...")]`:

```csharp
using System.Net.Http.Json;  // GetFromJsonAsync

private record FdaResponse(
    [property: JsonPropertyName("results")] List<FdaResult>? Results);
```

**ВАЖЛИВО — HttpClient lifecycle:**

```csharp
// ❌ НЕПРАВИЛЬНО — socket exhaustion:
using var http = new HttpClient();
var data = await http.GetFromJsonAsync<T>(url);

// ✅ ПРАВИЛЬНО — один статичний екземпляр:
private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };
```

Обробіть три типи помилок:
- `HttpRequestException` — мережева помилка
- `TaskCanceledException` when `!ct.IsCancellationRequested` — таймаут HttpClient
- `OperationCanceledException` від `ct` — **не ловити**, дозволити спливати

Реалізуйте `GetDrugInfoWithRaceAsync` через `Task.WhenAny` між API-запитом і `Task.Delay(timeoutMs)`.

Протестуйте на кількох препаратах: `Aspirin`, `Ibuprofen`, `Omeprazole`.

**Ключові питання:**
- Навіщо `Uri.EscapeDataString(drugName)` при формуванні URL?
- `using` для `HttpClient` — чому це антипатерн незважаючи на `IDisposable`?
- `GetFromJsonAsync<T>` повертає `null` при 204 No Content — що ще може призвести до `null`?

---

## Рефлексійні питання

1. **Deadlock з `.Result`/`.Wait()`.**
   ```csharp
   // WinForms / ASP.NET (старий):
   var patients = GetPatientsAsync().Result;  // може призвести до deadlock
   ```
   Поясніть механізм deadlock: `SynchronizationContext`, захоплення потоку, `ConfigureAwait(false)` як рішення. Чому в ASP.NET Core (без SynchronizationContext) deadlock не виникає?

2. **`async` всю дорогу ("async all the way").**
   Чому не можна зупинитися посередині: зробити один метод async, але його caller залишити синхронним з `.Result`? Намалюйте стек викликів і покажіть де виникне проблема.

3. **DbContext і thread-safety.**
   Чому `Parallel.ForEachAsync` з `FindAsync(_context, id)` всередині — небезпечно? Яке правильне рішення для паралельних операцій з БД у реальному застосунку? (`IDbContextFactory<T>`, окремі контексти на задачу)

4. **`Task.WhenAll` vs `Parallel.ForEachAsync`.**
   `Task.WhenAll` — для фіксованого набору задач. `Parallel.ForEachAsync` — для колекцій з обмеженням паралелізму. Якщо колекція містить 10 000 елементів — що буде з `Task.WhenAll(items.Select(ProcessAsync))`? Чому `MaxDegreeOfParallelism` важливий?

5. **`IProgress<T>` vs `event` для прогресу.**
   Обидва дозволяють повідомляти про прогрес. У чому перевага `IProgress<T>` перед подією? Розгляньте: thread-safety, типова безпека, тестованість.

6. **`HttpClient` socket exhaustion.**
   Якщо 1000 разів за секунду створювати `new HttpClient()` та одразу Dispose — що відбудеться на рівні ОС? Як `IHttpClientFactory` (ASP.NET Core) вирішує цю проблему?
