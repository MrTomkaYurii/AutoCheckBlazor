# Лаба 03 — Визначення класів

## Мета
Навчитися проєктувати та реалізовувати класи з полями, властивостями, конструкторами і методами — і зібрати перші три сутності медичної системи у робочий консольний застосунок.

## Контекст

Попередні лаби були **пісочницями** — окремими мінімальними проєктами для відпрацювання синтаксису. З цієї лаби починається **основний проєкт** `src/ClinicApp`, який буде рости протягом усіх 21 лаб.

Станом на початок цієї лаби:
- `sandbox/intro/` — базовий синтаксис C# (Лаба 01)
- `sandbox/arrays/` — масиви (Лаба 02)
- `src/` — **порожньо** (тут і починаємо)

Після цієї лаби у `src/` з'являться класи `Patient`, `Doctor`, `Appointment`, три менеджери колекцій та клас `Clinic`, що їх об'єднує.

---

## Гілка для цієї лаби

```bash
git checkout main
git checkout -b feature/catalog
```

> Гілка `feature/catalog` **зливається в `main`** після завершення всіх завдань.

---

## Створення проєкту

```bash
dotnet new console -o src --name ClinicApp
```

Перевірте, що з'явилися `src/ClinicApp.csproj` і `src/Program.cs`. Відредагуйте `.csproj`:

```xml
<PropertyGroup>
  <OutputType>Exe</OutputType>
  <TargetFramework>net8.0</TargetFramework>
  <Nullable>enable</Nullable>
  <ImplicitUsings>enable</ImplicitUsings>
</PropertyGroup>
```

Перший запуск:

```bash
cd src
dotnet run
```

Має вивести `Hello, World!`. Поверніться в корінь і зробіть перший коміт:

```bash
cd ..
git add src/
git commit -m "Lab03: create src/ClinicApp console project"
```

---

## Як виконувати завдання

На відміну від пісочниць, тут **не потрібно** файлів `Task1.cs`, `Task2.cs`. Кожне завдання додає новий файл із класом до **одного спільного проєкту**. Клас зразу використовується у спільному `Program.cs`.

Структура після завершення всіх завдань:

```
src/
├── ClinicApp.csproj
├── Program.cs              ← меню та тестовий код
├── Patient.cs              ← Задача 1
├── Doctor.cs               ← Задача 2
├── PatientManager.cs       ← Задача 3
├── DoctorManager.cs        ← Задача 4
├── Appointment.cs          ← Задача 5
├── AppointmentManager.cs   ← Задача 6
├── Clinic.cs               ← Задача 7
└── GrowablePatientManager.cs ← Задача 8
```

Усі файли — у **просторі імен `ClinicApp`**:
```csharp
namespace ClinicApp;

public class Patient { ... }
```

---

## Задача 1. Клас Patient ⭐⭐

### Умова

Створіть клас `Patient`, який описує пацієнта медичної клініки. Клас повинен мати автоматичний лічильник ID, три конструктори з ланцюжком виклику, обчислювані властивості та метод класифікації.

У `Program.cs` створіть 5 примірників різними конструкторами і виведіть їх на екран.

### Специфікація класу

| Член класу | Тип | Опис |
|------------|-----|------|
| `_nextId` | `private static int` | Лічильник, починається з 1 |
| `Id` | `public int` (readonly) | Автоматично призначається в конструкторі |
| `FirstName` | `public string` | Ім'я |
| `LastName` | `public string` | Прізвище |
| `DateOfBirth` | `public DateTime` | Дата народження |
| `BloodType` | `public string` | Група крові (`"A+"`, `"B-"`, `"O+"`, `"AB+"` тощо) |
| `Phone` | `public string` | Номер телефону |
| `Email` | `public string` | Email (порожній рядок якщо невідомий) |
| `FullName` | обчислювана `string` | `FirstName + " " + LastName` |
| `Age` | обчислювана `int` | Повних років з урахуванням дня народження |
| `IsAdult` | обчислювана `bool` | `Age >= 18` |
| `Patient()` | конструктор | Значення за замовчуванням |
| `Patient(firstName, lastName)` | конструктор | Ланцюжок до повного |
| `Patient(firstName, lastName, dob, bloodType, phone)` | конструктор | Повний — призначає Id |
| `GetAgeCategory()` | `public string` | `"дитина"` / `"дорослий"` / `"літній"` |
| `ToString()` | override | Повна рядкова форма |

### Приклад виводу

```
[1] Іван Петренко | Вік: 41 (дорослий) | Кров: A+ | Тел: 0501234567
[2] Олена Коваль | Вік: 33 (дорослий) | Кров: B- | Тел: 0672345678
[3] Максим Бойко | Вік: 16 (дитина) | Кров: O+ | Тел: 0933456789
[4] Невідомий Пацієнт | Вік: 26 (дорослий) | Кров: Невідомо | Тел: 0000000000
[5] Марія Ткач | Вік: 26 (дорослий) | Кров: Невідомо | Тел: 0000000000
```

### Підказки

