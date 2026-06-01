# Лаба 10 — Iterators & Comparators (Ітератори і компаратори)

## Мета

Навчитись реалізовувати `IComparable<T>` і `IComparer<T>` для управління порядком сортування об'єктів, а також `IEnumerable<T>` з `yield return` для лінивої генерації послідовностей. Побудувати аналітичний модуль клініки, де ці концепції мають природній практичний сенс.

## Контекст

Система вже накопичує дані: пацієнти, лікарі, записи на прийом. Але жодного способу отримати відповідь на питання "хто з лікарів прийняв найбільше пацієнтів?" або "хто з пацієнтів витратив найбільше?" — немає. Це задача аналітики: обчислити статистику по кожному об'єкту і відсортувати за різними критеріями.

Порівняння і сортування в C# будуються на двох інтерфейсах:
- `IComparable<T>` — **природній порядок**: клас сам знає як порівнювати себе з іншим. Один порядок, вбудований в тип.
- `IComparer<T>` — **зовнішній компаратор**: окремий клас реалізує один критерій сортування. Таких компараторів можна мати скільки завгодно.

Генерація даних для аналітики природньо виражається через `IEnumerable<T>` з `yield return` — ліниве обчислення статистики для кожного об'єкта по черзі, замість того щоб спочатку побудувати весь масив у пам'яті.

## Гілка

```bash
git checkout main
git pull
git checkout -b feature/iterators
```

---

## Завдання 1 — `DoctorStats` і `IComparable<T>`: природній порядок ⭐⭐

### Умова

Клініці потрібен об'єкт що представляє аналітичний знімок по лікарю: скільки прийомів провів, яка загальна виручка, коли був останній прийом. Цей об'єкт повинен вміти порівнювати себе з іншим таким об'єктом — щоб масив `DoctorStats[]` можна було відсортувати одним викликом `Array.Sort()`.

### Що реалізувати

Створи клас `DoctorStats` у `src/Models/DoctorStats.cs`.

**Властивості (тільки читання):**
- `int DoctorId` — ID лікаря
- `string FullName` — повне ім'я
- `int AppointmentCount` — загальна кількість прийомів
- `decimal TotalRevenue` — сума `GetCost()` по всіх прийомах
- `DateTime LastAppointmentDate` — дата останнього прийому (`DateTime.MinValue` якщо прийомів немає)

**Конструктор:** отримує всі п'ять значень, присвоює властивостям.

**`IComparable<DoctorStats>`:** реалізуй інтерфейс — визнач природній порядок: більша кількість прийомів = вища позиція в рейтингу. Тобто при сортуванні за зростанням (`Array.Sort()`) перший — той у кого найбільше прийомів.

**`override ToString()`:** один рядок — ID, ім'я, кількість прийомів, виручка, дата останнього прийому.

### Підказки

1. `IComparable<T>` вимагає один метод: `int CompareTo(T? other)`. Повертає від'ємне число якщо `this` йде перед `other`, нуль якщо рівні, додатнє якщо `this` йде після.

2. Щоб більша кількість прийомів опинилась першою (тобто сортування за спаданням через `Sort()` за зростанням), порівнюй навпаки: `return other.AppointmentCount.CompareTo(this.AppointmentCount)`. Перевір цю логіку на папері з прикладом: лікар А = 5 прийомів, лікар Б = 2 прийоми — після `Sort()` А повинен бути першим.

3. `DateTime.MinValue` — константа "найраніша можлива дата" в C#. Зручно як маркер "прийомів не було".

4. Конструктор `DoctorStats` не рахує статистику сам — він лише зберігає готові значення. Підрахунок буде в `AnalyticsManager` (Завдання 4).

