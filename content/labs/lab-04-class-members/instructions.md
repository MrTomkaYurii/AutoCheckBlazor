# Лаба 04 — Члени класу

## Мета

Збагатити систему новими видами членів класу: іменованими константами (`enum`), структурами-значеннями (`struct`), статичними утилітними класами та індексаторами — і навчитися перевантажувати методи.

## Контекст

Після Лаби 03 система працює, але код «брудний»: статуси — рядки `"Scheduled"`,
групи крові — `"A+"`, спеціальності — `"Кардіологія"`. Одна опечатка — і логіка
зламана, а компілятор мовчить.

Ця лаба лікує це системно: замінюємо magic strings на **типобезпечні
конструкції** (`enum`, `struct`), виносимо форматування в **статичний клас** і
розширюємо API менеджерів **індексаторами** та **перевантаженнями**.

Працюєте в тому самому проєкті `ClinicApp/`. Гілка зливається в `main`.

### Що нового дозволено (і тільки воно)

- `enum` — іменовані константи;
- `struct` — власний value-тип;
- `static class` — клас без екземплярів;
- індексатор `this[int index]`;
- перевантаження методів (кілька методів з одним іменем, різні параметри);
- `out`-параметр і патерн `TryXxx`;
- `?.` і `??` — **тепер можна** (раніше були заборонені).

Досі заборонено: `List<T>` / `Dictionary` (Лаба 09), LINQ (Лаба 14),
`interface` / `abstract` (Лаби 06–07).

---

## Крок 1. Гілка