1. Оголосіть `private static int _nextId = 1;` на рівні класу. Статичне поле — одне для всіх примірників.
2. Властивість `Id` лише з гетером: `public int Id { get; }` (ініціалізується тільки в конструкторі).
3. Повний конструктор присвоює `Id = _nextId++;` — постінкремент спершу бере значення, потім збільшує на 1.
4. Ланцюжок конструкторів через `: this(...)`:
   ```csharp
   public Patient() : this("Невідомий", "Пацієнт") { }
   public Patient(string f, string l) : this(f, l, new DateTime(2000, 1, 1), "Невідомо", "0000000000") { }
   public Patient(string f, string l, DateTime dob, string bt, string phone) { Id = _nextId++; ... }
   ```
5. Вік враховує, чи вже був день народження цього року:
   ```csharp
   int age = DateTime.Today.Year - DateOfBirth.Year;
   if (DateOfBirth.Date > DateTime.Today.AddYears(-age)) age--;
   ```
6. `GetAgeCategory()`:
   ```csharp
   if (Age < 18) return "дитина";
   if (Age < 60) return "дорослий";
   return "літній";
   ```
7. У `Program.cs` створіть мінімум 5 пацієнтів: хоча б одним конструктором без параметрів, одним з ім'ям та прізвищем, трьома — повним.

### Адаптація до вашого домену

| Клініка | Готель | Ресторан | Університет | Прокат авто | Бібліотека | Спортзал |
|---------|--------|----------|-------------|-------------|------------|---------|
| `Patient` | `Guest` | `Customer` | `Student` | `Client` | `Reader` | `Member` |
| `DateOfBirth` / `Age` | `CheckInDate` / `StayDays` | `LastVisit` | `EnrollmentYear` | `DriverSince` | `MemberSince` / `YearsActive` | `JoinDate` / `MembershipDays` |
| `BloodType` | `RoomPreference` | `DietaryRestrictions` | `Faculty` | `LicenseCategory` | `ReaderCategory` | `MembershipType` |
| `IsAdult` | `IsVip` | `IsRegular` | `IsFinalYear` | `HasFullLicense` | `IsActive` | `IsActiveMember` |
| `GetAgeCategory()` | `GetTierCategory()` | `GetLoyaltyCategory()` | `GetYearCategory()` | `GetRiskCategory()` | `GetCategoryName()` | `GetMemberCategory()` |

### Коміт

```bash
git add src/Patient.cs src/Program.cs
git commit -m "Lab03 Task1: add Patient class with 3 constructors and computed properties"
```

---

## Задача 2. Клас Doctor ⭐⭐

### Умова

Створіть клас `Doctor` для лікаря клініки. Лікар має **робочий графік** — година початку та кінця роботи (ціле число від 0 до 23). Клас повинен вміти перевіряти, чи доступний лікар у задану годину.

У `Program.cs` створіть 3–4 лікарі та виведіть їх, зокрема показуючи, хто доступний зараз.

### Специфікація класу

| Член класу | Тип | Опис |
|------------|-----|------|
| `_nextId` | `private static int` | Лічильник |
| `Id` | `public int` (readonly) | Автопризначення |
| `FirstName` | `public string` | Ім'я |
| `LastName` | `public string` | Прізвище |
| `Speciality` | `public string` | Спеціалізація (`"Кардіологія"`, `"Педіатрія"` тощо) |
| `LicenseNumber` | `public string` | Номер ліцензії |
| `Phone` | `public string` | Телефон |
| `WorkStartHour` | `public int` | Година початку роботи (0–23), за замовчуванням 8 |
| `WorkEndHour` | `public int` | Година кінця роботи (0–23), за замовчуванням 17 |
| `FullName` | обчислювана `string` | `FirstName + " " + LastName` |
| `WorkingHoursPerDay` | обчислювана `int` | `WorkEndHour - WorkStartHour` |
| `WorkSchedule` | обчислювана `string` | Рядок вигляду `"08:00–17:00"` |
| `IsAvailableNow` | обчислювана `bool` | Поточна година в межах `[WorkStartHour, WorkEndHour)` |
| `Doctor()` | конструктор | Значення за замовчуванням |
| `Doctor(firstName, lastName, speciality)` | конструктор | Ланцюжок |
| `Doctor(firstName, lastName, speciality, licenseNumber, phone)` | конструктор | Повний, виставляє 8–17 |
| `CanAcceptAt(int hour)` | `public bool` | `hour >= WorkStartHour && hour < WorkEndHour` |
| `ToString()` | override | Усі поля + статус доступності |

### Приклад виводу

```
[1] Олег Сидоренко | Кардіологія | LIC-001 | Тел: 0441234567 | 08:00–16:00 (8 год) | доступний зараз
[2] Наталія Мороз | Неврологія | LIC-002 | Тел: 0442345678 | 09:00–18:00 (9 год) | не в робочий час
[3] Андрій Власенко | Педіатрія | LIC-003 | Тел: 0443456789 | 08:00–17:00 (9 год) | доступний зараз
```

(Статус "доступний/не в робочий час" залежить від поточного часу запуску.)

### Підказки

1. `WorkStartHour` і `WorkEndHour` — прості `int`. Не потрібен спеціальний тип: `8` означає 08:00.
2. `WorkSchedule` — форматуйте через `ToString("D2")` для нулів: `WorkStartHour.ToString("D2") + ":00–" + WorkEndHour.ToString("D2") + ":00"`.
3. `IsAvailableNow` — отримайте поточну годину через `DateTime.Now.Hour`:
   ```csharp
   int currentHour = DateTime.Now.Hour;
   return currentHour >= WorkStartHour && currentHour < WorkEndHour;
   ```
