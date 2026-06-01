# Лаба 04 — Члени класу

## Мета

Збагатити систему новими видами членів класу: іменованими константами (`enum`), структурами-значеннями (`struct`), статичними утилітними класами та індексаторами — і навчитися перевантажувати методи.

## Контекст

Після Лаби 03 система працює, але має «брудний» код: статуси зберігаються як рядки `"Scheduled"`, групи крові як `"A+"`, спеціальності як `"Кардіологія"`. Будь-яка опечатка — і логіка зламана, а компілятор нічого не скаже.

Ця лаба вирішує це системно: замінюємо magic strings на типобезпечні конструкції та розширюємо API менеджерів колекцій.

---

## Гілка

```bash
git checkout main
git checkout -b feature/class-members
```

> Гілка **зливається в `main`** після завершення всіх завдань.

---

## Задача 1. Enum — замість магічних рядків ⭐⭐

### Умова

У поточному коді статус запису зберігається як `string Status = "Scheduled"`. Якщо хтось напише `"Shedüled"` — ніхто не помітить до виконання.

Вирішіть це через перерахування (`enum`): компілятор перевіряє допустимі значення на етапі збірки.

**Що реалізувати:**

1. `enum AppointmentStatus` — три стани запису.
2. `enum BloodType` — дев'ять значень (у т.ч. `Unknown`).
3. `enum Speciality` — вісім спеціальностей лікаря.
4. Замінити `string Status` в `Appointment` на `AppointmentStatus`.
5. Замінити `string BloodType` в `Patient` на `BloodType`.
6. Замінити `string Speciality` в `Doctor` на `Speciality`.

### Специфікація

| Enum | Значення |
|------|---------|
| `AppointmentStatus` | `Scheduled`, `Cancelled`, `Completed` |
| `BloodType` | `Unknown`, `APositive`, `ANegative`, `BPositive`, `BNegative`, `ABPositive`, `ABNegative`, `OPositive`, `ONegative` |
| `Speciality` | `General`, `Cardiology`, `Neurology`, `Pediatrics`, `Surgery`, `Orthopedics`, `Dermatology`, `Emergency` |

### Приклад

```csharp
// До (рядки — ніщо не захищає від помилки)
Status = "Cancelled";
if (Status == "Schdeuled") ...  // компілятор мовчить!

// Після (enum — помилка компіляції при опечатці)
Status = AppointmentStatus.Cancelled;
if (Status == AppointmentStatus.Scheduled) ...
```

### Підказки

1. Кожен `enum` — окремий файл у просторі імен `ClinicApp`:
   ```csharp
   namespace ClinicApp;
   public enum AppointmentStatus { Scheduled, Cancelled, Completed }
   ```
2. Перший елемент `enum` отримує числове значення `0`. `Unknown`, `General` — природні значення за замовчуванням.
3. У конструкторах замініть рядки на enum значення:
   ```csharp
   Status = AppointmentStatus.Scheduled;
   BloodType = BloodType.Unknown;
   ```
4. У `Cancel()` і `Complete()`:
   ```csharp
   if (Status != AppointmentStatus.Scheduled) return false;
   Status = AppointmentStatus.Cancelled;
   ```
5. `enum.ToString()` дає назву значення (`"Scheduled"`). Для відображення у зручному форматі ("Scheduled" → "A+") знадобиться Задача 3.

### Адаптація до вашого домену

| Клініка | Готель | Ресторан | Університет | Прокат авто | Бібліотека | Спортзал |
|---------|--------|----------|-------------|-------------|------------|---------|
| `AppointmentStatus` | `BookingStatus` | `ReservationStatus` | `EnrollmentStatus` | `RentalStatus` | `LoanStatus` | `SessionStatus` |
| `BloodType` | `RoomType` | `DishCategory` | `Faculty` | `CarClass` | `BookGenre` | `FitnessLevel` |
| `Speciality` | `Department` | `CuisineType` | `Subject` | `CarBrand` | `LibrarySection` | `TrainingType` |

### Коміт

```bash
git add src/AppointmentStatus.cs src/BloodType.cs src/Speciality.cs
git add src/Appointment.cs src/Patient.cs src/Doctor.cs
git commit -m "Lab04 Task1: add enums for status, blood type and speciality"
```

---

## Задача 2. Struct WorkSchedule — value type ⭐⭐⭐

### Умова

У `Doctor` є два окремих поля: `int WorkStartHour` і `int WorkEndHour`. Вони завжди разом — і разом мають зміст. Але нічого не заважає встановити `Start = 20, End = 6` — безглузде розкладання.

`struct` дозволяє об'єднати пов'язані дані у **нероздільний value type**: значення копіюється при присвоєнні, не передається за посиланням.

**Що реалізувати:**

