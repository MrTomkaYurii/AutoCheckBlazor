# Лаба 08 — Polymorphism (Поліморфізм)

## Мета

Зрозуміти різницю між `virtual`/`override` (справжній поліморфізм) та `new` (приховування методу). Навчитись будувати ієрархії підкласів де кожен тип поводиться по-своєму через єдиний базовий інтерфейс.

## Контекст

Після Lab 07 `Appointment` реалізує `IPayable` — але всі записи однакові і коштують однаково. Насправді клініка має три типи прийомів: звичайний, терміновий (+50% вартість) і консультація спеціаліста (+30% вартість). Ця лаба вводить підкласи. Меню **не змінюється** — зміни внутрішні.

> Ця лаба зливається в `main` після Task 4. Task 5 — бонус.

## Гілка

```bash
git checkout main
git pull
git checkout -b feature/polymorphism
```

---

## Завдання 1 — virtual методи та перший підклас ⭐

### Умова

Зараз `GetCost()` і `GetDescription()` в `Appointment` — звичайні методи. Підклас може їх перекрити через `new`, але поліморфізм не працюватиме. Потрібно зробити їх `virtual`.

### Що реалізувати

**`Models/Appointment.cs`** — внести зміни:

```csharp
public virtual decimal GetCost() => (decimal)DurationMinutes * 10m;
public virtual string GetDescription() => "Звичайний прийом";
public int GetPriority() => 3;  // не virtual — навмисно, для Task 3
```

Також оновити `ToString()` щоб використовував `GetDescription()` і `GetCost()`:

```csharp
public override string ToString()
{
    string result = "[" + Id + "] " + GetDescription() +
                    " | Пацієнт #" + PatientId + " → Лікар #" + DoctorId +
                    " | " + ScheduledAt.ToString("dd.MM.yyyy HH:mm") + "–" + EndsAt.ToString("HH:mm") +
                    " | " + Status +
                    " | " + GetCost().ToString("F2") + " грн";
    if (Notes.Length > 0) result += " | " + Notes;
    return result;
}
```

**`Models/RegularAppointment.cs`** — новий файл. Клас успадковує `Appointment`, конструктор викликає `base(...)`, `GetDescription()` повертає рядок-опис:

```csharp
public class RegularAppointment : Appointment
{
    public RegularAppointment(int patientId, int doctorId, DateTime scheduledAt, int durationMinutes = 30)
        : base(patientId, doctorId, scheduledAt, durationMinutes) { }

    public override string GetDescription() => /* рядок що описує тип прийому */;
}
```

### Що перевірити

Після змін: `new RegularAppointment(1, 1, DateTime.Today)` повинно компілюватись і виводитись через `ToString()` з описом "Звичайний прийом" та вартістю в гривнях.

### Підказки

1. `virtual` у базовому класі — це дозвіл на перевизначення. Без нього `override` у підкласі не компілюється.
2. Якщо `GetDescription()` в `ToString()` — то `ToString()` автоматично показуватиме рядок підкласу при виводі `Appointment[]`. Це і є поліморфізм.
3. [virtual keyword — docs](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/virtual)

### Адаптація до вашого домену

| Клініка | Готель | Ресторан | Університет | Прокат авто | Бібліотека | Спортзал |
|---------|--------|----------|-------------|-------------|------------|---------|
| `Appointment` | `Booking` | `TableReservation` | `Enrollment` | `Rental` | `BookLoan` | `Session` |
| `virtual GetDescription()` | `virtual GetDescription()` | `virtual GetDescription()` | `virtual GetDescription()` | `virtual GetDescription()` | `virtual GetDescription()` | `virtual GetDescription()` |
| `virtual GetCost()` | `virtual GetCost()` | `virtual GetCost()` | `virtual GetCost()` | `virtual GetCost()` | `virtual GetFine()` | `virtual GetCost()` |
| `RegularAppointment` | `StandardBooking` | `RegularReservation` | `RegularEnrollment` | `BasicRental` | `RegularLoan` | `RegularSession` |

### Коміт