4. У повному конструкторі виставляйте `WorkStartHour = 8; WorkEndHour = 17;` в тілі.
5. Після створення лікаря можна змінити графік через сеттери:
   ```csharp
   var doc = new Doctor("Олег", "Сидоренко", "Кардіологія", "LIC-001", "0441234567");
   doc.WorkStartHour = 8;
   doc.WorkEndHour   = 16;
   ```
6. `CanAcceptAt(int hour)` — корисний для перевірки перед записом: `if (!doctor.CanAcceptAt(10)) Console.WriteLine("Лікар не приймає о 10:00");`
7. У `ToString()` використовуйте `WorkSchedule` щоб не дублювати логіку форматування.

### Адаптація до вашого домену

| Клініка | Готель | Ресторан | Університет | Прокат авто | Бібліотека | Спортзал |
|---------|--------|----------|-------------|-------------|------------|---------|
| `Doctor` | `Staff` | `Waiter` | `Lecturer` | `Manager` | `Librarian` | `Trainer` |
| `Speciality` | `Department` | `Section` | `Subject` | `CarClass` | `Section` | `Specialty` |
| `WorkStartHour/WorkEndHour` | `ShiftStart/ShiftEnd` | `ShiftStart/ShiftEnd` | `LectureStartHour/LectureEndHour` | `WorkStartHour/WorkEndHour` | `ShiftStart/ShiftEnd` | `WorkStartHour/WorkEndHour` |
| `IsAvailableNow` | `IsOnShift` | `IsOnDuty` | `IsTeachingNow` | `IsAvailable` | `IsOnShift` | `IsAvailableNow` |
| `CanAcceptAt(hour)` | `CanCheckInAt(hour)` | `CanServeAt(hour)` | `CanTeachAt(hour)` | `CanHandleAt(hour)` | `CanIssueAt(hour)` | `CanTrainAt(hour)` |

### Коміт

```bash
git add src/Doctor.cs src/Program.cs
git commit -m "Lab03 Task2: add Doctor class with int work hours and availability check"
```

---

## Задача 3. PatientManager ⭐⭐

### Умова

Створіть клас `PatientManager` — колекцію пацієнтів з методами CRUD та статистикою. Для зберігання використовуйте **масив `Patient[]` фіксованого розміру** та окремий лічильник `_count`, як ви робили в Лабі 02.

Додайте у `Program.cs` консольне підменю «Пацієнти»: показати всіх, додати, знайти за ім'ям, видалити, статистика.

### Специфікація класу

| Член класу | Тип | Опис |
|------------|-----|------|
| `MaxPatients` | `private const int` | Ліміт (100) |
| `_patients` | `private Patient[]` | Масив `new Patient[MaxPatients]` |
| `_count` | `private int` | Поточна кількість пацієнтів |
| `Count` | `public int` | `_count` |
| `Add(Patient)` | `public void` | Додає, виводить підтвердження або повідомлення про ліміт |
| `FindById(int)` | `public Patient` | Лінійний пошук, повертає `null!` якщо не знайдено |
| `FindByName(string)` | `public Patient[]` | Пошук у `FirstName` або `LastName` (без урахування регістру) |
| `Remove(int id)` | `public bool` | Видаляє, зсуває елементи ліворуч |
| `DisplayAll()` | `public void` | Таблиця або «порожній список» |
| `DisplayStats()` | `public void` | Загальна кількість, середній вік, наймолодший, найстарший, кількість дорослих |

### Приклад виводу

```
Пацієнта [1] Іван Петренко додано.

=== Пацієнти (4 / 100) ===
[1] Іван Петренко | Вік: 41 (дорослий) | Кров: A+ | Тел: 0501234567
[2] Олена Коваль | Вік: 33 (дорослий) | Кров: B- | Тел: 0672345678
[3] Максим Бойко | Вік: 16 (дитина) | Кров: O+ | Тел: 0933456789
[4] Марія Ткач | Вік: 26 (дорослий) | Кров: Невідомо | Тел: 0000000000
────────────────────────────────────────────────────────────

=== Статистика пацієнтів ===
Всього:       4
Середній вік: 29.0 р.
Наймолодший:  Максим Бойко (16 р.)
Найстарший:   Іван Петренко (41 р.)
Дорослих:     3 з 4
============================
```

### Підказки

1. Поля масиву:
   ```csharp
   private const int MaxPatients = 100;
   private Patient[] _patients = new Patient[MaxPatients];
   private int _count = 0;
   ```
2. `Add` — перевірте ліміт, потім `_patients[_count] = patient; _count++;`
3. `FindById` — лінійний пошук: цикл від `0` до `_count`, порівняйте `_patients[i].Id == id`. Якщо не знайдено — поверніть `null!`.
4. `FindByName` — двопрохідний патерн з Лаби 02: один цикл рахує кількість збігів, потім виділяється масив точного розміру, другий цикл його заповнює. Порівняння регістронезалежне: `FirstName.ToLower().Contains(query.ToLower())` або аналогічно для `LastName`.
5. `Remove` — знайдіть індекс елемента (через цикл або `FindById`), зсуньте решту масиву ліворуч (`_patients[j] = _patients[j+1]`), встановіть останній слот у `null!`, зменшіть `_count`.
6. `DisplayStats` — один прохід по масиву: накопичуйте суму вікУ (`totalAge`), відстежуйте індекси мінімального та максимального елемента, рахуйте кількість дорослих. Середнє — `totalAge / _count`. Виводьте ім'я через `_patients[minIdx].FullName`.
7. У `Program.cs` ізолюйте підменю в окрему функцію `static void PatientsMenu(Clinic clinic)`.

