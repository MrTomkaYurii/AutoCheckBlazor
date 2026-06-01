# Лаба 06 — Успадкування

## Мета

Навчитися будувати ієрархії класів через успадкування: визначати спільну поведінку в абстрактному базовому класі, зобов'язувати підкласи реалізовувати абстрактні методи, перевизначати `virtual` методи та безпечно працювати з об'єктами різних типів через базовий тип.

## Контекст

Система вже вміє зберігати пацієнтів, лікарів і записи на прийом. Але медична картка пацієнта — окрема сутність: лікар додає різні типи записів (діагноз, результат аналізу, рецепт). Всі вони мають спільні атрибути (хто, кому, коли), але відрізняються за структурою та поведінкою.

Якщо зберігати кожен тип окремо — код дублюється і систему важко розширити. Успадкування вирішує це: одна база, різні підкласи.

---

## Гілка

```bash
git checkout main
git checkout -b feature/inheritance
```

> Гілка **зливається в `main`** після завершення всіх завдань.

---

## Задача 1. Abstract клас `MedicalRecord` + `Diagnosis` ⭐⭐

### Умова

Усі медичні записи мають спільні поля: хто пацієнт, який лікар, коли зроблено. Але вміст кожного типу різний — діагноз, аналіз і рецепт несуть зовсім різну інформацію.

`abstract class` дозволяє оголосити спільний контракт: визначити що **є** у кожного запису, і що кожен підклас **зобов'язаний** реалізувати. Поки клас абстрактний — `new MedicalRecord(...)` неможливий, тільки `new Diagnosis(...)`.

**Що реалізувати:**

1. `abstract class MedicalRecord` у `src/Models/`:
   - `Id` (статичний лічильник, як у `Patient`)
   - `PatientId`, `DoctorId`, `Date`, `Notes`
   - `protected` конструктор — ініціалізує спільні поля і перевіряє `PatientId > 0`, `DoctorId > 0` через `ClinicValidator.ValidatePositive`
   - `abstract string GetSummary()` — зміст запису, кожен підклас реалізує по-своєму
   - `virtual string GetRecordType()` — базова реалізація: `"Медичний запис"`
   - `virtual bool IsActive()` — базова реалізація: запис активний якщо давніший не більше 6 місяців
   - `override ToString()` — використовує `GetRecordType()` і `GetSummary()`

2. Перший конкретний підклас `Diagnosis : MedicalRecord`:
   - Приватні поля `_diagnosisCode`, `_description` з явними сеттерами — валідація через `ClinicValidator.ValidateName`
   - Публічні властивості: `DiagnosisCode`, `Description`, `IsChronic`
   - Конструктор що викликає `base(...)` і присвоює через властивості
   - `override GetSummary()` — `"I10: Гіпертонічна хвороба [хронічне]"`
   - `override GetRecordType()` — `"Діагноз"`

### Специфікація

| Член | Тип | Опис |
|------|-----|------|
| `Id` | `int` (get only) | Авто-лічильник |
| `PatientId` | `int` (get only) | ID пацієнта |
| `DoctorId` | `int` (get only) | ID лікаря |
| `Date` | `DateTime` (get only) | Дата запису |
| `Notes` | `string` (get; set) | Додаткові нотатки |
| `GetSummary()` | `abstract string` | Зміст запису |
| `GetRecordType()` | `virtual string` | Тип: `"Медичний запис"` |
| `IsActive()` | `virtual bool` | Давніший ≤ 6 місяців |
| `ToString()` | `override` | `"[1] Діагноз | 09.05.2026 | I10: Гіпертонічна хвороба"` |

### Приклад

```csharp
// abstract — не можна створити безпосередньо:
// MedicalRecord r = new MedicalRecord(...);  // помилка компіляції!

// Тільки через підклас:
Diagnosis d = new Diagnosis(1, 1, DateTime.Today, "I10", "Гіпертонічна хвороба", isChronic: true);
Console.WriteLine(d.GetRecordType());  // "Діагноз"
Console.WriteLine(d.GetSummary());     // "I10: Гіпертонічна хвороба [хронічне]"
Console.WriteLine(d);                  // "[1] Діагноз | 09.05.2026 | I10: Гіпертонічна хвороба [хронічне]"
Console.WriteLine(d.IsActive());       // true (щойно створено)

// Базовий тип може зберігати підклас:
MedicalRecord record = new Diagnosis(1, 1, DateTime.Today, "J06.9", "Ринофарингіт");
Console.WriteLine(record.GetRecordType()); // "Діагноз" — виклик іде в підклас!
```