📖 [IComparable\<T\> — Microsoft Docs](https://learn.microsoft.com/en-us/dotnet/api/system.icomparable-1)  
📖 [Array.Sort — Microsoft Docs](https://learn.microsoft.com/en-us/dotnet/api/system.array.sort)

### Що перевірити

Створи вручну кілька `DoctorStats` з різною кількістю прийомів, помісти в масив `DoctorStats[]`, виклич `Array.Sort()` — переконайся що лікар з найбільшою кількістю прийомів стоїть першим.

### Адаптація до вашого домену

| Клініка | Готель | Ресторан | Університет | Прокат авто | Бібліотека | Спортзал |
|---------|--------|----------|-------------|-------------|------------|---------|
| `DoctorStats` | `StaffStats` | `WaiterStats` | `LecturerStats` | `ManagerStats` | `LibrarianStats` | `TrainerStats` |
| `AppointmentCount` | к-сть check-in | к-сть столів | к-сть курсів | к-сть оренд | к-сть видач | к-сть сесій |
| `TotalRevenue` | загальна виручка | загальна виручка | к-сть студентів | загальна виручка | к-сть повернень | загальна виручка |
| більше прийомів = вище | більше обслугованих = вище | більше столів = вище | більше курсів = вище | більше оренд = вище | більше видач = вище | більше сесій = вище |

### Коміт

```bash
git add src/Models/DoctorStats.cs
git commit -m "Lab10 Task1: add DoctorStats with IComparable<DoctorStats> by appointment count"
```

---

## Завдання 2 — `PatientStats` і `IComparable<PatientStats>` ⭐⭐

### Умова

За аналогією з `DoctorStats` — статистичний об'єкт для пацієнта: скільки візитів, скільки витрачено, дата останнього візиту. Природній порядок — за кількістю візитів (найактивніший пацієнт — перший).

### Що реалізувати

Створи клас `PatientStats` у `src/Models/PatientStats.cs`.

**Властивості:** `int PatientId`, `string FullName`, `int VisitCount`, `decimal TotalSpent`, `DateTime LastVisitDate`.

**`IComparable<PatientStats>`:** природній порядок — більша кількість візитів = вища позиція.

**`override ToString()`:** один рядок з усіма даними.

### Підказки

1. Структура ідентична `DoctorStats` — той самий патерн, різні поля і логіка порівняння. Одна з цілей цього завдання — закріпити патерн на другому прикладі.

2. Зверни увагу на `LastVisitDate == DateTime.MinValue` у `ToString()` — якщо прийомів не було, виводь щось читабельне замість `01.01.0001`.

### Що перевірити

Виклич `Array.Sort()` на масиві `PatientStats[]` — пацієнт з найбільшою кількістю візитів повинен бути першим.

### Адаптація до вашого домену

| Клініка | Готель | Ресторан | Університет | Прокат авто | Бібліотека | Спортзал |
|---------|--------|----------|-------------|-------------|------------|---------|
| `PatientStats` | `GuestStats` | `CustomerStats` | `StudentStats` | `ClientStats` | `ReaderStats` | `MemberStats` |
| `VisitCount` | к-сть ночей | к-сть відвідин | к-сть курсів | к-сть оренд | к-сть видач | к-сть тренувань |
| `TotalSpent` | загальна сума | загальна сума | середній бал | загальна сума | штрафи сплачені | загальна сума |
| більше візитів = вище | більше ночей = вище | більше відвідин = вище | більше курсів = вище | більше оренд = вище | більше видач = вище | більше тренувань = вище |

### Коміт

```bash
git add src/Models/PatientStats.cs
git commit -m "Lab10 Task2: add PatientStats with IComparable<PatientStats> by visit count"
```

---

## Завдання 3 — `IComparer<T>`: множинні критерії сортування ⭐⭐⭐

### Умова

`IComparable<T>` дає один фіксований порядок. Але аналітичний модуль потребує кілька: лікарів можна ранжувати за навантаженням, за виручкою, за алфавітом. Для цього є `IComparer<T>` — окремий клас що реалізує один критерій і передається в `List<T>.Sort(comparer)`.

### Що реалізувати

Створи папку `src/Comparators/`. В ній — чотири класи:

**`DoctorStatsByRevenue`** — реалізує `IComparer<DoctorStats>`. Сортує за `TotalRevenue` за спаданням (більша виручка = вище).

**`DoctorStatsByName`** — реалізує `IComparer<DoctorStats>`. Сортує за `FullName` за зростанням (А → Я). Для порівняння рядків використовуй `string.Compare(x, y, StringComparison.CurrentCulture)`.

**`PatientStatsBySpent`** — реалізує `IComparer<PatientStats>`. Сортує за `TotalSpent` за спаданням.

**`PatientStatsByLastVisit`** — реалізує `IComparer<PatientStats>`. Сортує за `LastVisitDate` за спаданням (найновіший візит = вище). Для порівняння дат: `DateTime` реалізує `IComparable`, тому `y.LastVisitDate.CompareTo(x.LastVisitDate)` дає спадний порядок.

### Підказки

1. `IComparer<T>` вимагає один метод: `int Compare(T? x, T? y)`. Та сама семантика що й `CompareTo` — від'ємне якщо `x` перед `y`, нуль якщо рівні, додатнє якщо `x` після `y`.

2. Обробляй `null` явно: якщо `x == null && y == null` → `0`; якщо тільки `x == null` → `-1`; якщо тільки `y == null` → `1`. Компілятор вимагає цього бо тип може бути nullable.

3. Щоб отримати спадний порядок з `CompareTo`, просто міняй місцями `x` і `y`: `return y.TotalRevenue.CompareTo(x.TotalRevenue)` дає спадний замість зростаючого.

4. Використання: `list.Sort(new DoctorStatsByRevenue())` — `List<T>.Sort()` приймає `IComparer<T>` як аргумент.

📖 [IComparer\<T\> — Microsoft Docs](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.icomparer-1)  
📖 [List\<T\>.Sort(IComparer) — Microsoft Docs](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.list-1.sort)

### Що перевірити

Зроби один список `DoctorStats`, відсортуй його чотирма різними способами і виведи — переконайся що порядок щоразу різний:
- `.Sort()` — за кількістю прийомів (IComparable)
- `.Sort(new DoctorStatsByRevenue())` — за виручкою
- `.Sort(new DoctorStatsByName())` — за ім'ям

### Адаптація до вашого домену

| Клініка | Готель | Ресторан | Університет | Прокат авто | Бібліотека | Спортзал |
|---------|--------|----------|-------------|-------------|------------|---------|
| `DoctorStatsByRevenue` | `StaffStatsByRevenue` | `WaiterStatsByRevenue` | `LecturerStatsByStudents` | `ManagerStatsByRevenue` | `LibrarianStatsByIssues` | `TrainerStatsByRevenue` |
| `DoctorStatsByName` | `StaffStatsByName` | `WaiterStatsByName` | `LecturerStatsByName` | `ManagerStatsByName` | `LibrarianStatsByName` | `TrainerStatsByName` |
| `PatientStatsBySpent` | `GuestStatsBySpent` | `CustomerStatsBySpent` | `StudentStatsByGrade` | `ClientStatsBySpent` | `ReaderStatsByIssues` | `MemberStatsBySpent` |
| `PatientStatsByLastVisit` | `GuestStatsByLastStay` | `CustomerStatsByLastVisit` | `StudentStatsByLastCourse` | `ClientStatsByLastRental` | `ReaderStatsByLastLoan` | `MemberStatsByLastSession` |

### Коміт

```bash
git add src/Comparators/
git commit -m "Lab10 Task3: add IComparer<T> comparators for DoctorStats and PatientStats"
```

---

## Завдання 4 — `AnalyticsManager` і `yield return`: ліниве обчислення ⭐⭐⭐

### Умова

Де і як обчислювати статистику? Можна зробити метод що будує весь масив `DoctorStats[]` одразу і повертає його. Але є кращий підхід: `IEnumerable<T>` з `yield return` — метод обчислює статистику для кожного лікаря по черзі і **одразу повертає** результат, не накопичуючи весь масив.

Це важливо коли лікарів тисячі — ти можеш зупинитись після першого десятка і решту не обчислювати взагалі.

### Що реалізувати

Створи клас `AnalyticsManager` у `src/Managers/AnalyticsManager.cs`.

**Залежності через конструктор:**
```
AnalyticsManager(AppointmentManager appointments, DoctorManager doctors, PatientManager patients)
```

**Метод `IEnumerable<DoctorStats> ComputeDoctorStats()`:**
- Отримай всіх лікарів через `_doctors.GetAll()` і всі прийоми через `_appointments.GetAll()`
- Для кожного лікаря (цикл `for`) — пройди по всіх прийомах і знайди його прийоми: порахуй кількість, суму `GetCost()`, знайди максимальну дату
- Замість `return new DoctorStats(...)` — використовуй **`yield return new DoctorStats(...)`**
- Метод повертає `IEnumerable<DoctorStats>`, тобто компілятор перетворює його на state machine

**Метод `IEnumerable<PatientStats> ComputePatientStats()`:** аналогічно для пацієнтів.

**Оновлення `Clinic.cs`:** додай `public AnalyticsManager Analytics { get; }` і ініціалізуй у конструкторі: `Analytics = new AnalyticsManager(Appointments, Doctors, Patients)`.

### Підказки

1. `yield return` в методі з типом повернення `IEnumerable<T>` перетворює метод на **ітератор**. Після кожного `yield return` виконання методу "призупиняється" і відновлюється при наступному запиті елемента. Ось чому це "ліниво" — наступний `DoctorStats` не обчислюється поки його не запросили.

2. Щоб переконатись в ліниві: поклади `Console.WriteLine("обчислюю " + doctors[i].FullName)` перед `yield return`. При `foreach` по результату побачиш що рядки виводяться по одному під час ітерації, а не всі одразу на початку.

3. Щоб зібрати результати в `List<DoctorStats>`:
   ```csharp
   List<DoctorStats> list = new List<DoctorStats>();
   foreach (DoctorStats s in analytics.ComputeDoctorStats())
       list.Add(s);
   ```
   Тут `foreach` споживає ітератор по одному елементу.

4. Максимальна дата: починай з `DateTime.MinValue` і оновлюй коли `appointments[j].ScheduledAt > lastDate`.

📖 [yield return — Microsoft Docs](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/statements/yield)  
📖 [IEnumerable\<T\> — Microsoft Docs](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ienumerable-1)  
📖 [Iterators — C# Guide](https://learn.microsoft.com/en-us/dotnet/csharp/iterators)

### Що перевірити

- `ComputeDoctorStats()` повертає правильну кількість елементів (рівно стільки скільки лікарів)
- Для лікаря без прийомів: `AppointmentCount == 0`, `TotalRevenue == 0`, `LastAppointmentDate == DateTime.MinValue`
- `foreach` по результату без збереження в список — також працює

### Адаптація до вашого домену

| Клініка | Готель | Ресторан | Університет | Прокат авто | Бібліотека | Спортзал |
|---------|--------|----------|-------------|-------------|------------|---------|
| `AnalyticsManager` | `AnalyticsManager` | `AnalyticsManager` | `AnalyticsManager` | `AnalyticsManager` | `AnalyticsManager` | `AnalyticsManager` |
| `ComputeDoctorStats()` | `ComputeStaffStats()` | `ComputeWaiterStats()` | `ComputeLecturerStats()` | `ComputeManagerStats()` | `ComputeLibrarianStats()` | `ComputeTrainerStats()` |
| `ComputePatientStats()` | `ComputeGuestStats()` | `ComputeCustomerStats()` | `ComputeStudentStats()` | `ComputeClientStats()` | `ComputeReaderStats()` | `ComputeMemberStats()` |
| `yield return` по кожному лікарю | по кожному співробітнику | по кожному офіціанту | по кожному викладачу | по кожному менеджеру | по кожному бібліотекарю | по кожному тренеру |

### Коміт

```bash
git add src/Managers/AnalyticsManager.cs src/Clinic.cs
git commit -m "Lab10 Task4: add AnalyticsManager with yield return for IEnumerable<DoctorStats/PatientStats>"
```

---

## Завдання 5 — Меню аналітики: все разом ⭐⭐⭐

### Умова

`DoctorStats`, `PatientStats`, компаратори, `AnalyticsManager` — все готово. Тепер підключи це до меню. Новий пункт **"8. Аналітика"** з п'ятьма варіантами звітів.

### Що реалізувати

**`Program.cs`** — додай "8. Аналітика" до головного меню і функцію `AnalyticsMenu(Clinic clinic)`.

Підменю з варіантами:

1. **Лікарі за навантаженням** — `ComputeDoctorStats()` → зібрати в `List<DoctorStats>` → `.Sort()` (використовує `IComparable`) → вивести
2. **Лікарі за виручкою** — `.Sort(new DoctorStatsByRevenue())`
3. **Лікарі за іменем** — `.Sort(new DoctorStatsByName())`
4. **Пацієнти за кількістю візитів** — `ComputePatientStats()` → `.Sort()` (IComparable)
5. **Пацієнти за витратами** — `.Sort(new PatientStatsBySpent())`

Два допоміжних методи для збору результатів:
- `CollectDoctorStats(Clinic clinic)` → збирає `IEnumerable<DoctorStats>` у `List<DoctorStats>` через `foreach`
- `CollectPatientStats(Clinic clinic)` → аналогічно

### Підказки

1. Виклик `clinic.Analytics.ComputeDoctorStats()` повертає `IEnumerable<DoctorStats>` — ітератор, не список. Щоб сортувати, потрібен `List<DoctorStats>`. Збирай через `foreach` + `.Add()`.

2. Один і той самий `CollectDoctorStats()` можна викликати для кожного пункту меню — метод щоразу починає ітерацію заново.

3. При сортуванні зверни увагу: `.Sort()` без аргументу вимагає що тип реалізує `IComparable<T>`. `.Sort(comparer)` з аргументом — використовує переданий компаратор. Обидва методи існують на `List<T>`.

4. `using ClinicApp.Comparators;` — не забудь додати у верхній частині `Program.cs`.

📖 [List\<T\>.Sort() — Microsoft Docs](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.list-1.sort)

### Що перевірити

Запусти `dotnet run`. Відкрий "8. Аналітика". Перевір кожен з п'яти пунктів:
- Лікарі виводяться у правильному порядку для кожного критерію
- Після сортування за навантаженням і за виручкою порядок різний (якщо тестові дані різноманітні)
- Пацієнти без жодного запису виводяться з `Візитів: 0` і датою `—`

### Адаптація до вашого домену

| Клініка | Готель | Ресторан | Університет | Прокат авто | Бібліотека | Спортзал |
|---------|--------|----------|-------------|-------------|------------|---------|
| `8. Аналітика` | `8. Аналітика` | `8. Аналітика` | `8. Аналітика` | `8. Аналітика` | `8. Аналітика` | `8. Аналітика` |
| Лікарі за навантаженням | Персонал за check-in | Офіціанти за столами | Викладачі за курсами | Менеджери за орендами | Бібліотекарі за видачами | Тренери за сесіями |
| Лікарі за виручкою | Персонал за виручкою | Офіціанти за виручкою | Викладачі за студентами | Менеджери за виручкою | Бібліотекарі за відділами | Тренери за виручкою |
| Пацієнти за кількістю візитів | Гості за ночами | Клієнти за відвідинами | Студенти за курсами | Клієнти за орендами | Читачі за видачами | Учасники за тренуваннями |
| Пацієнти за витратами | Гості за витратами | Клієнти за витратами | Студенти за балом | Клієнти за витратами | Читачі за штрафами | Учасники за витратами |

### Коміт

```bash
git add src/Program.cs
git commit -m "Lab10 Task5: add Analytics menu item 8 with sort options for doctors and patients"
```

---

## Перевірка перед здачею

```bash
cd src
dotnet build
dotnet run
```

- [ ] `8. Аналітика` з'явилась у головному меню
- [ ] "Лікарі за навантаженням" і "Лікарі за виручкою" дають **різний** порядок
- [ ] "Лікарі за іменем" дає алфавітний порядок
- [ ] Пацієнт без записів показує `Візитів: 0` і дату `—`
- [ ] Видалення `IComparable` з `DoctorStats` призводить до помилки компіляції при `.Sort()` без аргументу
- [ ] `DoctorStatsByRevenue` — окремий клас, реалізує `IComparer<DoctorStats>`

---

## Питання для самоперевірки

1. Чим `IComparable<T>` відрізняється від `IComparer<T>`? Коли використовувати перше, коли друге?
2. Що повертає `CompareTo` при рівних значеннях? Що станеться якщо завжди повертати `0`?
3. Чому `yield return` у `ComputeDoctorStats()` дає "ліниве" обчислення? Коли саме виконується тіло циклу?
4. Як отримати з `IEnumerable<T>` лише перші N елементів без LINQ? (підказка: `foreach` + лічильник)
5. Що станеться якщо викликати `.Sort()` на `List<DoctorStats>` після того як видалиш `IComparable<DoctorStats>` з класу?
6. Порівняй: `ComputeDoctorStats()` з `yield return` vs метод що будує і повертає `DoctorStats[]`. В чому різниця у поведінці при великій кількості лікарів?

---

## Злиття

```bash
git checkout main
git merge --no-ff feature/iterators -m "Merge feature/iterators: Lab10 — DoctorStats, PatientStats, IComparable, IComparer, AnalyticsManager"
git push
```

> Наступна лаба: `git checkout -b feature/reflection` — атрибути і рефлексія.