### Адаптація до вашого домену

| Клініка | Готель | Ресторан | Університет | Прокат авто | Бібліотека | Спортзал |
|---------|--------|----------|-------------|-------------|------------|---------|
| `PatientManager` | `GuestManager` | `CustomerManager` | `StudentManager` | `ClientManager` | `ReaderManager` | `MemberManager` |
| `Patient[100]` | `Guest[200]` | `Customer[500]` | `Student[300]` | `Client[150]` | `Reader[200]` | `Member[250]` |
| `FindByName` | `FindByName` | `FindByName` | `FindByName` | `FindByName` | `FindByName` | `FindByName` |
| середній вік у статистиці | середня к-сть ночей | середня к-сть відвідин | середній бал | середній вік водія | середня к-сть книг | середній вік учасника |

### Коміт

```bash
git add src/PatientManager.cs src/Program.cs
git commit -m "Lab03 Task3: add PatientManager with array storage, CRUD and stats"
```

---

## Задача 4. DoctorManager ⭐⭐

### Умова

Створіть клас `DoctorManager` за тим самим шаблоном, що і `PatientManager`. Відмінність — метод `FindBySpeciality` та статистика зі **списком унікальних спеціальностей**.

Додайте у `Program.cs` підменю «Лікарі».

### Специфікація класу

| Член класу | Тип | Опис |
|------------|-----|------|
| `MaxDoctors` | `private const int` | Ліміт (50) |
| `_doctors` | `private Doctor[]` | Масив `new Doctor[MaxDoctors]` |
| `_count` | `private int` | Поточна кількість |
| `Count` | `public int` | `_count` |
| `Add(Doctor)` | `public void` | Додає або повідомляє про ліміт |
| `FindById(int)` | `public Doctor` | Лінійний пошук |
| `FindBySpeciality(string)` | `public Doctor[]` | Пошук за спеціальністю (без урахування регістру) |
| `GetAll()` | `public Doctor[]` | Копія перших `_count` елементів |
| `Remove(int id)` | `public bool` | Видаляє зі зсувом |
| `DisplayAll()` | `public void` | Таблиця з графіком і статусом |
| `DisplayStats()` | `public void` | Загальна кількість, кількість доступних зараз, список унікальних спеціальностей |

### Приклад виводу

```
=== Лікарі (3 / 50) ===
[1] Олег Сидоренко | Кардіологія | LIC-001 | Тел: 0441234567 | 08:00–16:00 (8 год) | доступний зараз
[2] Наталія Мороз | Неврологія | LIC-002 | Тел: 0442345678 | 09:00–18:00 (9 год) | не в робочий час
[3] Андрій Власенко | Педіатрія | LIC-003 | Тел: 0443456789 | 08:00–17:00 (9 год) | доступний зараз
────────────────────────────────────────────────────────────

=== Статистика лікарів ===
Всього:         3
Доступні зараз: 2
По спеціальностях:
  Кардіологія: 1
  Неврологія: 1
  Педіатрія: 1
==========================
```

### Підказки

1. `FindBySpeciality` — той самий двопрохідний патерн що і `PatientManager.FindByName`. Умова порівняння: `_doctors[i].Speciality.ToLower().Contains(q)`.
2. `GetAll()` — виділіть масив розміром `_count`, скопіюйте перші `_count` елементів з `_doctors`, поверніть.
3. `DisplayStats()` — для унікальних спеціальностей: зовнішній цикл по `i`, внутрішній (`j < i`) перевіряє чи спеціальність вже зустрічалась раніше. Якщо нова — третій цикл (`k`) рахує кількість лікарів з цією спеціальністю.
4. Кількість доступних: простий `for` з лічильником, `if (_doctors[i].IsAvailableNow) availableNow++;`
5. При введенні години роботи з консолі використовуйте `int.TryParse`:
   ```csharp
   Console.Write("Початок роботи (година, напр. 8): ");
   int.TryParse(Console.ReadLine(), out int workStart);
   ```

### Адаптація до вашого домену

| Клініка | Готель | Ресторан | Університет | Прокат авто | Бібліотека | Спортзал |
|---------|--------|----------|-------------|-------------|------------|---------|
| `DoctorManager` | `StaffManager` | `WaiterManager` | `LecturerManager` | `ManagerList` | `LibrarianManager` | `TrainerManager` |
| `FindBySpeciality` | `FindByDepartment` | `FindBySection` | `FindBySubject` | `FindByCarClass` | `FindBySection` | `FindBySpecialty` |
| Унікальні спеціальності | По відділах | По залах | По кафедрах | По класах авто | По відділах | По спеціалізаціях |
| «Доступні зараз» | «На зміні» | «На зміні» | «Читають зараз» | «На роботі» | «На зміні» | «Доступні зараз» |

### Коміт

```bash
git add src/DoctorManager.cs src/Program.cs
git commit -m "Lab03 Task4: add DoctorManager with speciality search and stats"
```

---

