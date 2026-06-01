# Лаба 05 — Інкапсуляція

## Мета

Навчитися приховувати внутрішній стан об'єктів за допомогою `private` полів та властивостей, забезпечити цілісність даних через валідацію в сеттерах, організувати проєкт у підпапки з підпросторами імен і обробляти виняткові ситуації через `try/catch`.

## Контекст

Після Лаби 04 система має хороші типи (`enum`, `struct`), але будь-хто може зробити `patient.FirstName = ""` або `new WorkSchedule(25, 3)` — і ніякого захисту. Об'єкти зберігають некоректні дані мовчки.

Ця лаба вирішує це системно:
1. Фізично впорядковуємо файли проєкту за підпапками та підпросторами імен.
2. Закриваємо поля через `private` і відкриваємо лише керований доступ через властивості.
3. Додаємо валідацію у сеттери — некоректні значення кидають виняток ще до збереження.
4. Виносимо правила валідації в окремий утилітний клас.
5. Обробляємо винятки у `Program.cs`, щоб програма не падала, а показувала зрозуміле повідомлення.

---

## Гілка

```bash
git checkout main
git checkout -b feature/encapsulation
```

> Гілка **зливається в `main`** після завершення всіх завдань.

---

## Задача 1. Підпапки та підпростори імен ⭐⭐

### Умова

Усі 14 `.cs` файлів зараз лежать у корені `src/`. Коли проєкт зростає, розібратись у плоскому списку файлів стає складно. Стандартна практика .NET — організовувати файли за відповідальністю у підпапки і відображати це у просторах імен.

**Що реалізувати:**

Перенесіть файли у такі підпапки, оновіть `namespace` і додайте `using`:

| Підпапка | Файли |
|----------|-------|
| `src/Models/` | `Patient.cs`, `Doctor.cs`, `Appointment.cs`, `WorkSchedule.cs` |
| `src/Enums/` | `BloodType.cs`, `Speciality.cs`, `AppointmentStatus.cs` |
| `src/Managers/` | `PatientManager.cs`, `DoctorManager.cs`, `AppointmentManager.cs` |
| `src/Utils/` | `ClinicFormatter.cs` |

Старі файли у корені `src/` — видалити.

### Специфікація

| Підпапка | Namespace |
|----------|-----------|
| `src/Models/` | `ClinicApp.Models` |
| `src/Enums/` | `ClinicApp.Enums` |
| `src/Managers/` | `ClinicApp.Managers` |
| `src/Utils/` | `ClinicApp.Utils` |
| `src/` (Clinic.cs, Program.cs) | `ClinicApp` |

Кожен файл повинен мати `using` для кожного підпростору імен, яким він користується. Наприклад, `PatientManager.cs` після переносу:

```csharp
namespace ClinicApp.Managers;

using ClinicApp.Enums;
using ClinicApp.Models;
using ClinicApp.Utils;
```

### Приклад

```
src/
  Clinic.cs           ← namespace ClinicApp
  GrowablePatientManager.cs ← namespace ClinicApp; using ClinicApp.Models;
  Program.cs          ← using ClinicApp; using ClinicApp.Models; ...
  Enums/
    AppointmentStatus.cs  ← namespace ClinicApp.Enums
    BloodType.cs
    Speciality.cs
  Managers/
    AppointmentManager.cs ← namespace ClinicApp.Managers; using ClinicApp.Models; ...
    DoctorManager.cs
    PatientManager.cs
  Models/
    Appointment.cs    ← namespace ClinicApp.Models; using ClinicApp.Enums; ...
    Doctor.cs
    Patient.cs
    WorkSchedule.cs
  Utils/
    ClinicFormatter.cs ← namespace ClinicApp.Utils; using ClinicApp.Enums;
```

### Підказки

1. У .NET 6+ (file-scoped namespace) синтаксис:
   ```csharp
   namespace ClinicApp.Models;   // крапка з комою — діє на весь файл
   ```
   Альтернатива зі старим синтаксисом:
   ```csharp
   namespace ClinicApp.Models
   {
       public class Patient { ... }
   }
   ```