1. `struct WorkSchedule` з двома `get`-only властивостями `Start` і `End`.
2. Конструктор `WorkSchedule(int start, int end)`.
3. Обчислювані властивості: `HoursPerDay`, `Display` (рядок `"08:00–17:00"`), `IsNow` (чи поточна година в межах розкладу).
4. Метод `Contains(int hour)`.
5. `override ToString()`.
6. Замінити `WorkStartHour`/`WorkEndHour` у `Doctor` одним полем `Schedule` типу `WorkSchedule`.

### Специфікація struct

| Член | Тип | Опис |
|------|-----|------|
| `Start` | `public int` (get only) | Година початку |
| `End` | `public int` (get only) | Година кінця |
| `HoursPerDay` | обчислювана `int` | `End - Start` |
| `Display` | обчислювана `string` | `"08:00–17:00"` |
| `IsNow` | обчислювана `bool` | `Contains(DateTime.Now.Hour)` |
| `WorkSchedule(int, int)` | конструктор | Ініціалізує Start та End |
| `Contains(int hour)` | `public bool` | `hour >= Start && hour < End` |
| `ToString()` | override | `Display + " (" + HoursPerDay + " год)"` |

### Приклад

```csharp
WorkSchedule morning = new WorkSchedule(8, 16);
WorkSchedule evening = new WorkSchedule(14, 22);

Console.WriteLine(morning);        // 08:00–16:00 (8 год)
Console.WriteLine(morning.IsNow);  // true/false залежно від годин

// Value type — копіюється при присвоєнні
WorkSchedule copy = morning;
// copy і morning — незалежні значення
```

### Підказки

1. `struct` оголошується як `class`, але ключове слово `struct`:
   ```csharp
   public struct WorkSchedule
   {
       public int Start { get; }
       public int End { get; }
       public WorkSchedule(int start, int end) { Start = start; End = end; }
   }
   ```
2. `get`-only властивості (`{ get; }`) можна ініціалізувати тільки в конструкторі — це забезпечує незмінність після створення.
3. `Display` — форматування через `ToString("D2")`:
   ```csharp
   public string Display => Start.ToString("D2") + ":00–" + End.ToString("D2") + ":00";
   ```
4. У `Doctor` замініть два поля одним:
   ```csharp
   public WorkSchedule Schedule { get; set; }
   ```
   У конструкторі: `Schedule = new WorkSchedule(8, 17);`
5. Після зміни у `Program.cs`:
   ```csharp
   d1.Schedule = new WorkSchedule(8, 16);   // замість WorkStartHour/WorkEndHour
   ```
6. `IsAvailableNow` у Doctor спрощується до:
   ```csharp
   public bool IsAvailableNow => Schedule.IsNow;
   ```
7. **Різниця struct vs class:** присвоєння `WorkSchedule a = b` копіює значення, а `Patient a = b` копіює посилання. Перевірте: змінивши `a.Start` після `WorkSchedule a = b`, `b.Start` не зміниться (але для struct з readonly властивостями взагалі не можна змінити після створення).

### Адаптація до вашого домену

| Клініка | Готель | Ресторан | Університет | Прокат авто | Бібліотека | Спортзал |
|---------|--------|----------|-------------|-------------|------------|---------|
| `WorkSchedule` (Start, End) | `BookingPeriod` (CheckIn, CheckOut) | `ServiceHours` (Open, Close) | `LectureSlot` (StartHour, EndHour) | `RentalPeriod` (PickupHour, ReturnHour) | `ShiftSchedule` (Start, End) | `TrainingSlot` (Start, End) |

### Коміт

```bash
git add src/WorkSchedule.cs src/Doctor.cs src/Program.cs
git commit -m "Lab04 Task2: add WorkSchedule struct, replace int hours in Doctor"
```

---

## Задача 3. Static клас та індексатор ⭐⭐⭐

### Умова

**Проблема 1:** `BloodType.APositive.ToString()` повертає `"APositive"`, але нам потрібно `"A+"`. Логіка форматування потрібна в багатьох місцях — куди її помістити, якщо вона не належить жодному конкретному об'єкту?

Відповідь: `static class` — клас без екземплярів, тільки статичні методи.

**Проблема 2:** Отримати третього пацієнта зараз: `clinic.Patients.FindById(3)`. Але якщо ми вже знаємо індекс — `clinic.Patients[2]` було б природніше.

Відповідь: **індексатор** `this[int index]`.

**Що реалізувати:**

1. `static class ClinicFormatter` з методами:
   - `FormatBloodType(BloodType bt)` → `"A+"`, `"B-"` тощо
   - `FormatSpeciality(Speciality s)` → `"Кардіологія"` тощо
   - `FormatAge(int age)` → `"41 рік"`, `"33 роки"`, `"16 років"` (правила відмінювання)
   - `FormatPhone(string phone)` → `"(050) 123-4567"`