```bash
git add src/Models/Appointment.cs src/Models/RegularAppointment.cs
git commit -m "Lab08 Task1: make GetCost() and GetDescription() virtual, add RegularAppointment"
```

---

## Завдання 2 — UrgentAppointment і SpecialistAppointment ⭐⭐

### Умова

Клініка хоче додати термінові прийоми (дорожче) і консультації спеціалістів (теж дорожче). Кожен тип має свою логіку ціни і свій опис. Але зберігатись вони повинні в одному масиві `Appointment[]`.

### Що реалізувати

**`Models/UrgentAppointment.cs`** — новий файл:

- Поле `string UrgencyNote` (причина терміновості, ініціалізується в конструкторі)
- `override GetCost()` → на 50% дорожче за базову ставку (`base.GetCost()`)
- `sealed override GetDescription()` → рядок "Терміновий" + UrgencyNote (якщо не порожній)
- `new int GetPriority() => 1` — **не** override (навмисно, пояснення в Task 3)

```csharp
public class UrgentAppointment : Appointment
{
    public string UrgencyNote { get; }

    public UrgentAppointment(int patientId, int doctorId, DateTime scheduledAt,
                              string urgencyNote = "", int durationMinutes = 30)
        : base(patientId, doctorId, scheduledAt, durationMinutes)
    {
        UrgencyNote = urgencyNote;
    }

    public override decimal GetCost() { /* base.GetCost() × коефіцієнт */ }
    public sealed override string GetDescription() { /* "Терміновий" + UrgencyNote */ }
    public new int GetPriority() => 1;
}
```

**`Models/SpecialistAppointment.cs`** — новий файл:

- Клас **sealed** (не можна далі успадковувати)
- Поле `string ConsultationTopic` (ініціалізується в конструкторі)
- `override GetCost()` → на 30% дорожче за базову ставку
- `override GetDescription()` → рядок "Консультація спеціаліста" + тема

```csharp
public sealed class SpecialistAppointment : Appointment
{
    public string ConsultationTopic { get; }

    public SpecialistAppointment(int patientId, int doctorId, DateTime scheduledAt,
                                  string topic = "", int durationMinutes = 45)
        : base(patientId, doctorId, scheduledAt, durationMinutes)
    {
        ConsultationTopic = topic;
    }

    public override decimal GetCost() { /* base.GetCost() × коефіцієнт */ }
    public override string GetDescription() { /* "Консультація спеціаліста" + тема */ }
}
```

### Що перевірити

```csharp
Appointment[] appointments = new Appointment[]
{
    new RegularAppointment(1, 1, DateTime.Today),
    new UrgentAppointment(1, 2, DateTime.Today, "біль у грудях"),
    new SpecialistAppointment(2, 3, DateTime.Today, "кардіологія", 60)
};

for (int i = 0; i < appointments.Length; i++)
    Console.WriteLine(appointments[i]); // кожен рядок різний — без жодного if!
```

Три різних рядки, три різних ціни — один масив `Appointment[]`.

### Підказки

1. `sealed override` на методі = можна `override` цей метод тут, але підкласи `UrgentAppointment` вже не зможуть.
2. `sealed class` = клас є листом ієрархії. Спроба успадкувати від `SpecialistAppointment` — помилка компіляції.
3. `base.GetCost()` — викликає реалізацію батька (30 * DurationMinutes), потім множимо на коефіцієнт.
4. [sealed modifier — docs](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/sealed)
5. [override keyword — docs](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/override)

### Адаптація до вашого домену

| Клініка | Готель | Ресторан | Університет | Прокат авто | Бібліотека | Спортзал |
|---------|--------|----------|-------------|-------------|------------|---------|
| `UrgentAppointment` (×1.5) | `SuiteBooking` (×2.0) | `PrivateRoomReservation` (×1.5) | `OnlineEnrollment` (×0.9) | `PremiumRental` (×1.3) | `DigitalLoan` (інша логіка) | `PersonalTraining` (×2.0) |
| `SpecialistAppointment` (×1.3) | `CorporateBooking` (×0.8) | `EventReservation` (×2.0) | `IntensiveCourse` (×1.4) | `LongTermRental` (×0.8) | `ResearchLoan` (довший термін) | `GroupSession` (×0.6) |
| `sealed override GetDescription()` | `sealed override GetDescription()` | `sealed override GetDescription()` | `sealed override GetDescription()` | `sealed override GetDescription()` | `sealed override GetDescription()` | `sealed override GetDescription()` |
| `sealed class SpecialistAppointment` | `sealed class CorporateBooking` | `sealed class EventReservation` | `sealed class IntensiveCourse` | `sealed class LongTermRental` | `sealed class ResearchLoan` | `sealed class GroupSession` |