### Підказки

1. `abstract class` оголошується ключовим словом `abstract`. Він може мати і звичайні методи, і `abstract` методи:
   ```csharp
   public abstract class MedicalRecord
   {
       public abstract string GetSummary();         // підклас ЗОБОВ'ЯЗАНИЙ реалізувати
       public virtual string GetRecordType() => "Медичний запис"; // підклас МОЖЕ перевизначити
   }
   ```
2. `abstract` метод не має тіла (немає `{ }`). Якщо підклас не реалізує `abstract` метод — помилка компіляції.
3. `virtual` метод має тіло за замовчуванням. Підклас може (`override`) або не може його перевизначати.
4. `protected` конструктор — видимий тільки в підкласах через `base(...)`. Ззовні `new MedicalRecord(...)` неможливий:
   ```csharp
   protected MedicalRecord(int patientId, int doctorId, DateTime date) { ... }
   ```
5. У підкласі конструктор викликає батьківський через `: base(...)`:
   ```csharp
   public Diagnosis(int patientId, int doctorId, DateTime date, string code, string desc, bool isChronic = false)
       : base(patientId, doctorId, date)
   {
       DiagnosisCode = code;
       // ...
   }
   ```
6. `override ToString()` у базовому класі використовує `virtual`/`abstract` методи — кожен підклас отримує правильний рядок автоматично:
   ```csharp
   public override string ToString() =>
       "[" + Id + "] " + GetRecordType() + " | " + Date.ToString("dd.MM.yyyy") + " | " + GetSummary();
   ```