## Задача 5. Клас Appointment ⭐⭐⭐

### Умова

Створіть клас `Appointment` — запис пацієнта до лікаря. Головна особливість: **статус має кінцевий автомат**. З початкового стану `"Scheduled"` можна перейти лише до `"Cancelled"` або `"Completed"`, і лише один раз.

> Зверніть увагу: клас зберігає тільки `PatientId` та `DoctorId`, а не посилання на об'єкти. При виводі через `ToString()` ви побачите лише числа — це навмисне обмеження. У Задачі 6 це буде вирішено через `AppointmentManager`.

### Специфікація класу

| Член класу | Тип | Опис |
|------------|-----|------|
| `_nextId` | `private static int` | Лічильник |
| `Id` | `public int` (readonly) | Автопризначення |
| `PatientId` | `public int` (readonly) | ID пацієнта |
| `DoctorId` | `public int` (readonly) | ID лікаря |
| `ScheduledAt` | `public DateTime` | Дата та час прийому |
| `DurationMinutes` | `public int` | Тривалість у хвилинах |
| `Status` | `public string` (`private set`) | `"Scheduled"` → `"Cancelled"` або `"Completed"` |
| `Notes` | `public string` (`private set`) | Примітка (порожня за замовчуванням) |
| `EndsAt` | обчислювана `DateTime` | `ScheduledAt.AddMinutes(DurationMinutes)` |
| `IsUpcoming` | обчислювана `bool` | `ScheduledAt > DateTime.Now && Status == "Scheduled"` |
| `Appointment(patientId, doctorId, scheduledAt, durationMinutes)` | конструктор | Призначає Id, Status = `"Scheduled"`, durationMinutes за замовчуванням 30 |
| `Cancel(string reason)` | `public bool` | Якщо `Status == "Scheduled"` → `"Cancelled"`, інакше `false` |
| `Complete()` | `public bool` | Якщо `Status == "Scheduled"` → `"Completed"`, інакше `false` |
| `ToString()` | override | Рядкова форма з ID, а не іменами |

### Приклад виводу

```
[1] Пацієнт #1 → Лікар #1 | 09.05.2026 10:00–10:30 | Scheduled
[2] Пацієнт #2 → Лікар #2 | 09.05.2026 11:00–11:45 | Scheduled
[3] Пацієнт #3 → Лікар #3 | 10.05.2026 09:00–09:20 | Scheduled

// Після Cancel та Complete:
[1] Пацієнт #1 → Лікар #1 | 09.05.2026 10:00–10:30 | Cancelled | Пацієнт не зміг прийти
[2] Пацієнт #2 → Лікар #2 | 09.05.2026 11:00–11:45 | Completed
```

### Підказки

1. `Status` має `private set` — ззовні змінити неможливо, лише через `Cancel`/`Complete`:
   ```csharp
   public string Status { get; private set; }
   ```
2. `Cancel` перевіряє поточний стан:
   ```csharp
   public bool Cancel(string reason = "")
   {
       if (Status != "Scheduled") return false;
       Status = "Cancelled";
       if (reason.Length > 0) Notes = reason;
       return true;
   }
   ```
3. Метод повертає `bool` — це дозволяє в `Program.cs` перевірити успіх:
   ```csharp
   if (!appointment.Cancel("причина"))
       Console.WriteLine("Вже завершено або скасовано.");
   ```
4. `DurationMinutes = 30` — значення за замовчуванням у підписі конструктора (optional parameter).
5. У `ToString()` виводьте лише `#PatientId` і `#DoctorId` — це навмисно. Ви відчуєте незручність і вирішите її в Задачі 6.
6. `Notes` ініціалізуйте порожнім рядком `""`, а в `ToString()` додавайте тільки якщо не порожній:
   ```csharp
   if (Notes.Length > 0) result += " | " + Notes;
   ```

### Адаптація до вашого домену

| Клініка | Готель | Ресторан | Університет | Прокат авто | Бібліотека | Спортзал |
|---------|--------|----------|-------------|-------------|------------|---------|
| `Appointment` | `Booking` | `TableReservation` | `Enrollment` | `Rental` | `BookLoan` | `Session` |
| `PatientId` / `DoctorId` | `GuestId` / `RoomId` | `CustomerId` / `TableId` | `StudentId` / `CourseId` | `ClientId` / `CarId` | `ReaderId` / `BookId` | `MemberId` / `TrainerId` |
| `ScheduledAt` | `CheckIn` | `ReservedAt` | `StartDate` | `RentalStart` | `LoanDate` | `ScheduledAt` |
| `DurationMinutes` | `StayNights` | `Duration` | `CourseDays` | `RentalDays` | `LoanDays` | `DurationMinutes` |
| `Cancel` / `Complete` | `Cancel` / `CheckOut` | `Cancel` / `Seat` | `Withdraw` / `Complete` | `Cancel` / `Return` | `Cancel` / `Return` | `Cancel` / `Complete` |

### Коміт

```bash
git add src/Appointment.cs src/Program.cs
git commit -m "Lab03 Task5: add Appointment class with status state machine"
```

---

## Задача 6. AppointmentManager ⭐⭐⭐

### Умова

Створіть клас `AppointmentManager` — менеджер записів, який отримує посилання на `PatientManager` і `DoctorManager` через конструктор. Це дозволяє **валідувати** ID при записі та **відображати імена** замість числових ID.

