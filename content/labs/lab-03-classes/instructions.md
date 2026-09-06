# Лаба 03 — Визначення класів

## Мета
Навчитися проєктувати та реалізовувати класи з полями, властивостями, конструкторами і методами — і зібрати перші три сутності медичної системи у робочий консольний застосунок.

## Контекст

Лаби 01–02 були тренажерами синтаксису. **З цієї лаби починається основний
проєкт `ClinicApp/`**, який росте до кінця курсу (Лаби 03–22). Це перша лаба,
гілка якої **зливається в `main`**.

Станом на початок:
- `Lab01/`, `Lab02/` — тренажери (лишились на своїх гілках, у `main` їх нема);
- `main` містить лише `.gitignore` і порожнє рішення `oop-course.sln`.

Після цієї лаби у `ClinicApp/` будуть класи `Patient`, `Doctor`, `Appointment`,
три менеджери-колекції на масивах та клас `Clinic`, що їх об'єднує, — і працююче
консольне меню.

### Що дозволено в цій лабі

Тільки те, що ви вже знаєте: **класи, поля, властивості, конструктори, методи,
масиви й цикли**. Свідомо **не використовуємо**:

- `List<T>`, `Dictionary<,>` та будь-які інші узагальнені колекції — зберігання
  лише через **масив фіксованого розміру + лічильник `_count`** (як у Лабі 02).
  Узагальнення (generics) — це Лаба 09;
- LINQ (`.Where`, `.FirstOrDefault`, `.Average` …) — Лаба 14;
- скорочені оператори `?.` і `??` — Лаба 04 (тут пишемо явні `== null` / `!= null`);
- патерн `is not null` та інші патерни зіставлення — значно пізніше;
- `enum`, `struct`, `interface`, `abstract` — наступні лаби.

Обмеження навмисні: спочатку ви руками пишете лінійний пошук і зростання масиву,
а вже потім курс показує готові інструменти й ви розумієте, що всередині них.

---

## Коротко про `null` і тип `T?`

У проєкті увімкнено `<Nullable>enable</Nullable>` — компілятор відстежує, які
змінні можуть бути `null`, і попереджає про небезпечні звернення.

Що вам знадобиться в цій лабі — рівно чотири речі:

1. **`null` — це «посилання в нікуди».** Змінна типу класу або тримає об'єкт, або
   дорівнює `null`. Звернення до члена через `null` (`patient.FullName`, коли
   `patient == null`) кидає `NullReferenceException`.

2. **Метод, який може «не знайти», повертає тип зі знаком `?`.** Наприклад
   `FindById` повертає `Patient?` — «або пацієнт, або `null`». Знак `?` — це
   сигнал і вам, і компілятору: результат треба перевірити.

3. **Перевіряйте звичайним порівнянням.** Отримавши `Patient?`, спершу
   `if (result == null)` (обробити відсутність) або `if (result != null)`
   (працювати далі). Тільки після такої перевірки компілятор дозволить
   звертатись до `result.FullName`.

   ```csharp
   Patient? found = manager.FindById(id);
   if (found == null)
   {
       Console.WriteLine("Не знайдено.");
       return;
   }
   Console.WriteLine(found.FullName);   // тут компілятор уже спокійний
   ```

4. **`Console.ReadLine()!`** — при читанні вводу залишайте знак `!` після
   `ReadLine()` (як у Лабах 01–02): «я гарантую, що тут не `null`».

Скорочені оператори `?.` і `??` для роботи з `null` — у **Лабі 04**. Тут —
тільки явні перевірки `== null` / `!= null`.