📖 [Abstract and sealed classes and class members](https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/abstract-and-sealed-classes-and-class-members)
📖 [virtual (C# Reference)](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/virtual)
📖 [Inheritance (C# Programming Guide)](https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/inheritance)

### Адаптація до вашого домену

| Клініка | Готель | Ресторан | Університет | Прокат авто | Бібліотека | Спортзал |
|---------|--------|----------|-------------|-------------|------------|---------|
| `MedicalRecord` (abstract) | `GuestRecord` (abstract) | `OrderRecord` (abstract) | `AcademicRecord` (abstract) | `ServiceRecord` (abstract) | `LibraryRecord` (abstract) | `GymRecord` (abstract) |
| `Diagnosis` (перший підклас) | `Complaint` | `FeedbackEntry` | `GradeEntry` | `DamageReport` | `LoanRecord` | `ProgressEntry` |
| `abstract GetSummary()` | `abstract GetSummary()` | `abstract GetSummary()` | `abstract GetSummary()` | `abstract GetSummary()` | `abstract GetSummary()` | `abstract GetSummary()` |
| `virtual GetRecordType()` → `"Медичний запис"` | → `"Запис гостя"` | → `"Замовлення"` | → `"Академічний запис"` | → `"Сервісний запис"` | → `"Бібліотечний запис"` | → `"Запис у клубі"` |

### Коміт

```bash
git add src/Models/MedicalRecord.cs src/Models/Diagnosis.cs
git commit -m "Lab06 Task1: add abstract MedicalRecord base class and Diagnosis subclass"
```

---

## Задача 2. `LabResult` та `Prescription` + `MedicalRecordManager` ⭐⭐⭐

### Умова

`Diagnosis` — лише один із типів медичних записів. Результат аналізу (`LabResult`) має числове значення, одиниці виміру і ознаку норми. Рецепт (`Prescription`) — назву препарату, дозування і тривалість курсу.

Кожен підклас реалізує `GetSummary()` по-своєму і за потреби перевизначає `virtual` методи — наприклад, `Prescription` змінює логіку `IsActive()`: рецепт активний допоки не закінчився курс, незалежно від 6-місячного правила.

`MedicalRecordManager` зберігає **поліморфний масив** `MedicalRecord[]` — в одному масиві живуть діагнози, аналізи і рецепти. `DisplayAll()` перебирає масив і викликає `ToString()` — кожен об'єкт виводить свій рядок.

**Що реалізувати:**

1. `LabResult : MedicalRecord`:
   - Приватні поля `_testName`, `_unit`, `_referenceRange` — валідація через `ClinicValidator.ValidateName`
   - `Value` (double) і `IsNormal` (bool) — auto-property без валідації
   - `override GetSummary()` → `"Гемоглобін: 145 г/л (норма: 120–160)"`; якщо поза нормою — додати `" ⚠ поза нормою"`
   - `override GetRecordType()` → `"Аналіз"`

2. `Prescription : MedicalRecord`:
   - Приватні поля `_medicationName`, `_dosage` — валідація через `ClinicValidator.ValidateName`
   - Приватне поле `_durationDays` — валідація через `ClinicValidator.ValidatePositive`
   - `Instructions` — auto-property (необов'язкове поле, не валідується)
   - Обчислювана властивість `ExpiresAt` → `Date.AddDays(DurationDays)`
   - `override GetSummary()` → `"Лізиноприл 10 мг × 30 днів (1 раз на добу вранці)"`
   - `override GetRecordType()` → `"Рецепт"`
   - `override IsActive()` → `ExpiresAt >= DateTime.Today` (замість 6-місячного правила)

3. `MedicalRecordManager` у `src/Managers/`:
   - Поліморфний масив `MedicalRecord[] _records` (ліміт 1000)
   - `Add(MedicalRecord)`, `FindById(int)`
   - `GetByPatient(int)` → `MedicalRecord[]`
   - `GetByDoctor(int)` → `MedicalRecord[]`
   - `DisplayAll()`, `DisplayList(MedicalRecord[])`
   - Індексатор `this[int index]`

### Приклад

```csharp
LabResult lr = new LabResult(1, 1, DateTime.Today, "Холестерин", 6.2, "ммоль/л", "< 5.2", isNormal: false);
Console.WriteLine(lr);
// [3] Аналіз | 09.05.2026 | Холестерин: 6.2 ммоль/л (норма: < 5.2) ⚠ поза нормою

Prescription rx = new Prescription(1, 1, DateTime.Today.AddDays(-5), "Лізиноприл", "10 мг", 30, "вранці");
Console.WriteLine(rx.IsActive());   // true — курс 30 днів, тільки 5 минуло
Console.WriteLine(rx.ExpiresAt.ToString("dd.MM.yyyy"));  // через 25 днів

// Поліморфний масив — різні типи, одне сховище:
MedicalRecord[] records = manager.GetByPatient(1);
for (int i = 0; i < records.Length; i++)
    Console.WriteLine(records[i]);  // кожен виводить свій ToString()
```

### Підказки

1. `override IsActive()` у `Prescription` повністю замінює базову реалізацію:
   ```csharp
   public override bool IsActive() => ExpiresAt >= DateTime.Today;
   ```
2. Поліморфний масив: `MedicalRecord[]` може зберігати будь-який підклас.
   ```csharp
   _records[0] = new Diagnosis(...);     // OK
   _records[1] = new LabResult(...);     // OK
   _records[2] = new Prescription(...);  // OK
   ```
3. `DisplayAll()` не знає реального типу кожного запису — і не мусить. `ToString()` вирішує:
   ```csharp
   for (int i = 0; i < _count; i++)
       Console.WriteLine(_records[i]);  // викликається override ToString() підкласу
   ```
4. Це і є поліморфізм: один код `Console.WriteLine(_records[i])` поводиться по-різному залежно від реального типу об'єкта.
5. Валідація в підкласах будується за тим самим патерном що і в `Patient`/`Doctor` з Lab 05: приватне поле + явний сеттер + `ClinicValidator`. Новий клас — нові правила, але **один і той самий** `ClinicValidator`.

📖 [Polymorphism (C# Programming Guide)](https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/polymorphism)
📖 [override (C# Reference)](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/override)

### Адаптація до вашого домену

| Клініка | Готель | Ресторан | Університет | Прокат авто | Бібліотека | Спортзал |
|---------|--------|----------|-------------|-------------|------------|---------|
| `MedicalRecord` | `GuestRecord` | `OrderRecord` | `AcademicRecord` | `ServiceRecord` | `LibraryRecord` | `GymRecord` |
| `Diagnosis` | `Complaint` | `FeedbackEntry` | `GradeEntry` | `DamageReport` | `LoanRecord` | `ProgressEntry` |
| `LabResult` | `RoomInspection` | `QualityCheck` | `ExamResult` | `TechInspection` | `BookReturn` | `FitnessTest` |
| `Prescription` | `ServiceRequest` | `SpecialOrder` | `Assignment` | `RepairOrder` | `FineNotice` | `TrainingPlan` |

### Коміт

```bash
git add src/Models/LabResult.cs src/Models/Prescription.cs src/Managers/MedicalRecordManager.cs
git commit -m "Lab06 Task2: add LabResult, Prescription subclasses and MedicalRecordManager"
```

---

## Задача 3. `is`, `as` — фільтрація за типом ⭐⭐⭐

### Умова

Масив `MedicalRecord[]` зберігає об'єкти різних типів. Але часто потрібно отримати тільки діагнози, або тільки хронічні, або тільки активні рецепти. Це вимагає **перевірки реального типу** об'єкта під час виконання.

`is` — оператор перевірки типу. `as` — безпечне приведення: повертає `null` якщо тип не збігається, замість `InvalidCastException`.

**Що реалізувати:**

Додати до `MedicalRecordManager`:

1. `GetDiagnoses(int patientId)` → `Diagnosis[]` — всі діагнози пацієнта
2. `GetLabResults(int patientId)` → `LabResult[]` — всі аналізи пацієнта
3. `GetPrescriptions(int patientId)` → `Prescription[]` — всі рецепти пацієнта
4. `GetChronicDiagnoses(int patientId)` → `Diagnosis[]` — тільки хронічні
5. `GetActivePrescriptions(int patientId)` → `Prescription[]` — тільки активні (через `IsActive()`)
6. `DisplayPatientSummary(int patientId)` — зведена картка:
   - Кількість записів кожного типу
   - Список хронічних діагнозів (якщо є)
   - Список активних рецептів з датою закінчення (якщо є)

### Приклад

```csharp
// is — перевірка типу, повертає bool:
MedicalRecord r = new Diagnosis(...);
if (r is Diagnosis) Console.WriteLine("це діагноз");

// is з pattern variable — перевірка і приведення одночасно:
if (r is Diagnosis d)
    Console.WriteLine(d.DiagnosisCode);  // d вже має тип Diagnosis

// as — спробувати привести, або null:
Diagnosis? diag = r as Diagnosis;
if (diag != null)
    Console.WriteLine(diag.IsChronic);

// Фільтрація в методі:
public Diagnosis[] GetDiagnoses(int patientId)
{
    int n = 0;
    for (int i = 0; i < _count; i++)
        if (_records[i].PatientId == patientId && _records[i] is Diagnosis) n++;

    Diagnosis[] result = new Diagnosis[n];
    int idx = 0;
    for (int i = 0; i < _count; i++)
        if (_records[i].PatientId == patientId && _records[i] is Diagnosis d)
            result[idx++] = d;
    return result;
}
```

```csharp
// Використання:
manager.DisplayPatientSummary(1);
// === Медична картка пацієнта #1 ===
// Всього записів: 5 (діагнозів: 2, аналізів: 2, рецептів: 1)
// Хронічні діагнози (1):
//   [1] Діагноз | 09.04.2026 | I10: Гіпертонічна хвороба [хронічне]
// Активні рецепти (1):
//   [5] Рецепт | 04.05.2026 | Лізиноприл 10 мг × 30 днів | до 03.06.2026
```

### Підказки

1. Різниця `is` та `as`:

   | | `is` | `as` |
   |---|---|---|
   | Повертає | `bool` | об'єкт або `null` |
   | Кидає виняток? | ніколи | ніколи |
   | Явне приведення `(T)obj` | — | так, кидає `InvalidCastException` при невдачі |

2. Pattern variable `is T variable` — сучасний стиль C#, замінює `is` + `as`:
   ```csharp
   // Старий стиль:
   if (r is Diagnosis)
   {
       Diagnosis d = (Diagnosis)r;
       // ...
   }
   // Новий стиль (одна операція):
   if (r is Diagnosis d)
   {
       // d одразу типу Diagnosis
   }
   ```
3. Комбінація умов: `_records[i] is Diagnosis d && d.IsChronic` — тільки хронічні діагнози.
4. Явне приведення `(Diagnosis)record` — кидає `InvalidCastException` якщо тип не збігається. Використовуйте `is`/`as` коли не впевнені у типі.

📖 [Type-testing operators and cast expressions](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/type-testing-and-cast)
📖 [Pattern matching overview](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/functional/pattern-matching)

### Адаптація до вашого домену

| Клініка | Готель | Ресторан | Університет | Прокат авто | Бібліотека | Спортзал |
|---------|--------|----------|-------------|-------------|------------|---------|
| `GetDiagnoses(patientId)` | `GetComplaints(guestId)` | `GetFeedback(customerId)` | `GetGrades(studentId)` | `GetDamageReports(clientId)` | `GetLoanRecords(readerId)` | `GetProgress(memberId)` |
| `GetLabResults(patientId)` | `GetRoomInspections(guestId)` | `GetQualityChecks(customerId)` | `GetExamResults(studentId)` | `GetTechInspections(clientId)` | `GetReturns(readerId)` | `GetFitnessTests(memberId)` |
| `GetActivePrescriptions(patientId)` | `GetActiveRequests(guestId)` | `GetActiveOrders(customerId)` | `GetActiveAssignments(studentId)` | `GetActiveRepairs(clientId)` | `GetActiveFines(readerId)` | `GetActivePlans(memberId)` |
| `DisplayPatientSummary` | `DisplayGuestSummary` | `DisplayCustomerSummary` | `DisplayStudentSummary` | `DisplayClientSummary` | `DisplayReaderSummary` | `DisplayMemberSummary` |

### Коміт

```bash
git add src/Managers/MedicalRecordManager.cs
git commit -m "Lab06 Task3: add type-filtered queries using is/as pattern matching"
```

---

## Задача 4. Інтеграція — `Clinic` + меню + тестові дані ⭐⭐⭐⭐

### Умова

Ієрархія класів і менеджер готові. Тепер потрібно підключити їх до системи: `Clinic` отримує новий менеджер, `Program.cs` — новий розділ меню, а в тестових даних з'являються реальні приклади кожного типу.

Ця задача демонструє **силу поліморфізму в реальному контексті**: код меню не знає реальних типів записів. Він викликає `DisplayList(MedicalRecord[])` — і кожен запис виводить себе правильно.

**Що реалізувати:**

1. У `Clinic.cs` додати властивість `MedicalRecordManager MedicalRecords`.

2. У тестових даних `Program.cs` додати приклади всіх трьох типів для кількох пацієнтів:
   - Хронічний і гострий діагноз для одного пацієнта
   - Аналіз в нормі і поза нормою
   - Активний і вже завершений рецепт (для демонстрації `IsActive()`)

3. Новий розділ меню "Медична картка" з пунктами:
   - `1` — Картка пацієнта (зведення через `DisplayPatientSummary`)
   - `2` — Всі записи пацієнта
   - `3` — Додати діагноз
   - `4` — Додати аналіз
   - `5` — Додати рецепт
   - `6` — Записи лікаря

4. Продемонструвати поліморфізм явно: вивести всі записи одного пацієнта — масив `MedicalRecord[]` містить різні типи, але `foreach` + `ToString()` дає правильний рядок для кожного.

### Приклад

```csharp
// Clinic.cs:
public MedicalRecordManager MedicalRecords { get; }
// у конструкторі:
MedicalRecords = new MedicalRecordManager();

// Тестові дані в Program.cs:
clinic.MedicalRecords.Add(new Diagnosis(1, 1, DateTime.Today.AddDays(-30), "I10", "Гіпертонічна хвороба", isChronic: true));
clinic.MedicalRecords.Add(new LabResult(1, 1, DateTime.Today.AddDays(-7), "Холестерин", 6.2, "ммоль/л", "< 5.2", isNormal: false));
clinic.MedicalRecords.Add(new Prescription(1, 1, DateTime.Today.AddDays(-5), "Лізиноприл", "10 мг", 30, "вранці"));

// Поліморфний вивід — один код, різна поведінка:
MedicalRecord[] records = clinic.MedicalRecords.GetByPatient(1);
for (int i = 0; i < records.Length; i++)
    Console.WriteLine(records[i].GetRecordType() + ": " + records[i].GetSummary());
// Діагноз: I10: Гіпертонічна хвороба [хронічне]
// Аналіз: Холестерин: 6.2 ммоль/л (норма: < 5.2) ⚠ поза нормою
// Рецепт: Лізиноприл 10 мг × 30 днів (вранці)
```

### Підказки

1. `MedicalRecord` у `Clinic.cs` вимагає `using ClinicApp.Managers;` — переконайтесь, що using є.
2. Меню "Медична картка" — окрема `static void MedicalRecordsMenu(Clinic clinic)` за зразком існуючих меню.
3. У пунктах "Додати діагноз/аналіз/рецепт" огорніть конструктор у `try/catch` — підкласи кидають `ArgumentException` при некоректних даних:
   ```csharp
   try
   {
       clinic.MedicalRecords.Add(new Diagnosis(patientId, doctorId, DateTime.Today, code, desc, isChronic));
   }
   catch (ArgumentOutOfRangeException e) { Console.WriteLine("Помилка: " + e.Message); }
   catch (ArgumentException e) { Console.WriteLine("Помилка: " + e.Message); }
   ```
4. Для "завершеного рецепту" в тестових даних: `DateTime.Today.AddDays(-40)` з `DurationDays = 10` — курс закінчився 30 днів тому, `IsActive()` поверне `false`.
5. Перевірте: `DisplayPatientSummary` для пацієнта без жодного хронічного діагнозу — не виводить порожній розділ.
6. Ключовий момент для самоперевірки: у методі `DisplayList(MedicalRecord[] records)` немає жодного `if`, жодного `is`. Це і є поліморфізм — код не знає типів, але поводиться правильно:
   ```csharp
   public void DisplayList(MedicalRecord[] records)
   {
       for (int i = 0; i < records.Length; i++)
           Console.WriteLine(records[i]);  // викликає override ToString() підкласу
   }
   ```

📖 [base keyword](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/base)
📖 [Inheritance and polymorphism (tutorial)](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/tutorials/inheritance)

### Адаптація до вашого домену

| Клініка | Готель | Ресторан | Університет | Прокат авто | Бібліотека | Спортзал |
|---------|--------|----------|-------------|-------------|------------|---------|
| `Clinic.MedicalRecords` | `Hotel.GuestHistory` | `Restaurant.OrderHistory` | `University.AcademicHistory` | `Fleet.ServiceHistory` | `Library.LibraryRecords` | `GymCenter.GymRecords` |
| Меню "Медична картка" | "Історія гостя" | "Замовлення" | "Успішність" | "Сервісна книжка" | "Картка читача" | "Картка учасника" |

### Коміт

```bash
git add src/Clinic.cs src/Program.cs
git commit -m "Lab06 Task4: integrate MedicalRecords into Clinic and Program menu"
```

---

## Перевірка перед здачею

```bash
cd src
dotnet build
dotnet run
```

Переконайтесь, що:

- [ ] `new MedicalRecord(...)` не компілюється — клас абстрактний
- [ ] `Diagnosis`, `LabResult`, `Prescription` успішно створюються
- [ ] `MedicalRecord record = new Diagnosis(...)` — присвоєння підкласу базовому типу працює
- [ ] `record.GetRecordType()` повертає `"Діагноз"`, а не `"Медичний запис"`
- [ ] `Prescription.IsActive()` повертає `false` для рецепту що закінчився
- [ ] `DisplayList(MedicalRecord[])` виводить різні рядки для різних типів — без жодного `if (r is ...)`
- [ ] `GetChronicDiagnoses` повертає тільки хронічні
- [ ] `DisplayPatientSummary` правильно рахує типи і показує зведення
- [ ] Меню "Медична картка" (пункт 4) доступне і всі підпункти працюють
- [ ] `new Diagnosis(1, 1, DateTime.Today, "", "Ринофарингіт")` кидає `ArgumentException`
- [ ] `new Prescription(1, 1, DateTime.Today, "Аспірин", "500 мг", 0)` кидає `ArgumentOutOfRangeException`
- [ ] При введенні порожнього коду діагнозу в меню — програма показує повідомлення про помилку, а не падає

---

## Питання для самоперевірки

1. Чому `abstract class` не можна інстанціювати? Що відбувається при спробі `new MedicalRecord(...)`?
2. Яка різниця між `abstract` і `virtual` методом? Що станеться якщо підклас не реалізує `abstract` метод?
3. Чому `override ToString()` у базовому класі викликає `GetSummary()` підкласу, а не базового? Як це називається?
4. Навіщо `protected` конструктор у базовому класі? Чим він відрізняється від `public` і `private`?
5. Яка різниця між `is`, `as` і явним приведенням `(Diagnosis)record`? Коли кожен із них кидає виняток?
6. Чому `Prescription.IsActive()` перевизначає логіку, а `LabResult.IsActive()` ні? Як базовий клас "знає" яку реалізацію викликати?
7. Метод `DisplayList(MedicalRecord[])` не містить жодного `if (r is ...)`, але виводить різні рядки для різних типів. Чому це можливо?
8. Чому `ClinicValidator` викликається і в базовому конструкторі (`ValidatePositive` для `patientId`, `doctorId`), і у сеттерах підкласів (`ValidateName` для назв)? Де саме "живе" відповідальність за кожну перевірку?

---

## Злиття

```bash
git checkout main
git merge --no-ff feature/inheritance -m "Merge feature/inheritance: Lab06 Inheritance"
```

> Наступна лаба: `git checkout -b feature/polymorphism` — `new` keyword, `sealed`, `base.Method()`.