### Коміт

```bash
git add src/Models/UrgentAppointment.cs src/Models/SpecialistAppointment.cs
git commit -m "Lab08 Task2: add UrgentAppointment and SpecialistAppointment"
```

---

## Завдання 3 — new vs override: в чому різниця? ⭐⭐⭐

### Умова

В `UrgentAppointment` є `new int GetPriority() => 1`, а в `Appointment` — `int GetPriority() => 3`. Студент має самостійно **дослідити** що відбувається при виклику через різні типи посилань, і пояснити різницю.

### Що реалізувати

**`Managers/AppointmentManager.cs`** — два зміни:

1. `Book()` тепер створює `RegularAppointment` замість `Appointment`:
```csharp
Appointment appointment = new RegularAppointment(patientId, doctorId, scheduledAt, durationMinutes);
```

2. Додати два нових методи:
```csharp
public bool BookUrgent(int patientId, int doctorId, DateTime scheduledAt,
                       string urgencyNote = "", int durationMinutes = 30)
{ ... } // аналогічно Book(), але створює UrgentAppointment

public bool BookSpecialist(int patientId, int doctorId, DateTime scheduledAt,
                           string topic = "", int durationMinutes = 45)
{ ... } // аналогічно Book(), але створює SpecialistAppointment
```

**`Program.cs`** — оновити seed data:

```csharp
clinic.Appointments.Book(1, 1, tomorrow.AddHours(10));
clinic.Appointments.BookUrgent(2, 2, tomorrow.AddHours(11), "гострий головний біль", 45);
clinic.Appointments.BookSpecialist(3, 3, dayAfter.AddHours(9), "педіатрія", 20);

// Демонстрація: new vs override
Appointment urgentRef = clinic.Appointments[1]; // тип посилання — Appointment
Console.WriteLine("GetDescription (override): " + urgentRef.GetDescription()); // "Терміновий (...)" ✓
Console.WriteLine("GetPriority   (new):       " + urgentRef.GetPriority());    // 3, а не 1!
```

### Ключове питання для розуміння

Запусти програму і подивись на вивід. Потім дай відповідь:

- Чому `GetDescription()` повертає `"Терміновий (...)"`, а не `"Звичайний прийом"`?
- Чому `GetPriority()` повертає `3`, а не `1`, хоча реальний об'єкт — `UrgentAppointment`?
- Що треба змінити в `Appointment`, щоб `GetPriority()` теж вів себе поліморфно?

### Підказки

1. Тип **посилання** (ліва частина `Appointment urgentRef`) визначає які методи доступні.
2. Тип **об'єкта** (правова частина `new UrgentAppointment(...)`) визначає яка реалізація викликається — але **тільки для `virtual`/`override` методів**.
3. `new` повідомляє компілятору: "я знаю, що ховаю базовий метод, це навмисно". Але поліморфізму не дає.
4. [new modifier — docs](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/new-modifier)

### Коміт

```bash
git add src/Managers/AppointmentManager.cs src/Program.cs
git commit -m "Lab08 Task3: BookUrgent/BookSpecialist, update seed data, demonstrate new vs override"
```

---

## Завдання 4 — фільтр за типом у меню ⭐⭐⭐

### Умова

Поліморфізм поки "невидимий" — програма працює правильно, але користувач не бачить різниці. Додай у підменю "Записи" новий пункт **"8. За типом прийому"** — щоб можна було окремо переглянути термінові, консультації спеціаліста і звичайні.

### Що реалізувати