2. `using` директиви ставляться після `namespace` (file-scoped) або у верхній частині файлу.
3. Якщо два простори імен містять однаковий тип (наприклад, `Patient`), C# вимагає уточнення: `ClinicApp.Models.Patient`. Але якщо є `using ClinicApp.Models;` — достатньо просто `Patient`.
4. `csproj` файл **не потребує змін** — .NET підхоплює всі `.cs` файли в підпапках автоматично.
5. Фізичне переміщення файлів і оновлення `namespace` — це окремі кроки. Зручно: спочатку скопіювати файл у нову папку, оновити namespace, потім видалити старий. Або через IDE — "Move to folder", яке оновить namespace автоматично.
6. Для видалення старих файлів через git:
   ```bash
   git rm src/Patient.cs src/Doctor.cs ...
   ```

📖 [Namespaces (Microsoft Docs)](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/types/namespaces)
📖 [Organizing and testing projects (Microsoft Docs)](https://learn.microsoft.com/en-us/dotnet/core/tutorials/testing-with-cli)

### Адаптація до вашого домену

| Клініка | Готель | Ресторан | Університет | Прокат авто | Бібліотека | Спортзал |
|---------|--------|----------|-------------|-------------|------------|---------|
| `ClinicApp.Models` | `HotelApp.Models` | `RestaurantApp.Models` | `UniversityApp.Models` | `RentalApp.Models` | `LibraryApp.Models` | `GymApp.Models` |
| `ClinicApp.Enums` | `HotelApp.Enums` | `RestaurantApp.Enums` | `UniversityApp.Enums` | `RentalApp.Enums` | `LibraryApp.Enums` | `GymApp.Enums` |
| `ClinicApp.Managers` | `HotelApp.Managers` | `RestaurantApp.Managers` | `UniversityApp.Managers` | `RentalApp.Managers` | `LibraryApp.Managers` | `GymApp.Managers` |
| `ClinicApp.Utils` | `HotelApp.Utils` | `RestaurantApp.Utils` | `UniversityApp.Utils` | `RentalApp.Utils` | `LibraryApp.Utils` | `GymApp.Utils` |

### Коміт

```bash
git add src/Models/ src/Enums/ src/Managers/ src/Utils/
git add src/Clinic.cs src/Program.cs src/GrowablePatientManager.cs
git rm src/Patient.cs src/Doctor.cs ...   # старі файли
git commit -m "Lab05 Task1: reorganize files into subfolders with sub-namespaces"
```

---

## Задача 2. Private поля та властивості — інкапсуляція ⭐⭐⭐

### Умова

Зараз поля у `Patient` і `Doctor` — публічні або повністю auto-property: `public string FirstName { get; set; }`. Будь-який код може встановити будь-яке значення без жодних перевірок.

Інкапсуляція вирішує це: **дані — `private`, доступ — через властивості**. Властивість може обмежити або перевірити значення, але з зовнішнього боку виглядає так само.

**Що реалізувати:**

У `Patient` замінити автовластивості на приватні поля + явні властивості для:
- `FirstName`, `LastName`, `DateOfBirth`, `Phone`

У `Doctor` аналогічно для:
- `FirstName`, `LastName`, `LicenseNumber`, `Phone`

У `Appointment` для:
- `DurationMinutes`

### Специфікація

```csharp
// Приватне поле — "склад" даних
private string _firstName = "";

// Властивість — єдиний офіційний вхід/вихід
public string FirstName
{
    get => _firstName;
    set { /* валідація */ _firstName = value; }
}
```

Поки що сеттери просто присвоюють значення без перевірок (перевірки — у Задачах 3 і 4). Мета цієї задачі — правильна структура.

### Приклад

```csharp
// Auto-property (до):
public string FirstName { get; set; }

// Приватне поле + явна властивість (після):
private string _firstName = "";
public string FirstName
{
    get => _firstName;
    set => _firstName = value;
}
```

З зовнішнього боку поведінка **ідентична**. Весь існуючий код `patient.FirstName = "Іван"` продовжує працювати.

### Підказки

1. Угода про іменування приватних полів: `_camelCase` (підкреслення + маленька літера).
2. `get => _firstName;` — скорочений запис, еквівалентний `get { return _firstName; }`.
3. Властивості з readonly ініціалізацією (`{ get; }`) можна ініціалізувати тільки в конструкторі. Якщо потрібна перевірка при зміні ззовні — потрібна явна властивість із сеттером.
4. `Id` у `Patient` і `Doctor` — залишити `{ get; }` без сеттера: ID призначається лише раз у конструкторі.
5. `BloodType` і `Speciality` (enum) — можна залишити auto-property `{ get; set; }`, оскільки enum не може мати "некоректне" значення.
6. `Email` — теж залишити auto-property, перевірка через regex стане опційним завданням.

📖 [Properties (C# Programming Guide)](https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/properties)
📖 [Access Modifiers](https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/access-modifiers)

### Адаптація до вашого домену

| Клініка | Готель | Ресторан | Університет | Прокат авто | Бібліотека | Спортзал |
|---------|--------|----------|-------------|-------------|------------|---------|
| `Patient`, `Doctor`, `Appointment` | `Guest`, `Staff`, `Booking` | `Customer`, `Waiter`, `TableReservation` | `Student`, `Lecturer`, `Enrollment` | `Client`, `Manager`, `Rental` | `Reader`, `Librarian`, `BookLoan` | `Member`, `Trainer`, `Session` |
| `_firstName`, `_lastName`, `_phone` | `_firstName`, `_lastName`, `_phone` | `_firstName`, `_lastName`, `_phone` | `_firstName`, `_lastName`, `_phone` | `_firstName`, `_lastName`, `_phone` | `_firstName`, `_lastName`, `_phone` | `_firstName`, `_lastName`, `_phone` |

### Коміт

```bash
git add src/Models/Patient.cs src/Models/Doctor.cs src/Models/Appointment.cs
git commit -m "Lab05 Task2: add private backing fields to Patient, Doctor, Appointment"
```

---

## Задача 3. Валідація у сеттерах — throw ⭐⭐⭐

### Умова

Приватні поля вже захищають від прямого доступу, але сеттер поки що приймає будь-яке значення. Наступний крок — перевірити значення перед збереженням і кинути виняток, якщо воно некоректне.

C# надає вбудовані типи винятків для типових ситуацій:
- `ArgumentException` — аргумент взагалі некоректний (порожній рядок, недопустиме значення)
- `ArgumentOutOfRangeException` — число або дата виходить за допустимі межі

**Що реалізувати:**

Додати перевірки у сеттери:

| Поле | Умова | Тип винятку |
|------|-------|-------------|
| `FirstName`, `LastName` у `Patient`, `Doctor` | порожній або whitespace → помилка; довжина > 50 → помилка | `ArgumentException` |
| `LicenseNumber` у `Doctor` | порожній або whitespace → помилка | `ArgumentException` |
| `Phone` у `Patient`, `Doctor` | не 10 символів → помилка; не лише цифри → помилка | `ArgumentException` |
| `DateOfBirth` у `Patient` | в майбутньому → помилка; раніше 1900 → помилка | `ArgumentOutOfRangeException` |
| `DurationMinutes` у `Appointment` | ≤ 0 → помилка | `ArgumentOutOfRangeException` |
| `WorkSchedule(int, int)` | start < 0 або > 23 → помилка; end < 1 або > 24 → помилка; start >= end → помилка | `ArgumentOutOfRangeException` / `ArgumentException` |

### Приклад

```csharp
public string FirstName
{
    get => _firstName;
    set
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Ім'я не може бути порожнім.");
        if (value.Length > 50)
            throw new ArgumentException("Ім'я занадто довге (макс. 50 символів).");
        _firstName = value;
    }
}

public DateTime DateOfBirth
{
    get => _dateOfBirth;
    set
    {
        if (value > DateTime.Today)
            throw new ArgumentOutOfRangeException(nameof(DateOfBirth), "Дата не може бути в майбутньому.");
        if (value.Year < 1900)
            throw new ArgumentOutOfRangeException(nameof(DateOfBirth), "Дата не може бути раніше 1900 року.");
        _dateOfBirth = value;
    }
}
```

```csharp
// Запуск: якщо передати некоректне ім'я — виняток
Patient p = new Patient("", "Петренко", new DateTime(1990, 1, 1), BloodType.OPositive, "0501234567");
// System.ArgumentException: Ім'я не може бути порожнім.
```

### Підказки

1. `throw` — оператор, що викидає виняток і негайно переривав виконання поточного методу.
2. `ArgumentException(string message)` — передайте зрозуміле повідомлення для програміста.
3. `ArgumentOutOfRangeException(string paramName, string message)` — два аргументи: ім'я параметра і пояснення.
4. `nameof(PropertyName)` — повертає ім'я властивості як рядок. Краще ніж `"DateOfBirth"` — компілятор перевіряє і перейменовується разом.
5. `string.IsNullOrWhiteSpace(value)` — повертає `true` якщо рядок `null`, порожній або містить лише пробіли.
6. Для перевірки Phone:
   ```csharp
   if (phone.Length != 10)
       throw new ArgumentException("Телефон має містити рівно 10 цифр.");
   for (int i = 0; i < phone.Length; i++)
       if (phone[i] < '0' || phone[i] > '9')
           throw new ArgumentException("Телефон має містити тільки цифри.");
   ```
7. Валідація відбувається у конструкторі теж: якщо конструктор присвоює поля через властивості (`FirstName = firstName;`), то перевірки спрацюють автоматично.
8. Якщо конструктор ще пише напряму в поле (`_firstName = firstName;`) — перепишіть на присвоєння через властивість.

📖 [throw (C# Reference)](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/statements/exception-handling-statements#the-throw-statement)
📖 [ArgumentException](https://learn.microsoft.com/en-us/dotnet/api/system.argumentexception)
📖 [ArgumentOutOfRangeException](https://learn.microsoft.com/en-us/dotnet/api/system.argumentoutofrangeexception)

### Адаптація до вашого домену

| Клініка | Готель | Ресторан | Університет | Прокат авто | Бібліотека | Спортзал |
|---------|--------|----------|-------------|-------------|------------|---------|
| `FirstName`/`LastName` не порожні | `FirstName`/`LastName` не порожні | `FirstName`/`LastName` не порожні | `FirstName`/`LastName` не порожні | `FirstName`/`LastName` не порожні | `FirstName`/`LastName` не порожні | `FirstName`/`LastName` не порожні |
| `DateOfBirth` не в майбутньому | `CheckInDate` не в минулому | `ReservationDate` не в минулому | `EnrollmentDate` в межах року | `RentalStart` не в минулому | `LoanDate` не в майбутньому | `SessionDate` не в минулому |
| `DurationMinutes` > 0 | `StayNights` > 0 | `Duration` > 0 | `CourseDays` > 0 | `RentalDays` > 0 | `LoanDays` > 0 | `DurationMinutes` > 0 |
| `WorkSchedule(start, end)`: start < end | аналогічно | аналогічно | аналогічно | аналогічно | аналогічно | аналогічно |

### Коміт

```bash
git add src/Models/Patient.cs src/Models/Doctor.cs src/Models/Appointment.cs src/Models/WorkSchedule.cs
git commit -m "Lab05 Task3: add validation with throw in property setters"
```

---

## Задача 4. ClinicValidator та try/catch ⭐⭐⭐⭐

### Умова

Правила валідації повторюються: `FirstName`, `LastName`, `LicenseNumber` — всі перевіряються однаково. Копіювати один і той самий код — погана практика. Якщо правило зміниться (наприклад, максимальна довжина стане 100), доведеться змінювати в кожному місці.

Рішення: **виненсіть правила у окремий статичний клас** `ClinicValidator`. Всі сеттери викликають його методи — правило прописане один раз.

Друга частина задачі: код у `Program.cs`, що створює пацієнтів і лікарів за введенням користувача, може отримати некоректні дані. Якщо не обробити виняток — програма впаде. `try/catch` дозволяє перехопити виняток і показати зрозуміле повідомлення.

**Що реалізувати:**

1. `static class ClinicValidator` у `src/Utils/`:
   - `ValidateName(string value, string fieldName)` — не порожній, ≤ 50 символів
   - `ValidatePhone(string phone)` — 10 символів, тільки цифри
   - `ValidateDate(DateTime value, string fieldName)` — не в майбутньому, не раніше 1900
   - `ValidatePositive(int value, string fieldName)` — більше нуля

2. Переписати сеттери у `Patient`, `Doctor`, `Appointment`, `WorkSchedule` — замість inline-перевірок викликати `ClinicValidator`.

3. У `Program.cs` в меню "Додати пацієнта" та "Додати лікаря" огорнути `new Patient(...)` та `new Doctor(...)` у `try/catch`:

```csharp
try
{
    clinic.Patients.Add(new Patient(firstName, lastName, dob, bloodType, phone));
}
catch (ArgumentOutOfRangeException e)
{
    Console.WriteLine("Помилка: " + e.Message);
}
catch (ArgumentException e)
{
    Console.WriteLine("Помилка: " + e.Message);
}
```

### Специфікація ClinicValidator

```csharp
namespace ClinicApp.Utils;

public static class ClinicValidator
{
    // Перевіряє: не null/whitespace, довжина ≤ 50
    public static void ValidateName(string value, string fieldName) { ... }

    // Перевіряє: не null/whitespace, рівно 10 символів, тільки цифри
    public static void ValidatePhone(string phone) { ... }

    // Перевіряє: не в майбутньому, не раніше 1900
    public static void ValidateDate(DateTime value, string fieldName) { ... }

    // Перевіряє: value > 0
    public static void ValidatePositive(int value, string fieldName) { ... }
}
```

Тіла методів реалізуйте самостійно, спираючись на перевірки з Задачі 3 (вони вже написані — тепер централізуйте їх тут). Тип винятку: `ArgumentException` або `ArgumentOutOfRangeException` відповідно до правил з Задачі 3.

### Приклад

```csharp
// Сеттер Patient.FirstName — після рефакторингу:
public string FirstName
{
    get => _firstName;
    set { ClinicValidator.ValidateName(value, "Ім'я"); _firstName = value; }
}
```

Аналогічно перепишіть інші сеттери у `Patient`, `Doctor`, `Appointment`, `WorkSchedule`. Для `WorkSchedule` валідація розташована у конструкторі — він не підтримує звичайних сеттерів, перевіряйте параметри безпосередньо перед присвоєнням.

```csharp
// Program.cs — обробка помилки при введенні користувача:
try
{
    clinic.Doctors.Add(new Doctor(firstName, lastName, speciality, license, phone));
}
catch (ArgumentOutOfRangeException e)
{
    Console.WriteLine("Помилка: " + e.Message);
}
catch (ArgumentException e)
{
    Console.WriteLine("Помилка: " + e.Message);
}
// → Програма НЕ падає. Показує повідомлення і повертається до меню.
```

### Підказки

1. `try/catch` — блок `try` виконується як звичайно; якщо виникає виняток — виконання стрибає в блок `catch`. Виконання коду після місця помилки у `try` пропускається.
2. Порядок `catch` важливий: **спочатку — конкретніший тип**. `ArgumentOutOfRangeException` є підкласом `ArgumentException`, тому він має стояти першим. Інакше — помилка компіляції (`CS0160`).
3. Якщо перехопити `Exception` (базовий клас всіх винятків) — він перехопить все. Це зручно для логування, але приховує деталі.
4. Блок `finally` (необов'язковий) виконується завжди — і після `try`, і після `catch`. Корисний для звільнення ресурсів.
5. Виняток, не перехоплений ніде, завершує програму з stack trace. `try/catch` — це межа між "внутрішньою логікою" і "зовнішнім світом" (введення користувача, файли, мережа).
6. `e.Message` — рядок із поясненням, `e.GetType().Name` — назва класу винятку.

📖 [try-catch (C# Reference)](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/statements/exception-handling-statements)
📖 [Exception handling (C# Fundamentals)](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/exceptions/)
📖 [Best practices for exceptions](https://learn.microsoft.com/en-us/dotnet/standard/exceptions/best-practices-for-exceptions)

### Адаптація до вашого домену

| Клініка | Готель | Ресторан | Університет | Прокат авто | Бібліотека | Спортзал |
|---------|--------|----------|-------------|-------------|------------|---------|
| `ClinicValidator` | `HotelValidator` | `RestaurantValidator` | `UnivValidator` | `RentalValidator` | `LibraryValidator` | `GymValidator` |
| `ValidateName` | `ValidateGuestName` | `ValidateDishName` | `ValidateStudentName` | `ValidateClientName` | `ValidateReaderName` | `ValidateMemberName` |
| `ValidatePhone` | `ValidatePhone` | `ValidatePhone` | `ValidatePhone` | `ValidatePhone` | `ValidatePhone` | `ValidatePhone` |
| `ValidateDate` | `ValidateCheckInDate` | `ValidateReservationDate` | `ValidateEnrollmentDate` | `ValidateRentalDate` | `ValidateLoanDate` | `ValidateSessionDate` |

### Коміт

```bash
git add src/Utils/ClinicValidator.cs
git add src/Models/Patient.cs src/Models/Doctor.cs src/Models/Appointment.cs src/Models/WorkSchedule.cs
git add src/Program.cs
git commit -m "Lab05 Task4: add ClinicValidator, refactor setters, add try/catch in Program"
```

---

## Задача 5 (опційна). Regex для перевірки телефону ⭐⭐⭐

### Умова

Метод `ValidatePhone` у `ClinicValidator` зараз використовує `for`-цикл для перевірки цифр. Регулярні вирази (`Regex`) — потужніший і лаконічніший інструмент для перевірки формату рядків за шаблоном.

**Що реалізувати:**

1. Замінити `for`-цикл у `ValidatePhone` на `Regex.IsMatch`.
2. Опційно: розширити формат — підтримати `+38XXXXXXXXXX` (12 цифр після `+38`) як альтернативу 10-значному номеру.
3. Додати до `ClinicValidator` метод `ValidateEmail(string email)` — перевірка базового формату email через Regex.

### Специфікація

```csharp
// Проста версія (тільки 10 цифр)
private static readonly System.Text.RegularExpressions.Regex _phoneRegex
    = new System.Text.RegularExpressions.Regex(@"^\d{10}$");

public static void ValidatePhone(string phone)
{
    if (string.IsNullOrWhiteSpace(phone))
        throw new ArgumentException("Телефон не може бути порожнім.");
    if (!_phoneRegex.IsMatch(phone))
        throw new ArgumentException("Телефон має містити рівно 10 цифр.");
}

// Перевірка email
private static readonly System.Text.RegularExpressions.Regex _emailRegex
    = new System.Text.RegularExpressions.Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");

public static void ValidateEmail(string email)
{
    if (!_emailRegex.IsMatch(email))
        throw new ArgumentException("Некоректний формат email.");
}
```

### Приклад

```csharp
// Regex шаблони:
@"^\d{10}$"       // ^ = початок, \d = цифра, {10} = рівно 10, $ = кінець
@"^\d{10,12}$"    // від 10 до 12 цифр

// Виклик:
bool ok = Regex.IsMatch("0501234567", @"^\d{10}$");  // true
bool ok2 = Regex.IsMatch("050abc4567", @"^\d{10}$"); // false
```

### Підказки

1. `Regex.IsMatch(input, pattern)` — статичний метод, повертає `bool`.
2. Зберігати `Regex` як `static readonly` поле — компіляція шаблону відбувається один раз, а не при кожному виклику.
3. Символи шаблону: `\d` = цифра, `\w` = буква/цифра/підкреслення, `.` = будь-який символ, `^` = початок, `$` = кінець.
4. `{n}` = рівно n разів, `{n,m}` = від n до m разів, `+` = один або більше, `*` = нуль або більше.
5. Шаблони пишуться як verbatim string `@"..."` — зворотний слеш не потребує екранування.

📖 [Regular expressions in .NET](https://learn.microsoft.com/en-us/dotnet/standard/base-types/regular-expressions)
📖 [Regex class](https://learn.microsoft.com/en-us/dotnet/api/system.text.regularexpressions.regex)
📖 [Regular expression language — quick reference](https://learn.microsoft.com/en-us/dotnet/standard/base-types/regular-expression-language-quick-reference)

### Коміт

```bash
git add src/Utils/ClinicValidator.cs
git commit -m "Lab05 Task5 (optional): replace phone loop with Regex, add email validation"
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
- [ ] Файли розміщені у підпапках `Models/`, `Enums/`, `Managers/`, `Utils/`
- [ ] Кожен клас має правильний `namespace` і потрібні `using` директиви
- [ ] `Patient`, `Doctor`, `Appointment` мають приватні поля `_camelCase`
- [ ] Спроба `new Patient("", "Петренко", ...)` кидає `ArgumentException`
- [ ] Спроба `new Patient("Іван", "Петренко", DateTime.Today.AddDays(1), ...)` кидає `ArgumentOutOfRangeException`
- [ ] Спроба `new WorkSchedule(20, 6)` кидає виняток
- [ ] У меню "Додати пацієнта" при введенні некоректних даних програма **не падає**, а показує повідомлення
- [ ] Весь попередній функціонал (пошук, запис, звіт) працює як раніше

---

## Питання для самоперевірки

1. Що таке інкапсуляція? Чому вона корисна?
2. Яка різниця між `private string _name` і `public string Name { get; set; }`?
3. Навіщо `private` поле, якщо властивість `{ get; set; }` і так приховує деталі?
4. У якому порядку мають стояти `catch` блоки? Чому `ArgumentOutOfRangeException` стоїть перед `ArgumentException`?
5. Що станеться, якщо виняток не перехоплений жодним `catch`?
6. Чому `ClinicValidator` — `static class`? Чим це відрізняється від `ClinicFormatter` з Лаби 04?
7. Навіщо зберігати `Regex` як `static readonly`? Що буде, якщо створювати `new Regex(...)` у кожному виклику методу?

---

## Злиття

```bash
git checkout main
git merge --no-ff feature/encapsulation -m "Merge feature/encapsulation: Lab05 Encapsulation"
```

> Наступна лаба: `git checkout -b feature/inheritance` — наслідування та поліморфізм.