> **Робочий процес** (повністю — [Git Воркшоп](https://tomka.space/git-workshop/)):
> лаба = гілка `Lab-XX` від `main`, коміт на кожне завдання (`LabXX TaskYY`), у
> кінці — злиття в `main` (Лаби 03+; 01–02 не зливались).

Проєкт `ClinicApp/` уже існує з Лаби 03. Тут лише нова гілка від `main`:

```bash
git checkout main
git checkout -b Lab-04
```

Коміт — на кожне завдання (`Lab04 TaskNN`).

### Ваш домен

За замовчуванням — домен «клініка». Для власного домену дивіться таблицю
**«Адаптація до вашого домену»** в кінці кожного завдання.

### Як користуватися підказками

Підказки — **напрям думки, не готовий код**. «Що реалізувати» і «Специфікація»
кажуть *що*; підказки — *як міркувати*; блок **📖 Документація** — де прочитати
синтаксис. Спершу документація і власна спроба.

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

1. **Кожен `enum` — окремий файл** у просторі імен `ClinicApp`. Оголошення
   `enum` — коротке; синтаксис подивіться в документації.
2. **Порядок значень має сенс.** Перший елемент отримує число `0` — тому на
   першу позицію ставте «природне за замовчуванням»: `Unknown` для групи крові,
   `General` для спеціальності. Так поле без явної ініціалізації матиме розумне
   значення.
3. **Заміна типу поля — механічна, але наскрізна.** У класах `Appointment`,
   `Patient`, `Doctor` змініть тип властивості з `string` на відповідний `enum`,
   а потім пройдіться по конструкторах і методах (`Cancel`, `Complete`),
   замінюючи рядкові літерали на значення enum. Компілятор сам покаже всі місця,
   де лишився `string`.
4. **Порівняння enum** — звичайне `==` / `!=`, як з числами.
5. **`enum.ToString()`** поверне назву значення як у коді (`"APositive"`), а не
   `"A+"`. Гарне відображення зробите в Задачі 3 (статичний форматер).

📖 Документація:
- [Тип `enum`](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/enum)
- [Типи перерахувань (посібник)](https://learn.microsoft.com/dotnet/csharp/programming-guide/enumeration-types)

### Адаптація до вашого домену

| Клініка | Готель | Ресторан | Університет | Прокат авто | Бібліотека | Спортзал |
|---------|--------|----------|-------------|-------------|------------|---------|
| `AppointmentStatus` | `BookingStatus` | `ReservationStatus` | `EnrollmentStatus` | `RentalStatus` | `LoanStatus` | `SessionStatus` |
| `BloodType` | `RoomType` | `DishCategory` | `Faculty` | `CarClass` | `BookGenre` | `FitnessLevel` |
| `Speciality` | `Department` | `CuisineType` | `Subject` | `CarBrand` | `LibrarySection` | `TrainingType` |

### Коміт

```bash
git add ClinicApp/AppointmentStatus.cs ClinicApp/BloodType.cs ClinicApp/Speciality.cs
git add ClinicApp/Appointment.cs ClinicApp/Patient.cs ClinicApp/Doctor.cs
git commit -m "Lab04 Task01"
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

1. **`struct` описується майже як `class`** — тільки ключове слово інше.
   Синтаксис і повний перелік відмінностей — у документації.
2. **`Start` і `End` — незмінні після створення.** Це властивості лише з `get`,
   які присвоюються один раз у конструкторі. Далі `schedule.Start = 5` не
   скомпілюється — і це добре: розклад не можна «покалічити».
3. **`Display`** будує рядок `"08:00–17:00"` з двох годин, доповнених нулем
   (формат `D2`) — так само, як `WorkSchedule`-властивість у Лабі 03, тільки
   тепер логіка живе в одному місці.
4. **`Contains` та `IsNow`.** `Contains(hour)` перевіряє діапазон `[Start, End)`;
   `IsNow` — це просто `Contains` для поточної години. Не дублюйте умову.
5. **У `Doctor` два поля стають одним** полем типу `WorkSchedule` (з `get`/`set`).
   `IsAvailableNow` тоді зводиться до звернення до `Schedule.IsNow`, а
   `CanAcceptAt` — до `Schedule.Contains(...)`.
6. **Ключова відмінність `struct` від `class` — семантика копіювання.**
   Присвоєння однієї `struct`-змінної іншій копіює *значення* (два незалежні
   розклади), тоді як присвоєння об'єкта класу копіює *посилання* (обидві
   змінні — на один об'єкт). Перевірте це експериментом у `Program.cs` і
   прочитайте розділ «Value types vs reference types».
7. **Валідацію `Start < End` поки не додаємо** — це Лаба 05 (Інкапсуляція).

📖 Документація:
- [Типи `struct`](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/struct)
- [Типи-значення й типи-посилання](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/value-types)
- [Властивості лише для читання](https://learn.microsoft.com/dotnet/csharp/programming-guide/classes-and-structs/properties#read-only)

### Адаптація до вашого домену

| Клініка | Готель | Ресторан | Університет | Прокат авто | Бібліотека | Спортзал |
|---------|--------|----------|-------------|-------------|------------|---------|
| `WorkSchedule` (Start, End) | `BookingPeriod` (CheckIn, CheckOut) | `ServiceHours` (Open, Close) | `LectureSlot` (StartHour, EndHour) | `RentalPeriod` (PickupHour, ReturnHour) | `ShiftSchedule` (Start, End) | `TrainingSlot` (Start, End) |

### Коміт

```bash
git add ClinicApp/WorkSchedule.cs ClinicApp/Doctor.cs ClinicApp/Program.cs
git commit -m "Lab04 Task02"
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

1. **`static class` не має екземплярів** — `new ClinicFormatter()` заборонено
   компілятором. Усередині лише `public static` методи. Такий клас — просто
   «набір функцій під спільним іменем».
2. **`FormatBloodType` / `FormatSpeciality`** — це зіставлення «значення enum →
   рядок». Найчистіше — `switch` як вираз із гілкою `_` для решти. Кожній назві
   зі специфікації Задачі 1 поставте у відповідність потрібний рядок.
3. **`FormatAge` — правила українського відмінювання:**
   - остача від ділення на 100 у діапазоні 11–19 → завжди «років»;
   - інакше дивимось останню цифру: `1` → «рік», `2`–`4` → «роки», решта → «років».
   Перевірте на 1, 3, 11, 21, 111.
4. **`FormatPhone`** — спершу переконайтесь, що рядок має рівно 10 символів і всі
   вони цифри; якщо ні — поверніть як є. Якщо так — зберіть
   `"(050) 123-4567"` з підрядків. Метод виділення підрядка — у документації
   `String`.
5. **Індексатор — це властивість із параметром.** Синтаксис — `this[int index]`
   з блоком `get`. Усередині перевірте межі (`index` у `[0, _count)`); якщо поза
   межами — поверніть `null` (тому тип — `Patient?`). Тільки `get`, без `set`.
6. **`Patient.ToString()` / `Doctor.ToString()`** тепер викликають `ClinicFormatter`
   замість того, щоб форматувати самотужки.

📖 Документація:
- [`static`-класи](https://learn.microsoft.com/dotnet/csharp/programming-guide/classes-and-structs/static-classes-and-static-class-members)
- [Вираз `switch`](https://learn.microsoft.com/dotnet/csharp/language-reference/operators/switch-expression)
- [Індексатори](https://learn.microsoft.com/dotnet/csharp/programming-guide/indexers/)
- [`String.Substring`](https://learn.microsoft.com/dotnet/api/system.string.substring) / [`Char.IsDigit`](https://learn.microsoft.com/dotnet/api/system.char.isdigit)

### Адаптація до вашого домену

| Клініка | Готель | Ресторан | Університет | Прокат авто | Бібліотека | Спортзал |
|---------|--------|----------|-------------|-------------|------------|---------|
| `ClinicFormatter.FormatBloodType` | `HotelFormatter.FormatRoomType` | `RestaurantFormatter.FormatCategory` | `UnivFormatter.FormatFaculty` | `RentalFormatter.FormatCarClass` | `LibraryFormatter.FormatGenre` | `GymFormatter.FormatTrainingType` |
| `PatientManager[i]` | `GuestManager[i]` | `CustomerManager[i]` | `StudentManager[i]` | `ClientManager[i]` | `ReaderManager[i]` | `MemberManager[i]` |

### Коміт

```bash
git add ClinicApp/ClinicFormatter.cs
git add ClinicApp/Patient.cs ClinicApp/Doctor.cs
git add ClinicApp/PatientManager.cs ClinicApp/DoctorManager.cs ClinicApp/AppointmentManager.cs
git commit -m "Lab04 Task03"
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

1. **Перевантаження — це просто два методи з однаковим іменем і різними
   параметрами.** `FindBySpeciality(string)` шукає за частиною рядка (як у Лабі
   03), `FindBySpeciality(Speciality)` — точним порівнянням `enum`. Компілятор
   сам обере потрібний за типом аргументу на місці виклику.
2. **`GetByDate(int, int, int)` не дублює логіку** — він будує `DateTime` з трьох
   чисел і викликає вже наявний `GetByDate(DateTime)`. Одна реалізація,
   зручніший вхід.
3. **`TryFindById` — патерн `TryXxx`.** Метод повертає `bool` (знайшли чи ні), а
   сам об'єкт віддає через параметр `out`. Всередині він спирається на наявний
   `FindById`. Прочитайте про `out` і про те, чому цей патерн зручніший за
   «повернути `null` і сподіватись, що викличок перевірить».
4. **`FindByBloodType(BloodType)`** — той самий двопрохідний патерн, що
   `FindByName` у Лабі 03, лише умова — точна рівність `enum` (`==`), а не
   входження підрядка.
5. **`?.` (null-conditional)** — звертання до члена, що безпечно дає `null`,
   якщо ліворуч `null`. **`??` (null-coalescing)** — підставляє запасне
   значення замість `null`. Разом: `FindById(99)?.FullName ?? "невідомий"` не
   впаде на неіснуючому ID. Ці оператори дозволені саме з цієї лаби —
   продемонструйте їх у `Program.cs`.

📖 Документація:
- [Перевантаження методів](https://learn.microsoft.com/dotnet/csharp/programming-guide/classes-and-structs/methods#method-signatures)
- [Параметр `out` і патерн Try](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/out-parameter-modifier)
- [Оператори `?.` та `?[]`](https://learn.microsoft.com/dotnet/csharp/language-reference/operators/member-access-operators#null-conditional-operators--and-)
- [Оператор `??`](https://learn.microsoft.com/dotnet/csharp/language-reference/operators/null-coalescing-operator)

### Адаптація до вашого домену

| Клініка | Готель | Ресторан | Університет | Прокат авто | Бібліотека | Спортзал |
|---------|--------|----------|-------------|-------------|------------|---------|
| `FindBySpeciality(Speciality)` | `FindByDepartment(Department)` | `FindByCategory(DishCategory)` | `FindByFaculty(Faculty)` | `FindByClass(CarClass)` | `FindBySection(LibrarySection)` | `FindByType(TrainingType)` |
| `TryFindById(id, out Patient)` | `TryFindById(id, out Guest)` | `TryFindById(id, out Customer)` | `TryFindById(id, out Student)` | `TryFindById(id, out Client)` | `TryFindById(id, out Reader)` | `TryFindById(id, out Member)` |

### Коміт

```bash
git add ClinicApp/PatientManager.cs ClinicApp/DoctorManager.cs ClinicApp/AppointmentManager.cs ClinicApp/Program.cs
git commit -m "Lab04 Task04"
```

---

## Перевірка перед здачею

```bash
dotnet build ClinicApp
dotnet run --project ClinicApp
```

Переконайтесь, що:

- [ ] Проєкт компілюється без помилок і попереджень
- [ ] У коді **немає рядкових літералів** `"Scheduled"`, `"A+"`, `"Кардіологія"` —
  усе через `enum`
- [ ] `BloodType.APositive` показується як `"A+"` (через `ClinicFormatter`)
- [ ] `Doctor.Schedule.ToString()` повертає `"08:00–16:00 (8 год)"`
- [ ] `clinic.Patients[0]` повертає першого пацієнта, `clinic.Patients[999]` — `null`
- [ ] `clinic.Doctors.FindBySpeciality(Speciality.Cardiology)` і
  `FindBySpeciality("кардіо")` обидва працюють (перевантаження)
- [ ] `TryFindById(99, out var p)` повертає `false` і не кидає виняток
- [ ] `FindById(99)?.FullName ?? "не знайдено"` не падає з `NullReferenceException`
- [ ] `FormatAge`: 1 → «1 рік», 3 → «3 роки», 11 → «11 років», 21 → «21 рік»

---

## Питання для самоперевірки

1. Що саме перевіряє компілятор, коли статус — `enum`, і чого він **не** може
   перевірити, коли статус — `string`?
2. `WorkSchedule a = b; a.Start = 10;` — чому цей код навіть не скомпілюється, і
   що було б з `b`, якби `Start` мав `set`?
3. Чому `ClinicFormatter` — `static class`, а не звичайний клас, який просто
   ніхто не інстанціює? Що дає ключове слово `static` на класі?
4. Індексатор і звичайна властивість — у чому синтаксична й змістова різниця?
5. `TryFindById` повертає `bool` + `out`. Чим це надійніше за «повернути
   `Patient?` і сподіватись на перевірку `!= null`»?
6. `FindBySpeciality(string)` і `FindBySpeciality(Speciality)` — це перевантаження.
   Чи можна було б обійтися одним методом з необов'язковим параметром? Чому ні?

---

## Статус гілки

Після всіх 4 завдань (кожне — окремий коміт `Lab04 TaskNN` на гілці `Lab-04`):

```bash
git push -u origin Lab-04
git checkout main
git merge --no-ff Lab-04 -m "Merge Lab-04: Class Members"
git push
```

> Наступна лаба: `git checkout main` → `git checkout -b Lab-05`.