Додайте у `Program.cs` підменю «Записи».

### Специфікація класу

| Член класу | Тип | Опис |
|------------|-----|------|
| `MaxAppointments` | `private const int` | Ліміт (500) |
| `_appointments` | `private Appointment[]` | Масив `new Appointment[MaxAppointments]` |
| `_count` | `private int` | Поточна кількість |
| `_patients` | `private PatientManager` | Збережена посилання |
| `_doctors` | `private DoctorManager` | Збережена посилання |
| `Count` | `public int` | `_count` |
| `AppointmentManager(PatientManager, DoctorManager)` | конструктор | Зберігає посилання |
| `Book(patientId, doctorId, scheduledAt, durationMinutes)` | `public bool` | Перевіряє наявність, створює запис |
| `Cancel(int id, string reason)` | `public bool` | Знаходить і делегує `appointment.Cancel()` |
| `Complete(int id)` | `public bool` | Знаходить і делегує `appointment.Complete()` |
| `GetByPatient(int patientId)` | `public Appointment[]` | Всі записи пацієнта |
| `GetByDoctor(int doctorId)` | `public Appointment[]` | Всі записи лікаря |
| `GetByDate(DateTime date)` | `public Appointment[]` | Всі записи на задану дату |
| `GetUpcoming()` | `public Appointment[]` | Всі майбутні записи |
| `DisplayAppointment(Appointment)` | `public void` | Виводить з іменами (пошук у менеджерах) |
| `DisplayList(Appointment[])` | `public void` | Виводить кожен або «не знайдено» |

### Приклад виводу

```
Запис [1] створено: Іван Петренко → Олег Сидоренко о 09.05.2026 10:00
Помилка: пацієнта з ID 99 не знайдено.

Майбутні записи:
[1] Іван Петренко → Олег Сидоренко | 09.05.2026 10:00–10:30 | Scheduled
[2] Олена Коваль → Наталія Мороз | 09.05.2026 11:00–11:45 | Scheduled
[3] Максим Бойко → Андрій Власенко | 10.05.2026 09:00–09:20 | Scheduled

Запис [1] скасовано.

Записи пацієнта #2:
[2] Олена Коваль → Наталія Мороз | 09.05.2026 11:00–11:45 | Scheduled
```

### Підказки

1. Конструктор зберігає посилання, не копіює:
   ```csharp
   public AppointmentManager(PatientManager patients, DoctorManager doctors)
   {
       _patients = patients;
       _doctors = doctors;
   }
   ```
2. `Book` — спочатку перевірте ID (через `FindById`, порівняйте з `null`), потім створюйте:
   ```csharp
   Patient patient = _patients.FindById(patientId);
   if (patient == null) { Console.WriteLine("..."); return false; }
   Doctor doctor = _doctors.FindById(doctorId);
   if (doctor == null) { Console.WriteLine("..."); return false; }
   _appointments[_count++] = new Appointment(patientId, doctorId, scheduledAt, durationMinutes);
   ```
3. `DisplayAppointment` — знайдіть імена через `FindById`, перевірте на `null` явно:
   ```csharp
   Patient patient = _patients.FindById(a.PatientId);
   string patientName;
   if (patient != null) patientName = patient.FullName;
   else patientName = "Пацієнт #" + a.PatientId;
   ```
4. `GetByDate` — порівнюйте тільки дату (без часу): `_appointments[i].ScheduledAt.Date == date.Date`
5. `GetByPatient`, `GetByDoctor`, `GetByDate`, `GetUpcoming` — всі за тим самим двопрохідним патерном.
6. Приватний допоміжний метод `FindById(int id)` всередині класу спрощує `Cancel` і `Complete`:
   ```csharp
   private Appointment FindById(int id)
   {
       for (int i = 0; i < _count; i++)
           if (_appointments[i].Id == id) return _appointments[i];
       return null!;
   }
   ```
7. У підменю «Записи» перед введенням ID пацієнта виводьте список пацієнтів, перед ID лікаря — список лікарів. Так видно, які ID доступні.

### Адаптація до вашого домену

| Клініка | Готель | Ресторан | Університет | Прокат авто | Бібліотека | Спортзал |
|---------|--------|----------|-------------|-------------|------------|---------|
| `AppointmentManager` | `BookingManager` | `ReservationManager` | `EnrollmentManager` | `RentalManager` | `LoanManager` | `SessionManager` |
| `Book(patientId, doctorId, ...)` | `Book(guestId, roomId, ...)` | `Reserve(custId, tableId, ...)` | `Enroll(studentId, courseId, ...)` | `Rent(clientId, carId, ...)` | `Lend(readerId, bookId, ...)` | `Book(memberId, trainerId, ...)` |
| `GetByPatient` | `GetByGuest` | `GetByCustomer` | `GetByStudent` | `GetByClient` | `GetByReader` | `GetByMember` |
| `GetByDoctor` | `GetByRoom` | `GetByTable` | `GetByCourse` | `GetByCar` | `GetByBook` | `GetByTrainer` |

### Коміт

```bash
git add src/AppointmentManager.cs src/Program.cs
git commit -m "Lab03 Task6: add AppointmentManager with constructor injection and name lookup"
```

---

## Задача 7. Клас Clinic ⭐⭐⭐

### Умова

