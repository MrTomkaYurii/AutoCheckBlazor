# Лабораторна робота 20 — EF Core: IQueryable, Pagination, Projections

## Проблема

Клініка зростає: 10 000 пацієнтів, 50 000 записів. Запит `context.Patients.ToList()` — завантажує **всі** 10 000 рядків у пам'ять, займає 3 секунди і 50 МБ.

Три типові помилки продуктивності:
1. Завантажити всі записи і потім відфільтрувати в C# — замість `WHERE` в SQL
2. Показати список з 1000 елементів замість сторінок по 20
3. Завантажити повний об'єкт (15 полів) коли потрібні тільки 3 — `SELECT *` замість `SELECT id, name, phone`

Усі три вирішуються зрозумінням одного принципу: **EF Core будує SQL-запит поступово до моменту матеріалізації**.

---

## Ключові концепції

### IQueryable&lt;T&gt; — відкладене виконання

`IQueryable<T>` — це не колекція. Це **опис запиту** (Expression Tree), який EF перетворить в SQL. SQL виконується тільки при матеріалізації:

| Операція | Чи виконується SQL? |
|----------|-------------------|
| `context.Patients` | ❌ ні |
| `.Where(p => p.LastName == "Коваль")` | ❌ ні |
| `.OrderBy(p => p.LastName)` | ❌ ні |
| `.Skip(20).Take(10)` | ❌ ні |
| **`.ToList()`** | ✅ **тут SQL виконується** |
| **`.Count()`** | ✅ **тут SQL виконується** |
| **`.FirstOrDefault()`** | ✅ **тут SQL виконується** |
| **`foreach`** | ✅ **тут SQL виконується** |

Якщо написати `.ToList()` між операціями — решта фільтрів виконається в C#, а не SQL:

```csharp
// ❌ ПРОБЛЕМА: завантажує ВСЕ, потім фільтрує в пам'яті
context.Patients.ToList().Where(p => p.Age > 18)

// ✅ ПРАВИЛЬНО: фільтрує в SQL — завантажує тільки результат
context.Patients.Where(p => p.DateOfBirth < cutoff).ToList()
```

### .Skip().Take() — пагінація

Стандартна пагінація: сторінка 1 = перші 20, сторінка 2 = наступні 20...

```
Skip((page - 1) * pageSize).Take(pageSize)
```

EF генерує SQL Server-синтаксис:
```sql
ORDER BY LastName
OFFSET 20 ROWS FETCH NEXT 20 ROWS ONLY
```

**Важливо:** `.Skip()/.Take()` без `.OrderBy()` — результат непередбачуваний (порядок у БД не гарантований).

**Рахуємо загальну кількість:** перед Skip/Take зробіть `int total = query.Count()` — окремий `SELECT COUNT(*)`.

### Проєкції — Select(new DTO)

Замість `SELECT *` — вибрати тільки потрібні стовпці:

```csharp
context.Patients.Select(p => new PatientSummaryDto(
    p.Id,
    p.FirstName + " " + p.LastName,  // обчислення в SQL
    p.Appointments.Count             // COUNT subquery
)).ToList()
```

EF генерує:
```sql
SELECT p.Id, p.FirstName + ' ' + p.LastName AS FullName,
       (SELECT COUNT(*) FROM Appointments WHERE PatientId = p.Id) AS AppointmentCount
FROM Patients
```

Не завантажуються: Email, RowVersion, MedicalRecords, navigation collections.

**DTO (Data Transfer Object)** — простий клас/record без логіки, тільки дані:
```csharp
public record PatientSummaryDto(int Id, string FullName, int Age, ...);
```

### Global Query Filter — автоматичний фільтр

`HasQueryFilter(p => !p.IsDeleted)` в `OnModelCreating` — EF додає `WHERE IsDeleted = 0` до **кожного** запиту до Patients, включно з `.Include()`.

**Переваги:** не потрібно щоразу писати `.Where(p => !p.IsDeleted)`.