2. Оновити `Patient.ToString()` і `Doctor.ToString()` щоб використовували форматер.
3. Додати індексатор `this[int index]` до `PatientManager`, `DoctorManager`, `AppointmentManager`.

### Приклад

```csharp
// static клас — викликається без екземпляру
Console.WriteLine(ClinicFormatter.FormatBloodType(BloodType.APositive));  // A+
Console.WriteLine(ClinicFormatter.FormatAge(1));   // 1 рік
Console.WriteLine(ClinicFormatter.FormatAge(3));   // 3 роки
Console.WriteLine(ClinicFormatter.FormatAge(11));  // 11 років

// індексатор
Patient first = clinic.Patients[0];
Doctor second = clinic.Doctors[1];
```

### Підказки

1. `static class` — не можна створити `new ClinicFormatter()`. Всі методи `public static`:
   ```csharp
   public static class ClinicFormatter
   {
       public static string FormatBloodType(BloodType bt) => bt switch
       {
           BloodType.APositive => "A+",
           BloodType.ANegative => "A-",
           // ...
           _ => "Невідомо"
       };
   }
   ```
2. Правила відмінювання для `FormatAge`:
   - 11–19 → завжди "років" (виняток для підлітків)
   - закінчення 1 → "рік" (21 рік, 31 рік, але не 11)
   - закінчення 2,3,4 → "роки" (22 роки, 33 роки)
   - інше → "років"
   ```csharp
   if (age % 100 >= 11 && age % 100 <= 19) return age + " років";
   switch (age % 10)
   {
       case 1: return age + " рік";
       case 2: case 3: case 4: return age + " роки";
       default: return age + " років";
   }
   ```
3. Індексатор синтаксично схожий на властивість, але з параметром `this[...]`:
   ```csharp
   public Patient this[int index]
   {
       get
       {
           if (index < 0 || index >= _count) return null!;
           return _patients[index];
       }
   }
   ```
4. Індексатор — лише `get` (readonly). `PatientManager[0] = new Patient(...)` не потрібно.
5. `FormatPhone`: перевірте довжину 10 символів, усі цифри, потім форматуйте:
   ```csharp
   return "(" + phone.Substring(0, 3) + ") " + phone.Substring(3, 3) + "-" + phone.Substring(6);
   ```

### Адаптація до вашого домену

| Клініка | Готель | Ресторан | Університет | Прокат авто | Бібліотека | Спортзал |
|---------|--------|----------|-------------|-------------|------------|---------|
| `ClinicFormatter.FormatBloodType` | `HotelFormatter.FormatRoomType` | `RestaurantFormatter.FormatCategory` | `UnivFormatter.FormatFaculty` | `RentalFormatter.FormatCarClass` | `LibraryFormatter.FormatGenre` | `GymFormatter.FormatTrainingType` |
| `PatientManager[i]` | `GuestManager[i]` | `CustomerManager[i]` | `StudentManager[i]` | `ClientManager[i]` | `ReaderManager[i]` | `MemberManager[i]` |

### Коміт

```bash
git add src/ClinicFormatter.cs
git add src/Patient.cs src/Doctor.cs
git add src/PatientManager.cs src/DoctorManager.cs src/AppointmentManager.cs
git commit -m "Lab04 Task3: add ClinicFormatter static class and indexers on managers"
```

---

## Задача 4. Перевантаження методів та параметр out ⭐⭐⭐⭐

### Умова

**Перевантаження методів** — декілька методів з однаковою назвою, але різними параметрами. Компілятор обирає потрібний за типом аргументів.

**Параметр `out`** — дозволяє методу повертати додаткове значення через аргумент. Класичний патерн — `TryXxx`: повертає `bool` (успіх/невдача) і через `out` — знайдений об'єкт.

**Що реалізувати:**

1. **Перевантаження в `DoctorManager`:**
   - `FindBySpeciality(string query)` — існуючий (пошук за рядком, часткове співпадіння)
   - `FindBySpeciality(Speciality speciality)` — **новий** (точне співпадіння за enum)
2. **Перевантаження в `AppointmentManager`:**
   - `GetByDate(DateTime date)` — існуючий
   - `GetByDate(int year, int month, int day)` — **новий** (три числа замість `DateTime`)
3. **TryFindById у `PatientManager`:**
   - `bool TryFindById(int id, out Patient patient)`
4. **TryFindById у `DoctorManager`:**
   - `bool TryFindById(int id, out Doctor doctor)`
5. Додати `FindByBloodType(BloodType bloodType)` до `PatientManager`.
6. Продемонструвати `?.` та `??` у `Program.cs`.

### Приклад