Створіть клас `Clinic` — оркестратор, що об'єднує три менеджери під одним дахом. `Clinic` створює менеджери у конструкторі. Клас реалізує `DisplaySchedule` та `GenerateReport`.

Перепишіть `Program.cs` так, щоб вся робота йшла через єдиний об'єкт `clinic`.

### Специфікація класу

| Член класу | Тип | Опис |
|------------|-----|------|
| `Name` | `public string` | Назва клініки |
| `Patients` | `public PatientManager` | Менеджер пацієнтів |
| `Doctors` | `public DoctorManager` | Менеджер лікарів |
| `Appointments` | `public AppointmentManager` | Менеджер записів |
| `Clinic(string name)` | конструктор | Створює всі три менеджери; `AppointmentManager` отримує два інших |
| `DisplaySchedule(DateTime date)` | `public void` | Шапка + записи на цю дату |
| `GenerateReport()` | `public void` | Назва клініки, кількість пацієнтів/лікарів/майбутніх записів, навантаження кожного лікаря |

### Приклад виводу

```
=== Розклад на 09.05.2026 ===
[1] Іван Петренко → Олег Сидоренко | 09.05.2026 10:00–10:30 | Scheduled
[2] Олена Коваль → Наталія Мороз | 09.05.2026 11:00–11:45 | Scheduled

╔══════════════════════════════════════════════╗
║  Звіт — Медична Клініка
╠══════════════════════════════════════════════╣
║  Пацієнтів:          4
║  Лікарів:            3
║  Майбутніх записів:  3
╠══════════════════════════════════════════════╣
║  Навантаження лікарів (майбутні записи):
║    Олег Сидоренко (Кардіологія): 1 записів
║    Наталія Мороз (Неврологія): 1 записів
║    Андрій Власенко (Педіатрія): 1 записів
╚══════════════════════════════════════════════╝
```

### Підказки

1. У конструкторі порядок важливий — `AppointmentManager` потребує вже створених менеджерів:
   ```csharp
   Patients = new PatientManager();
   Doctors  = new DoctorManager();
   Appointments = new AppointmentManager(Patients, Doctors);
   ```
2. `DisplaySchedule` — отримайте масив через `Appointments.GetByDate(date)`, виведіть через `Appointments.DisplayList(...)`.
3. `GenerateReport` — для навантаження кожного лікаря: отримайте майбутні записи один раз, потім для кожного лікаря рахуйте в циклі:
   ```csharp
   Appointment[] upcoming = Appointments.GetUpcoming();
   Doctor[] allDoctors = Doctors.GetAll();
   for (int i = 0; i < allDoctors.Length; i++)
   {
       int doctorCount = 0;
       for (int j = 0; j < upcoming.Length; j++)
           if (upcoming[j].DoctorId == allDoctors[i].Id) doctorCount++;
       Console.WriteLine("  " + allDoctors[i].FullName + ": " + doctorCount + " записів");
   }
   ```
4. Символи рамок: `╔ ╗ ╚ ╝ ╠ ╣ ║ ═` — скопіюйте звідси.
5. У `Program.cs` тепер все через `clinic`. Підменю отримують `Clinic clinic` як параметр.

### Адаптація до вашого домену

| Клініка | Готель | Ресторан | Університет | Прокат авто | Бібліотека | Спортзал |
|---------|--------|----------|-------------|-------------|------------|---------|
| `Clinic` | `Hotel` | `Restaurant` | `University` | `CarRental` | `Library` | `GymCenter` |
| `Patients` | `Guests` | `Customers` | `Students` | `Clients` | `Readers` | `Members` |
| `Doctors` | `Rooms` або `Staff` | `Tables` або `Staff` | `Lecturers` | `Cars` | `Librarians` | `Trainers` |
| `Appointments` | `Bookings` | `Reservations` | `Enrollments` | `Rentals` | `Loans` | `Sessions` |
| `GenerateReport()` | Звіт по заповненості | Звіт по бронюванням | Звіт по успішності | Звіт по флоту | Звіт по фонду | Звіт по завантаженості |

### Коміт

```bash
git add src/Clinic.cs src/Program.cs
git commit -m "Lab03 Task7: add Clinic orchestrator with schedule and report"
```

---

## Задача 8. Зростаючий масив — передумови для List\<T\> ⭐⭐⭐⭐

### Умова

`PatientManager` має жорсткий ліміт — `const int MaxPatients = 100`. Що станеться, коли клініка виросте до 200 пацієнтів?

Просте збільшення константи нічого не вирішує: масив завжди займає пам'ять на `MaxPatients` елементів — навіть якщо в ньому лише 5. З іншого боку, занадто малий ліміт зупиняє роботу.

**Ваше завдання:** реалізувати `GrowablePatientManager` — клас з **автоматичним збільшенням масиву** коли він заповнений.

Алгоритм зростання: коли `_count == _capacity`, створіть **новий масив удвічі більший** та скопіюйте всі елементи.

### Специфікація класу

| Член класу | Тип | Опис |
|------------|-----|------|
| `_patients` | `private Patient[]` | Починається з розміру 4 |
| `_count` | `private int` | Поточна кількість |
| `Count` | `public int` | `_count` |
| `Capacity` | `public int` | `_patients.Length` — поточна ємність масиву |
| `Add(Patient)` | `public void` | Якщо повний — зростає, потім додає |
| `FindById(int)` | `public Patient` | Лінійний пошук |
| `Remove(int id)` | `public bool` | Видаляє зі зсувом |
| `DisplayAll()` | `public void` | Виводить список + поточну ємність |