**Обійти фільтр:** `.IgnoreQueryFilters()` — для адміністративних запитів.

**Попередження EF Core:** якщо Patient з фільтром — required кінець зв'язку з Appointment — EF попереджає про можливий неконсистентний стан. Це навмисна педагогічна ситуація.

---

## Завдання

### Завдання 1. IQueryable&lt;T&gt; — демонстрація відкладеного виконання

**Задача:** написати методи що демонструють різницю між IQueryable (фільтр у SQL) і IEnumerable (фільтр в C#).

Напишіть клас `ClinicQueryService` у `src/Data/` з методом:
```
DemoQueryableVsEnumerable(string filter) → (efficient, inefficient) списки
```

Перший варіант: `.Where(p => p.LastName.Contains(filter)).ToList()` — SQL WHERE.
Другий варіант: `.ToList().Where(p => ...)` — в пам'яті.

Щоб побачити різницю, включіть логування EF Core:
```csharp
optionsBuilder.LogTo(Console.WriteLine, LogLevel.Information);
```

Тимчасово додайте в `OnConfiguring` і зверніть увагу: перший варіант генерує SQL з `LIKE`, другий — без фільтру.

**Поверніть з методу `IQueryable<Patient>`:**
```
public IQueryable<Patient> QueryPatients()
    => _context.Patients.AsNoTracking();
```

Викликаючий код може додати умови після виклику:
```
var seniors = queryService.QueryPatients()
    .Where(p => p.DateOfBirth.Year < 1960)  // додається до того ж запиту!
    .OrderBy(p => p.LastName)
    .ToList();
```

**Ключові питання:**
- Коли саме `IQueryable` перетворюється на `IEnumerable` (перестає бути описом і стає даними)?
- Чому `.AsNoTracking()` важливий для read-only запитів у ClinicQueryService?

---

### Завдання 2. Пагінація

**Задача:** реалізувати `GetPatientsPaged` і `GetAppointmentsPaged` з підтримкою сортування і фільтрів.

**Структура результату:**
```
(List<T> Items, int TotalCount)
```
`TotalCount` — для UI: "Показано 1-20 з 347".

**Алгоритм `GetPatientsPaged`:**
1. Порахувати `total = query.Count()` до Skip/Take
2. Застосувати OrderBy (обов'язково!)
3. `.Skip((page-1) * pageSize).Take(pageSize).ToList()`

**Alгоритм `GetAppointmentsPaged` з необов'язковими фільтрами:**

Nullable параметри (`AppointmentStatus? status`, `int? patientId`) дозволяють динамічно додавати умови:
```
if (status.HasValue)
    query = query.Where(a => a.Status == status.Value);
```

Кожне `query = query.Where(...)` НЕ виконує SQL — це лише нарощування expression tree.

**Ключові питання:**
- Чому `query.Count()` після умов — правильно, а `query.ToList().Count` — ні?
- Що означає "без ORDER BY результат непередбачуваний"? Чи не завжди записи в тому ж порядку?

---

### Завдання 3. Проєкції та DTO

**Задача:** замість завантаження повних об'єктів — вибирати тільки потрібні поля через `Select(new DTO)`.

Створіть:
1. `PatientSummaryDto` — id, FullName, вік, телефон, тип крові, кількість записів
2. `AppointmentSummaryDto` — id, ім'я пацієнта, ім'я лікаря, спеціальність, дата, статус, вартість

Використайте C# `record` замість `class`:
```csharp
public record PatientSummaryDto(int Id, string FullName, int Age, ...);
```

`record` — це незмінний (immutable) тип даних, ідеальний для DTO: не має стану, не має логіки.

**Реалізуйте `GetPatientSummaries`:**

```csharp
context.Patients.Select(p => new PatientSummaryDto(
    p.Id,
    p.FirstName + " " + p.LastName,
    // Age — обчислення з DateOfBirth (EF може перетворити це в SQL)
    DateTime.Today.Year - p.DateOfBirth.Year,
    p.Phone,
    p.BloodType.ToString(),
    p.Appointments.Count  // → COUNT subquery
))
```

Увага: деякі C# методи EF не може перекласти у SQL (`a.GetType().Name` — не можна). В такому разі EF або кидає виняток, або матеріалізує дані спочатку а потім виконує в пам'яті. Що відбувається у вашій реалізації?

**Ключові питання:**
- Чим `record` відрізняється від `class` у C#?
- Що буде якщо написати `.Select(p => new { p.Id, p.FirstName })` — анонімний тип vs DTO?

---

### Завдання 4. Soft Delete та Global Query Filter

**Задача:** реалізувати "м'яке видалення" і Global Query Filter.

**Soft Delete патерн:**
Замість `context.Patients.Remove(patient)` → `patient.SoftDelete()` + `SaveChanges()`.

Додайте до `Patient`:
```csharp
public bool IsDeleted { get; private set; }
public void SoftDelete() { IsDeleted = true; }
```

**Global Query Filter в DbContext:**
```
entity.HasQueryFilter(p => !p.IsDeleted);
```

Після цього **кожен** запит до Patients автоматично включає `WHERE IsDeleted = 0`.

**Тест що все працює:**
1. `SoftDeletePatient(id)` — позначити як видаленого
2. `context.Patients.ToList()` — пацієнта НЕ видно (фільтр автоматичний)
3. `.IgnoreQueryFilters().ToList()` — пацієнт видно
4. `GetDeletedPatients()` — тільки видалені

**Попередження EF Core при міграції:**
EF видасть попередження: "Patient з QueryFilter є required кінцем зв'язку з Appointment".

Це означає: якщо пацієнта "видалено" (IsDeleted=true), запити до Appointments при `.Include(a => a.Patient)` можуть повернути `a.Patient == null` — хоча патч не видаляв рядок.

Прочитайте попередження і подумайте: як обійти цю проблему? Варіанти:
- Додати такий же фільтр до Appointment
- Зробити navigation property optional у FK конфігурації
- Прийняти обмеження і задокументувати поведінку

**Ключові питання:**
- Як відновити м'яко-видаленого пацієнта (set IsDeleted = false)?
- Чи потрібна міграція при додаванні `HasQueryFilter` або тільки при зміні схеми?

---

## Рефлексійні питання

1. **Expression Tree vs Delegate.** `IQueryable` використовує Expression Trees для трансляції в SQL. `IEnumerable` використовує `Func<T, bool>` делегати (виконуються в C#). Чому EF Core не може перетворити будь-який Func в SQL?

2. **Cursor-based pagination.** `.Skip().Take()` — "offset pagination". При великих обсягах (100 000+ записів) OFFSET дорогий. Альтернатива — keyset pagination: `WHERE Id > @lastId ORDER BY Id TAKE 20`. Коли варто переходити на keyset?

3. **DTO vs ViewModel.** DTO — для передачі даних між шарами. ViewModel — для відображення в UI (може містити обчислення для відображення). Чи є різниця між ними у вашому проєкті?

4. **record vs class для DTO.** C# record автоматично генерує: `Equals()`, `GetHashCode()`, `ToString()`, деструктуризацію. Чи потрібні ці методи для DTO? Чи є сценарії де `class` краще?

5. **Soft Delete і унікальні індекси.** У Doctors є унікальний індекс `UX_Doctors_License`. Якщо лікаря "видалити" soft delete, а потім додати нового з тим же номером ліцензії — унікальний індекс не дозволить. Як вирішити?

6. **Matеріалізація в середині chain.** Де помилка?
```csharp
var result = context.Patients
    .AsNoTracking()
    .ToList()                          // матеріалізація тут!
    .GroupBy(p => p.BloodType)
    .Select(g => new { g.Key, Count = g.Count() })
    .ToList();
```
Чому цей код правильно компілюється, але є проблемою продуктивності?