```csharp
// Перевантаження — компілятор обирає за типом аргументу
Doctor[] cardiologists = clinic.Doctors.FindBySpeciality(Speciality.Cardiology);  // enum версія
Doctor[] found = clinic.Doctors.FindBySpeciality("кардіо");                       // string версія

// GetByDate overload
Appointment[] today = clinic.Appointments.GetByDate(2026, 5, 10);  // зручніше, ніж new DateTime(...)

// TryFindById з out параметром
if (clinic.Patients.TryFindById(3, out Patient patient))
    Console.WriteLine("Знайдено: " + patient.FullName);
else
    Console.WriteLine("Пацієнта не знайдено.");

// ?. та ??
string name = clinic.Patients.FindById(99)?.FullName ?? "не знайдено";
Console.WriteLine(name);  // не знайдено
```

### Підказки

1. Перевантаження — просто два методи з однаковою назвою:
   ```csharp
   public Doctor[] FindBySpeciality(string query) { /* рядковий пошук */ }
   public Doctor[] FindBySpeciality(Speciality speciality) { /* точне порівняння enum */ }
   ```
   C# обере правильний варіант залежно від типу аргументу при виклику.
2. `GetByDate` з трьома числами — делегує до основного:
   ```csharp
   public Appointment[] GetByDate(int year, int month, int day)
   {
       return GetByDate(new DateTime(year, month, day));
   }
   ```
3. `TryFindById` — класичний TryXxx патерн:
   ```csharp
   public bool TryFindById(int id, out Patient patient)
   {
       patient = FindById(id);
       return patient != null;
   }
   ```
   Виклик: `if (manager.TryFindById(5, out Patient p)) { ... }`
4. `FindByBloodType` — двопрохідний патерн з Lab03, але умова — `== bloodType` замість `.Contains()`:
   ```csharp
   public Patient[] FindByBloodType(BloodType bloodType) { ... }
   ```
5. `?.` — null-conditional: `obj?.Property` повертає `null` якщо `obj == null`, інакше `obj.Property`.
6. `??` — null-coalescing: `expr ?? defaultValue` повертає `defaultValue` якщо `expr == null`.
7. Комбінація: `clinic.Patients.FindById(99)?.FullName ?? "невідомий"` — безпечне звернення до властивості з fallback значенням.

### Адаптація до вашого домену

| Клініка | Готель | Ресторан | Університет | Прокат авто | Бібліотека | Спортзал |
|---------|--------|----------|-------------|-------------|------------|---------|
| `FindBySpeciality(Speciality)` | `FindByDepartment(Department)` | `FindByCategory(DishCategory)` | `FindByFaculty(Faculty)` | `FindByClass(CarClass)` | `FindBySection(LibrarySection)` | `FindByType(TrainingType)` |
| `TryFindById(id, out Patient)` | `TryFindById(id, out Guest)` | `TryFindById(id, out Customer)` | `TryFindById(id, out Student)` | `TryFindById(id, out Client)` | `TryFindById(id, out Reader)` | `TryFindById(id, out Member)` |

### Коміт

```bash
git add src/PatientManager.cs src/DoctorManager.cs src/AppointmentManager.cs src/Program.cs
git commit -m "Lab04 Task4: add method overloads, TryFindById with out, FindByBloodType"
```

---

## Перевірка перед здачею

```bash
cd src
dotnet build
dotnet run
```

Переконайтесь, що:

- [ ] Проєкт компілюється без помилок
- [ ] `AppointmentStatus.Scheduled` — у коді немає рядка `"Scheduled"`
- [ ] `BloodType.APositive` відображається як `"A+"` через `ClinicFormatter`
- [ ] `Doctor.Schedule.ToString()` повертає `"08:00–16:00 (8 год)"`
- [ ] `clinic.Patients[0]` повертає першого пацієнта
- [ ] `clinic.Doctors.FindBySpeciality(Speciality.Cardiology)` повертає кардіологів
- [ ] `TryFindById(99, out p)` повертає `false` і не кидає виняток
- [ ] `FindById(99)?.FullName ?? "не знайдено"` працює без NullReferenceException

---

## Питання для самоперевірки

1. Чому `enum` безпечніший за `string` для статусів? Що конкретно перевіряє компілятор?
2. Яка різниця між `class` і `struct`? Що станеться при `WorkSchedule a = b; a.Start = 10`?
3. Навіщо `static class`? Чому не можна просто написати звичайний клас і не створювати екземпляри?
4. Що таке індексатор? Як він відрізняється від звичайної властивості?
5. Чому `TryFindById` повертає `bool` і має `out`, а не просто повертає `null` при невдачі?
6. Яка різниця між перевантаженням методів та параметрами за замовчуванням?

---

## Злиття

```bash
git checkout main
git merge --no-ff feature/class-members -m "Merge feature/class-members: Lab04 Class Members"
```

> Наступна лаба: `git checkout -b feature/encapsulation` — інкапсуляція, приватні поля, валідація.