### Що очікується у виводі

```
=== Тест GrowablePatientManager ===
Додаємо пацієнтів одного за одним...
  Додано [1]. Розмір: 1 / 4
  Додано [2]. Розмір: 2 / 4
  Додано [3]. Розмір: 3 / 4
  Додано [4]. Розмір: 4 / 4
  Масив заповнений! Розширення: 4 → 8
  Додано [5]. Розмір: 5 / 8
  Додано [6]. Розмір: 6 / 8
  Додано [7]. Розмір: 7 / 8
  Додано [8]. Розмір: 8 / 8
  Масив заповнений! Розширення: 8 → 16
  Додано [9]. Розмір: 9 / 16
  ...
  Додано [20]. Розмір: 20 / 32

Тест пошуку:
  FindById(10) → Тест Пацієнт10
  FindById(99) → не знайдено

Порівняння:
  PatientManager:         100 місць (фіксовано)
  GrowablePatientManager:  32 місця (зросте при потребі)
```

### Підказки

1. Приватний метод `Grow()`: обчисліть нову ємність (`_patients.Length * 2`), виділіть новий масив, скопіюйте перші `_count` елементів зі старого циклом, присвойте `_patients = newArray`. Виведіть рядок із старою та новою ємністю.
2. `Add`: якщо `_count == _patients.Length` — викличте `Grow()`. Потім присвойте елемент і збільшіть `_count`.
3. Починайте з малого початкового розміру (`new Patient[4]`) — так ви побачите кілька розширень навіть на малих даних.
4. `_patients = newArray;` — після цього старий масив більше не потрібен. C# автоматично звільнить пам'ять.
5. Додайте `Capacity` властивість: `public int Capacity => _patients.Length;` — для виводу поточного розміру.
6. Протестуйте: додайте 20 пацієнтів у циклі, після кожного виводьте `Count` та `Capacity`. Ви побачите стрибки: 4→8→16→32.
7. Подумайте: скільки разів копіюється кожен елемент загалом, якщо додати 16 пацієнтів (починаючи з ємності 4)? Намалюйте на папері.

> **Підказка наперед:** саме цей алгоритм — "подвоєння при заповненні" — використовується всередині `List<T>` у C#. У наступній лабі ви перейдете на `List<T>` і більше не писатимете цей код вручну.

### Адаптація до вашого домену

| Клініка | Готель | Ресторан | Університет | Прокат авто | Бібліотека | Спортзал |
|---------|--------|----------|-------------|-------------|------------|---------|
| `GrowablePatientManager` | `GrowableGuestManager` | `GrowableCustomerManager` | `GrowableStudentManager` | `GrowableClientManager` | `GrowableReaderManager` | `GrowableMemberManager` |
| Початковий розмір 4 | Початковий розмір 4 | Початковий розмір 4 | Початковий розмір 4 | Початковий розмір 4 | Початковий розмір 4 | Початковий розмір 4 |
| Пацієнтів / ємність | Гостей / ємність | Відвідувачів / ємність | Студентів / ємність | Клієнтів / ємність | Читачів / ємність | Учасників / ємність |

### Коміт

```bash
git add src/GrowablePatientManager.cs src/Program.cs
git commit -m "Lab03 Task8: implement growable array manager to understand List<T> internals"
```

---

## Перевірка перед здачею

```bash
cd src
dotnet build
dotnet run
```

Переконайтесь, що:

- [ ] Проєкт компілюється без помилок і попереджень
- [ ] Кожен пацієнт має унікальний Id (1, 2, 3, ...)
- [ ] Вік розраховується правильно (з урахуванням дня народження)
- [ ] `Cancel` повертає `false`, якщо запис вже скасовано або завершено
- [ ] `Book` виводить помилку при неіснуючому ID
- [ ] `FindByName` знаходить за частиною імені (без урахування регістру)
- [ ] `DisplayStats` виводить коректні дані (середнє, мін, макс)
- [ ] Підменю всіх трьох розділів (Пацієнти, Лікарі, Записи) працюють
- [ ] `GenerateReport()` виводить правильну кількість майбутніх записів
- [ ] `GrowablePatientManager` розширюється при заповненні та зберігає всі елементи

---

## Питання для самоперевірки

1. Чому `_nextId` — `static`, а `Id` — не `static`? Що трапиться, якщо `_nextId` буде звичайним полем?
2. Чому конструктор `Patient(firstName, lastName)` викликає `: this(...)`, а не просто присвоює поля?
3. У `Appointment` поле `Status` має `private set`. Як би виглядав код без цього обмеження? Що могло б піти не так?
4. Чому `AppointmentManager` отримує `PatientManager` і `DoctorManager` у конструкторі, а не створює їх сам?
5. Скільки разів кожен елемент копіюється якщо послідовно додати 16 пацієнтів у `GrowablePatientManager`, що починається з ємності 4?

---

## Статус гілки

Ця гілка **зливається в `main`**. Після завершення всіх завдань:

```bash
git checkout main
git merge feature/catalog
```

> Наступна лаба: `git checkout -b feature/abstraction` — абстрактні класи та enum.