📖 [Nullable-типи посилань](https://learn.microsoft.com/dotnet/csharp/nullable-references) ·
[Оператор `!` (null-forgiving)](https://learn.microsoft.com/dotnet/csharp/language-reference/operators/null-forgiving)

---

## Крок 1. Проєкт `ClinicApp`

> **Робочий процес** (повністю — [Git Воркшоп](https://tomka.space/git-workshop/)):
> лаба = гілка `Lab-XX` від `main`, коміт на кожне завдання (`LabXX TaskYY`), у
> кінці — злиття в `main`. **Лаби 01–02 у `main` не зливались; з цієї лаби —
> зливаються.**

Рішення `oop-course.sln` створене в Лабі 01. Тут — гілка `Lab-03` і новий проєкт.

```bash
git checkout main
git checkout -b Lab-03

dotnet new console -o ClinicApp --name ClinicApp
dotnet sln add ClinicApp/ClinicApp.csproj
```

Перевірте, що у `ClinicApp/ClinicApp.csproj` увімкнені такі властивості (додайте
відсутні):

```xml
<PropertyGroup>
  <OutputType>Exe</OutputType>
  <TargetFramework>net8.0</TargetFramework>
  <Nullable>enable</Nullable>
  <ImplicitUsings>enable</ImplicitUsings>
</PropertyGroup>
```

Перший запуск і коміт:

```bash
dotnet run --project ClinicApp        # має вивести Hello, World!
git add oop-course.sln ClinicApp/
git commit -m "Lab03: project"
```

---

## Як виконувати завдання

На відміну від Лаб 01–02, тут **немає** файлів `Task1.cs`. Кожне завдання додає
новий файл із класом у **той самий проєкт** `ClinicApp/`, і клас одразу
використовується у спільному `ClinicApp/Program.cs`. Коміт — на кожне завдання
(`Lab03 TaskNN`).

Структура після завершення всіх завдань:

```
ClinicApp/
├── ClinicApp.csproj
├── Program.cs                  ← меню та тестовий код
├── Patient.cs                  ← Задача 1
├── Doctor.cs                   ← Задача 2
├── PatientManager.cs           ← Задача 3
├── DoctorManager.cs            ← Задача 4
├── Appointment.cs              ← Задача 5
├── AppointmentManager.cs       ← Задача 6
├── Clinic.cs                   ← Задача 7
└── GrowablePatientManager.cs   ← Задача 8
```

Усі класи — у просторі імен `ClinicApp` (`namespace ClinicApp;` на початку файлу).

### Ваш домен

За замовчуванням виконуйте завдання **як написано** (домен «клініка»). Якщо ведете
власний домен — таблиця **«Адаптація до вашого домену»** в кінці кожного завдання
показує відповідники сутностей і методів. Структуру класів зберігайте.

### Як користуватися підказками

Підказки — це **напрям думки, а не готовий код**. Специфікація класу (таблиця
членів) каже, *що* має бути; підказки кажуть, *як міркувати*; блок
**📖 Документація** — куди піти по деталі синтаксису. Спершу документація і
власна спроба, і лише потім наступний крок підказки.

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

1. **Лічильник ID — статичне поле.** Одне поле, спільне для всіх об'єктів класу
   (не для кожного окремо). Прочитайте про `static` члени: різниця між полем
   екземпляра і полем класу.
2. **`Id` не можна змінювати ззовні.** Це властивість «лише читання»: значення
   присвоюється один раз у конструкторі й далі незмінне. Знайдіть у документації
   get-only auto-property.
3. **Присвоєння Id у повному конструкторі.** Візьміть поточне значення лічильника,
   а потім збільште його на 1 — так наступний пацієнт отримає наступний номер.
   Розберіться з різницею пре- та постінкремента.
4. **Ланцюжок конструкторів.** Коротші конструктори не дублюють присвоєння полів,
   а делегують роботу повному через `: this(...)`. Спроєктуйте так, щоб `Id++`
   траплявся рівно в одному місці — у повному конструкторі. Документація —
   «constructor chaining» / `this` як ініціалізатор конструктора.
5. **Обчислення віку.** Різниця років — це груба оцінка; якщо день народження
   цього року ще не настав, вік на 1 менший. Продумайте перевірку на папері для
   дати народження 31 грудня. `DateTime` має властивості `Today`, `Year` і метод
   зсуву дати — шукайте в документації.
6. **`GetAgeCategory()`** — той самий каскад `if`, що в Задачі 3 Лаби 01, але
   межі: `< 18` → дитина, `< 60` → дорослий, інакше → літній.
7. **`Age`, `IsAdult`, `FullName`** — обчислювані властивості (лише `get`), вони
   не зберігають значення, а рахують його щоразу з інших полів.
8. **У `Program.cs`** створіть щонайменше 5 пацієнтів: хоча б по одному кожним із
   трьох конструкторів. Виведіть кожного (спрацює ваш `ToString()`).

📖 Документація:
- [`static` члени](https://learn.microsoft.com/dotnet/csharp/programming-guide/classes-and-structs/static-classes-and-static-class-members)
- [Автоматичні властивості (get-only)](https://learn.microsoft.com/dotnet/csharp/programming-guide/classes-and-structs/auto-implemented-properties)
- [Конструктори та `: this()`](https://learn.microsoft.com/dotnet/csharp/programming-guide/classes-and-structs/constructors#constructor-syntax)
- [Оператори інкременту `++`](https://learn.microsoft.com/dotnet/csharp/language-reference/operators/arithmetic-operators#increment-operator-)
- [Структура `DateTime`](https://learn.microsoft.com/dotnet/api/system.datetime)
- [`override ToString()`](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/override)

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
git add ClinicApp/Patient.cs ClinicApp/Program.cs
git commit -m "Lab03 Task01"
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

1. **Години — прості `int` (0–23).** Окремий тип поки не потрібен: `8` означає
   08:00. У Лабі 04 ви якраз об'єднаєте ці два поля у `struct`.
2. **`WorkSchedule` — форматований рядок.** Щоб отримати `08` замість `8`,
   потрібне доповнення нулем — це стандартний числовий формат (`D2`). Зберіть
   рядок `"08:00–17:00"` із двох відформатованих годин.
3. **`IsAvailableNow`** порівнює *поточну* годину з діапазоном `[Start, End)`:
   більше-або-дорівнює початку **і** строго менше кінця. Поточну годину дає
   `DateTime.Now`.
4. **Значення за замовчуванням 8–17** виставляйте у повному конструкторі (решта
   конструкторів делегують йому через `: this(...)`).
5. **Графік можна міняти після створення** — тому `WorkStartHour`/`WorkEndHour`
   мають і `get`, і `set` (звичайні авто-властивості).
6. **`CanAcceptAt(int hour)`** — та сама перевірка діапазону, що в
   `IsAvailableNow`, але для довільної години. Не дублюйте логіку — нехай
   `IsAvailableNow` спирається на `CanAcceptAt`.
7. **`ToString()`** складіть з готових властивостей (`FullName`, `WorkSchedule`,
   `WorkingHoursPerDay`, `IsAvailableNow`) — не повторюйте форматування вручну.

📖 Документація:
- [Стандартні числові формати (`D2`)](https://learn.microsoft.com/dotnet/standard/base-types/standard-numeric-format-strings#decimal-format-specifier-d)
- [`DateTime.Now`](https://learn.microsoft.com/dotnet/api/system.datetime.now)
- [Обчислювані властивості (вираз-тіло)](https://learn.microsoft.com/dotnet/csharp/programming-guide/classes-and-structs/properties#expression-body-definitions)

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
git add ClinicApp/Doctor.cs ClinicApp/Program.cs
git commit -m "Lab03 Task02"
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
| `FindById(int)` | `public Patient?` | Лінійний пошук; повертає `null`, якщо не знайдено |
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

1. **Три приватні поля.** Константа-ліміт (`MaxPatients`), сам масив, створений
   на цей ліміт, і лічильник фактичної кількості `_count`. Це та сама схема
   «масив + `_count`», що в Задачах 3, 6, 8 Лаби 02 — тільки елементи тепер
   об'єкти `Patient`, а не числа.
2. **`Add`.** Спершу перевірте, чи є місце (`_count < MaxPatients`). Якщо ні —
   повідомте про ліміт і вийдіть. Якщо є — покладіть об'єкт у комірку `_count`
   і збільште лічильник.
3. **`FindById` — лінійний пошук.** Цикл по перших `_count` елементах, порівняння
   `Id`. Знайшли — одразу `return` знайдений об'єкт. Дійшли до кінця — `return null`
   (тому тип результату `Patient?`).
4. **`FindByName` — двопрохідний патерн із Лаби 02.** Перший прохід рахує,
   скільки елементів підходять; потім створюєте масив-результат точно цього
   розміру; другий прохід його заповнює. Порівняння без урахування регістру —
   приведіть обидва рядки до нижнього регістру перед перевіркою входження
   підрядка.
5. **`Remove` — видалення зі зсувом.** Знайдіть індекс потрібного елемента.
   Зсуньте всі наступні елементи на одну позицію ліворуч. Останню (тепер
   дубльовану) комірку очистіть, зменшіть `_count`. Поверніть `true`/`false`
   залежно від того, чи знайшли елемент.
6. **`DisplayStats` — один прохід.** Накопичуйте суму віків; відстежуйте
   *індекси* наймолодшого й найстаршого (а не самі об'єкти); рахуйте, скільки
   `IsAdult`. Середній вік = сума / `_count` (стежте за типом ділення — потрібне
   дробове).
7. **Порожній список.** `DisplayAll` і `DisplayStats` мають коректно
   відпрацьовувати, коли `_count == 0`.
8. **У `Program.cs`** винесіть підменю «Пацієнти» в окремий локальний метод —
   щоб `Program.cs` не перетворився на суцільний `switch`.

📖 Документація:
- [`const`](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/const)
- [Масиви](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/arrays)
- [`String.ToLower`](https://learn.microsoft.com/dotnet/api/system.string.tolower) / [`String.Contains`](https://learn.microsoft.com/dotnet/api/system.string.contains)
- [Nullable-типи посилань (`Patient?`)](https://learn.microsoft.com/dotnet/csharp/nullable-references)
- [Локальні функції](https://learn.microsoft.com/dotnet/csharp/programming-guide/classes-and-structs/local-functions)

### Адаптація до вашого домену

| Клініка | Готель | Ресторан | Університет | Прокат авто | Бібліотека | Спортзал |
|---------|--------|----------|-------------|-------------|------------|---------|
| `PatientManager` | `GuestManager` | `CustomerManager` | `StudentManager` | `ClientManager` | `ReaderManager` | `MemberManager` |
| `Patient[100]` | `Guest[200]` | `Customer[500]` | `Student[300]` | `Client[150]` | `Reader[200]` | `Member[250]` |
| `FindByName` | `FindByName` | `FindByName` | `FindByName` | `FindByName` | `FindByName` | `FindByName` |
| середній вік у статистиці | середня к-сть ночей | середня к-сть відвідин | середній бал | середній вік водія | середня к-сть книг | середній вік учасника |

### Коміт

```bash
git add ClinicApp/PatientManager.cs ClinicApp/Program.cs
git commit -m "Lab03 Task03"
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
| `FindById(int)` | `public Doctor?` | Лінійний пошук; `null`, якщо не знайдено |
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

1. **`DoctorManager` — копія `PatientManager` за структурою.** Той самий масив +
   `_count`, ті самі `Add` / `FindById` / `Remove` / `DisplayAll`. Відрізняються
   лише `FindBySpeciality` і `DisplayStats`.
2. **`FindBySpeciality`** — той самий двопрохідний патерн, що `FindByName`, лише
   порівняння йде по полю `Speciality` (без урахування регістру).
3. **`GetAll()`** повертає *копію* — новий масив рівно на `_count` елементів. Це
   потрібно, щоб зовнішній код не міг зіпсувати внутрішній масив менеджера.
4. **Унікальні спеціальності без `Dictionary`.** Для кожного лікаря `i`
   перевірте вкладеним циклом по попередніх (`j < i`), чи така спеціальність вже
   траплялась. Якщо ні — це нова спеціальність; порахуйте окремим проходом,
   скільки всього лікарів її мають, і виведіть рядок. Так виходить три вкладені
   цикли — це нормально для навчальної лаби (у Лабі 09 те саме зробить
   `Dictionary` в один прохід).
5. **Кількість доступних зараз** — один прохід із лічильником по `IsAvailableNow`.
6. **Ввід години з консолі** — розбирайте рядок методом, який повертає ознаку
   успіху й не кидає виняток на некоректному тексті (`int.TryParse`). Повноцінно
   «свої» методи з `out` ви писатимете в Лабі 04.

📖 Документація:
- [`Array.Copy`](https://learn.microsoft.com/dotnet/api/system.array.copy)
- [`int.TryParse`](https://learn.microsoft.com/dotnet/api/system.int32.tryparse)
- [Вкладені цикли](https://learn.microsoft.com/dotnet/csharp/language-reference/statements/iteration-statements#the-for-statement)

### Адаптація до вашого домену

| Клініка | Готель | Ресторан | Університет | Прокат авто | Бібліотека | Спортзал |
|---------|--------|----------|-------------|-------------|------------|---------|
| `DoctorManager` | `StaffManager` | `WaiterManager` | `LecturerManager` | `ManagerList` | `LibrarianManager` | `TrainerManager` |
| `FindBySpeciality` | `FindByDepartment` | `FindBySection` | `FindBySubject` | `FindByCarClass` | `FindBySection` | `FindBySpecialty` |
| Унікальні спеціальності | По відділах | По залах | По кафедрах | По класах авто | По відділах | По спеціалізаціях |
| «Доступні зараз» | «На зміні» | «На зміні» | «Читають зараз» | «На роботі» | «На зміні» | «Доступні зараз» |

### Коміт

```bash
git add ClinicApp/DoctorManager.cs ClinicApp/Program.cs
git commit -m "Lab03 Task04"
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

1. **Кінцевий автомат статусу.** Дозволені лише переходи
   `Scheduled → Cancelled` і `Scheduled → Completed`, і кожен — лише один раз.
   Тому `Status` можна *читати* ззовні, але *змінювати* — тільки зсередини
   класу. Це властивість із `private set` (шукайте «private setter» у
   документації про властивості).
2. **`Cancel` і `Complete` — вартові переходу.** Обидва спершу перевіряють, що
   поточний статус досі `Scheduled`. Якщо ні — нічого не змінюють і повертають
   `false`. Якщо так — змінюють статус і повертають `true`.
3. **Навіщо повертати `bool`.** Викликач у `Program.cs` за результатом вирішує,
   що показати користувачу («скасовано» проти «вже завершено»).
4. **`reason` у `Cancel`** — необов'язковий параметр зі значенням за
   замовчуванням (порожній рядок). Якщо причину передали — запишіть її в `Notes`.
5. **`Notes` — звичайний рядок, не nullable.** Ініціалізуйте його `""` у
   конструкторі; у `ToString()` додавайте до виводу лише коли він непорожній
   (`Notes.Length > 0`).
6. **`DurationMinutes` за замовчуванням 30** — необов'язковий параметр
   конструктора.
7. **`ToString()` навмисно показує `#PatientId` і `#DoctorId`, а не імена.**
   Клас не знає про менеджери й не має доступу до об'єктів `Patient`/`Doctor`.
   Цю незручність ви приберете в Задачі 6.

📖 Документація:
- [Властивості: `private set`](https://learn.microsoft.com/dotnet/csharp/programming-guide/classes-and-structs/properties#init-only-and-private-set)
- [Необов'язкові аргументи](https://learn.microsoft.com/dotnet/csharp/programming-guide/classes-and-structs/named-and-optional-arguments#optional-arguments)
- [Кінцевий автомат (State machine) — загальна ідея](https://uk.wikipedia.org/wiki/Скінченний_автомат)

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
git add ClinicApp/Appointment.cs ClinicApp/Program.cs
git commit -m "Lab03 Task05"
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

1. **Конструктор зберігає *посилання* на менеджери, а не копіює їх.** Поля
   `_patients` і `_doctors` вказують на ті самі об'єкти, з якими працює решта
   програми, — тому менеджер записів завжди бачить актуальні списки. Це і є суть
   «отримати залежність через конструктор».
2. **`Book` спершу валідує ID.** Через `_patients.FindById` і `_doctors.FindById`
   перевірте, що обидва існують (результат не `null`). Якщо ні — повідомте й
   поверніть `false`, запис не створюйте. Якщо все гаразд — створіть `Appointment`
   і покладіть у масив.
3. **`DisplayAppointment` перетворює ID на імена.** Знайдіть `Patient` і `Doctor`
   за їхніми ID; якщо знайдено — беріть `FullName`, якщо ні (запис на видаленого) —
   виводьте запасне `"Пацієнт #<id>"`. Перевірку на `null` робіть явно через
   `== null` / `!= null`.
4. **`GetByDate`** порівнює лише *дату*, ігноруючи час: у `DateTime` для цього є
   властивість, що відкидає час доби.
5. **`GetByPatient`, `GetByDoctor`, `GetByDate`, `GetUpcoming`** — усі той самий
   двопрохідний патерн (порахувати → виділити масив → заповнити), лише умова
   фільтра різна.
6. **Приватний `FindById` всередині менеджера** прибирає дублювання в `Cancel`
   і `Complete`. Він теж повертає `Appointment?` (може не знайти).
7. **У підменю «Записи»** перед запитом ID показуйте список пацієнтів і список
   лікарів — щоб користувач бачив доступні номери.

📖 Документація:
- [Передача залежностей через конструктор (Dependency Injection — базова ідея)](https://learn.microsoft.com/dotnet/core/extensions/dependency-injection)
- [Значення й посилальні типи](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/reference-types)
- [`DateTime.Date`](https://learn.microsoft.com/dotnet/api/system.datetime.date)

### Адаптація до вашого домену

| Клініка | Готель | Ресторан | Університет | Прокат авто | Бібліотека | Спортзал |
|---------|--------|----------|-------------|-------------|------------|---------|
| `AppointmentManager` | `BookingManager` | `ReservationManager` | `EnrollmentManager` | `RentalManager` | `LoanManager` | `SessionManager` |
| `Book(patientId, doctorId, ...)` | `Book(guestId, roomId, ...)` | `Reserve(custId, tableId, ...)` | `Enroll(studentId, courseId, ...)` | `Rent(clientId, carId, ...)` | `Lend(readerId, bookId, ...)` | `Book(memberId, trainerId, ...)` |
| `GetByPatient` | `GetByGuest` | `GetByCustomer` | `GetByStudent` | `GetByClient` | `GetByReader` | `GetByMember` |
| `GetByDoctor` | `GetByRoom` | `GetByTable` | `GetByCourse` | `GetByCar` | `GetByBook` | `GetByTrainer` |

### Коміт

```bash
git add ClinicApp/AppointmentManager.cs ClinicApp/Program.cs
git commit -m "Lab03 Task06"
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

1. **Порядок створення в конструкторі важливий.** `AppointmentManager` вимагає
   вже готових `PatientManager` і `DoctorManager` (див. Задачу 6), тому спершу
   створіть їх, а потім передайте в конструктор менеджера записів.
2. **`Clinic` майже нічого не робить сам** — він делегує. `DisplaySchedule`
   бере записи на дату в `Appointments` і виводить їх наявним методом
   `DisplayList`.
3. **`GenerateReport` — навантаження лікарів без LINQ.** Візьміть майбутні записи
   один раз (`GetUpcoming`) і всіх лікарів (`GetAll`). Для кожного лікаря
   вкладеним циклом порахуйте, скільки записів мають його `Id`, і виведіть рядок.
4. **Символи рамок** для звіту: `╔ ╗ ╚ ╝ ╠ ╣ ║ ═` — скопіюйте потрібні.
5. **`Program.cs` після цієї задачі** працює лише через один об'єкт `clinic`.
   Кожне підменю приймає `Clinic` параметром і звертається до `clinic.Patients`,
   `clinic.Doctors`, `clinic.Appointments`.
6. **Публічні властивості-менеджери** (`Patients`, `Doctors`, `Appointments`) —
   get-only: створюються в конструкторі й далі не переприсвоюються.

📖 Документація:
- [Порядок ініціалізації в конструкторі](https://learn.microsoft.com/dotnet/csharp/programming-guide/classes-and-structs/constructors)
- [Композиція об'єктів («має-а»)](https://learn.microsoft.com/dotnet/csharp/fundamentals/object-oriented/objects)
- [Символи псевдографіки (box-drawing) — Wikipedia](https://uk.wikipedia.org/wiki/Символи_для_малювання_рамок)

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
git add ClinicApp/Clinic.cs ClinicApp/Program.cs
git commit -m "Lab03 Task07"
```

---

## Задача 8. Зростаючий масив ⭐⭐⭐⭐

### Умова

`PatientManager` має жорсткий ліміт — `const int MaxPatients = 100`. Що станеться,
коли клініка виросте до 200 пацієнтів? А якщо поставити ліміт `100000` — програма
завжди триматиме в пам'яті масив на 100000 комірок, навіть коли пацієнтів п'ятеро.

**Ваше завдання:** реалізувати `GrowablePatientManager` — менеджер, у якому масив
**сам збільшується**, коли заповнюється, і жодного наперед заданого ліміту немає.

Алгоритм зростання (лише масиви й цикли, нічого нового): коли масив заповнений
(`_count` дорівнює довжині масиву), створіть **новий масив удвічі більший**,
циклом скопіюйте в нього всі елементи зі старого й далі працюйте з новим.

### Специфікація класу

| Член класу | Тип | Опис |
|------------|-----|------|
| `_patients` | `private Patient[]` | Внутрішній масив; початкова довжина — 4 |
| `_count` | `private int` | Поточна кількість пацієнтів |
| `Count` | `public int` | `_count` |
| `Capacity` | `public int` (get-only) | Поточна довжина масиву `_patients` |
| `Add(Patient)` | `public void` | Якщо масив повний — спершу збільшити, потім додати |
| `FindById(int)` | `public Patient?` | Лінійний пошук; `null`, якщо не знайдено |
| `Remove(int id)` | `public bool` | Видаляє зі зсувом ліворуч |
| `DisplayAll()` | `public void` | Список пацієнтів + поточна ємність |

### Приклад виводу

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

1. **Приватний метод «збільшити».** Він рахує нову довжину (удвічі більшу за
   поточну), створює новий масив, циклом переносить у нього перші `_count`
   елементів, і робить його новим внутрішнім масивом. Тут же виведіть рядок
   «Розширення: X → Y».
2. **`Add`.** Перед додаванням перевірте, чи масив заповнений (`_count` дорівнює
   довжині масиву). Якщо так — спершу викличте метод збільшення. Потім покладіть
   елемент у комірку `_count` і збільште лічильник.
3. **Малий початковий розмір (4)** — навмисно: на 20 пацієнтах ви побачите
   кілька розширень поспіль (4 → 8 → 16 → 32) і зрозумієте закономірність.
4. **Стару копію масиву чіпати не треба.** Щойно поле вказало на новий масив,
   старий стає нікому не потрібен — середовище саме звільнить пам'ять
   (збирач сміття).
5. **`Capacity`** — обчислювана властивість, що просто повертає довжину
   внутрішнього масиву.
6. **Тест у `Program.cs`:** у циклі додайте 20 пацієнтів, після кожного
   виводьте `Count` і `Capacity`. Потім перевірте `FindById` на наявному та
   відсутньому ID.
7. **Питання на подумати (на папері):** якщо додати 16 пацієнтів, починаючи з
   ємності 4, скільки разів загалом буде скопійовано *окремий* елемент? Чому
   подвоєння вигідніше, ніж «додавати по 1 комірці щоразу»?

> Ви щойно вручну реалізували те, що надалі в курсі роблять готові динамічні
> колекції. Коли курс дійде до них — ви вже знатимете, що всередині просто масив,
> який подвоюється.

📖 Документація:
- [Масиви](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/arrays)
- [`Array.Resize` (як це виглядає «з коробки»)](https://learn.microsoft.com/dotnet/api/system.array.resize)
- [Керування пам'яттю та збирач сміття (базово)](https://learn.microsoft.com/dotnet/standard/garbage-collection/fundamentals)
- [Амортизована складність (динамічний масив) — Wikipedia](https://uk.wikipedia.org/wiki/Динамічний_масив)

### Адаптація до вашого домену

| Клініка | Готель | Ресторан | Університет | Прокат авто | Бібліотека | Спортзал |
|---------|--------|----------|-------------|-------------|------------|---------|
| `GrowablePatientManager` | `GrowableGuestManager` | `GrowableCustomerManager` | `GrowableStudentManager` | `GrowableClientManager` | `GrowableReaderManager` | `GrowableMemberManager` |
| Початковий розмір 4 | Початковий розмір 4 | Початковий розмір 4 | Початковий розмір 4 | Початковий розмір 4 | Початковий розмір 4 | Початковий розмір 4 |
| Пацієнтів / ємність | Гостей / ємність | Відвідувачів / ємність | Студентів / ємність | Клієнтів / ємність | Читачів / ємність | Учасників / ємність |

### Коміт

```bash
git add ClinicApp/GrowablePatientManager.cs ClinicApp/Program.cs
git commit -m "Lab03 Task08"
```

---

## Перевірка перед здачею

```bash
dotnet build ClinicApp
dotnet run --project ClinicApp
```

Переконайтесь, що:

- [ ] Проєкт компілюється **без помилок і без попереджень**
- [ ] Кожен пацієнт має унікальний Id (1, 2, 3, …)
- [ ] Вік розраховується правильно (з урахуванням дня народження)
- [ ] `Cancel` повертає `false`, якщо запис уже скасовано або завершено
- [ ] `Book` виводить помилку при неіснуючому ID пацієнта чи лікаря
- [ ] `FindByName` знаходить за частиною імені (без урахування регістру)
- [ ] `DisplayStats` рахує коректні дані (середнє, мін, макс, кількість дорослих)
- [ ] `DisplayAll` / `DisplayStats` не падають на порожньому списку
- [ ] Усі три підменю (Пацієнти, Лікарі, Записи) працюють через об'єкт `clinic`
- [ ] `GenerateReport()` виводить правильну кількість майбутніх записів
- [ ] `GrowablePatientManager` розширюється при заповненні й зберігає всі елементи
- [ ] у коді немає `List<T>`, `Dictionary<,>`, LINQ, `?.`, `??`, `enum`, `struct`

---

## Питання для самоперевірки

1. `_nextId` — `static`, `Id` — ні. Що зламається, якщо зробити `_nextId`
   звичайним полем екземпляра?
2. Конструктор `Patient(firstName, lastName)` викликає `: this(...)`. Що погано
   станеться з нумерацією `Id`, якщо він натомість сам присвоюватиме всі поля?
3. `Status` в `Appointment` має `private set`. Наведіть рядок коду, який став би
   можливий без цього обмеження і зламав би кінцевий автомат.
4. Чому `AppointmentManager` *отримує* менеджери в конструкторі, а не створює
   їх сам через `new`? Що було б, якби створював?
5. Скільки разів буде скопійовано окремий елемент, якщо додати 16 пацієнтів у
   `GrowablePatientManager` з початковою ємністю 4?
6. `FindById` повертає `Patient?`. Чому тип із `?`, і що зобов'язаний зробити
   викличок перед тим, як звертатись до властивостей результату?
7. `DisplayStats` у `DoctorManager` рахує унікальні спеціальності трьома
   вкладеними циклами. Яка приблизно складність цього щодо кількості лікарів N?

---

## Статус гілки

Це **перша лаба, що зливається в `main`**. Після всіх 8 завдань (кожне — окремий
коміт `Lab03 TaskNN` на гілці `Lab-03`):

```bash
git push -u origin Lab-03          # гілка на GitHub
git checkout main
git merge --no-ff Lab-03 -m "Merge Lab-03: Defining Classes"
git push
```

`--no-ff` створює явний merge-коміт — у графі історії видно межі лаби.

> Наступна лаба: `git checkout main` → `git checkout -b Lab-04`. Проєкт той самий
> (`ClinicApp/`), гілка знову зливається в `main`.