**`Managers/AppointmentManager.cs`** — три нових методи (паттерн аналогічний `GetByPatient`):

```csharp
public Appointment[] GetUrgent()
{
    int matchCount = 0;
    for (int i = 0; i < _count; i++)
        if (_appointments[i] is UrgentAppointment) matchCount++;
    Appointment[] result = new Appointment[matchCount];
    int idx = 0;
    for (int i = 0; i < _count; i++)
        if (_appointments[i] is UrgentAppointment) result[idx++] = _appointments[i];
    return result;
}
// Аналогічно: GetSpecialist() і GetRegular()
```

Також оновити `DisplayAppointment` — додати тип і вартість у рядок виводу:

```csharp
string line = "[" + a.Id + "] " + a.GetDescription() +   // ← поліморфний виклик
              " | " + patientName + " → " + doctorName +
              " | " + a.ScheduledAt.ToString("dd.MM.yyyy HH:mm") + "–" + a.EndsAt.ToString("HH:mm") +
              " | " + a.Status +
              " | " + a.GetCost().ToString("F2") + " грн"; // ← різна ціна для кожного типу
```

**`Program.cs`** — додати у меню "Записи":

```csharp
Console.WriteLine("  8. За типом прийому");
// ...
case "8": AppointmentsByTypeMenu(clinic); break;
```

```csharp
static void AppointmentsByTypeMenu(Clinic clinic)
{
    Console.WriteLine("── За типом прийому ──────────");
    Console.WriteLine("  1. Термінові");
    Console.WriteLine("  2. Консультації спеціаліста");
    Console.WriteLine("  3. Звичайні");
    Console.Write("Оберіть: ");
    string choice = Console.ReadLine() ?? "";
    switch (choice)
    {
        case "1": clinic.Appointments.DisplayList(clinic.Appointments.GetUrgent()); break;
        case "2": clinic.Appointments.DisplayList(clinic.Appointments.GetSpecialist()); break;
        case "3": clinic.Appointments.DisplayList(clinic.Appointments.GetRegular()); break;
    }
}
```

### Що перевірити

Запусти і відкрий `3. Записи → 8. За типом → 1. Термінові`. Якщо seed data завантажено правильно — побачиш тільки `UrgentAppointment` з написом "Терміновий (гострий головний біль)" і ціною × 1.5.

### Ключові спостереження

- `a.GetDescription()` в `DisplayAppointment` — це поліморфний виклик. Без `virtual`/`override` всі рядки виглядали б однаково.
- `a.GetCost()` — аналогічно, кожен тип повертає іншу суму без жодного `if`.
- `is UrgentAppointment` у циклі — це runtime-перевірка фактичного типу об'єкта, не типу посилання.

### Коміт

```bash
git add src/Managers/AppointmentManager.cs src/Program.cs
git commit -m "Lab08 Task4: GetUrgent/GetSpecialist/GetRegular, AppointmentsByTypeMenu, show type in list"
```

---

## Завдання 5 — відкрита проблема: комбінації типів ⭐⭐⭐⭐

### Умова

Керівник клініки каже: "Ми хочемо VIP-знижку 20% для всіх трьох типів прийомів. Тобто VIP-терміновий = базова ціна × 1.5 × 0.8. VIP-консультація = базова × 1.3 × 0.8."

Студент пробує додати `VipUrgentAppointment : UrgentAppointment` — але `GetDescription()` в `UrgentAppointment` **sealed**, тобто `override` забороняється. А `SpecialistAppointment` взагалі **sealed class**.

### Що потрібно дослідити

1. Спробуй успадкувати від `SpecialistAppointment`. Яка помилка компілятора? Що вона означає?
2. Спробуй успадкувати від `UrgentAppointment` і `override GetDescription()`. Яка помилка?
3. Підрахуй: якщо додати VIP-варіант кожного типу — скільки нових класів потрібно? А якщо ще є "дитячий" тариф і "пенсійний"?

### Що реалізувати

Запропонуй і реалізуй один із підходів:

**Варіант А — поле-модифікатор у базовому класі:**
```csharp
// В Appointment:
public decimal DiscountFactor { get; set; } = 1.0m;
public override decimal GetCost() => (decimal)DurationMinutes * 10m * DiscountFactor;
// У підкласах: base.GetCost() вже враховує знижку
```

**Варіант Б — конструктор з коефіцієнтом:**
```csharp
public class UrgentAppointment : Appointment
{
    private readonly decimal _factor;
    public UrgentAppointment(..., decimal factor = 1.5m) : base(...) { _factor = factor; }
    public override decimal GetCost() => base.GetCost() * _factor;
}
// Тоді: new UrgentAppointment(1, 1, date, factor: 1.5m * 0.8m)
```

Обери варіант, реалізуй, і напиши коментар чому саме цей підхід.

### Підказки

1. Жоден варіант не є "правильним" — є компроміси. Варіант А простіший, Варіант Б гнучкіший.
2. Ця проблема — класичний Open/Closed Principle: клас відкритий до розширення, закритий до модифікації. У Lab 21 (SOLID) ти повернешся до цього коду.
3. Подумай: що якщо замість `decimal` передавати `Func<decimal, decimal> applyDiscount`? Що це дає?
4. [Composition over inheritance](https://en.wikipedia.org/wiki/Composition_over_inheritance)

### Коміт

```bash
git add -A
git commit -m "Lab08 Task4: explore sealed limitations, implement discount modifier approach"
```

---

## Перевірка перед здачею

```bash
cd src
dotnet build
dotnet run
```

Переконайтесь, що:

- [ ] `Appointment[] arr = { new RegularAppointment(...), new UrgentAppointment(...), new SpecialistAppointment(...) }` — компілюється
- [ ] Цикл `for` по `arr` виводить різні рядки для кожного типу — без `if`/`switch`
- [ ] `UrgentAppointment.GetCost()` повертає більше за `RegularAppointment.GetCost()` при однаковій тривалості
- [ ] `SpecialistAppointment.GetCost()` теж більше за базовий
- [ ] Спроба `class X : SpecialistAppointment` → помилка компіляції (sealed class)
- [ ] `Appointment ref = new UrgentAppointment(...)` → `ref.GetPriority()` повертає `3`, не `1`
- [ ] `UrgentAppointment ref = new UrgentAppointment(...)` → `ref.GetPriority()` повертає `1`
- [ ] `BookUrgent()` і `BookSpecialist()` додають записи в `AppointmentManager`
- [ ] Список записів тепер показує тип і вартість кожного прийому
- [ ] `3. Записи → 8. За типом → 1. Термінові` — виводить тільки `UrgentAppointment`
- [ ] `3. Записи → 8. За типом → 2. Консультації` — виводить тільки `SpecialistAppointment`
- [ ] `3. Записи → 8. За типом → 3. Звичайні` — виводить тільки `RegularAppointment`

---

## Питання для самоперевірки

1. Що таке поліморфізм? Яку роль відіграє `virtual`/`override` у його реалізації?
2. Навіщо `new` якщо він не дає поліморфізму? Коли `new` може бути корисним?
3. Що означає `sealed` на класі? Що означає `sealed` на методі? Чим вони відрізняються?
4. `base.GetCost()` в `UrgentAppointment` — що конкретно він викликає? Що повернеться якщо `DurationMinutes = 30`?
5. Чому зберігати `UrgentAppointment` в масиві `Appointment[]` — це нормально? Що при цьому відбувається з типом?
6. Якщо додати четвертий тип прийому `EmergencyAppointment : Appointment` — які файли треба змінити? Чи треба змінювати `AppointmentManager.DisplayList()`?
7. (Бонус) Яка різниця між поліморфізмом через `virtual`/`override` (Lab 08) і поліморфізмом через `interface` (Lab 07)? Коли обираєш одне, коли інше?

---

## Злиття

```bash
git checkout main
git merge --no-ff feature/polymorphism -m "Merge feature/polymorphism: Lab08 Polymorphism"
git push
```

> Наступна лаба: `git checkout -b feature/generics` — `Repository<T>`, `WaitingQueue<T>`, `where T :`.
