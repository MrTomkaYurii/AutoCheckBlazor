# Лаба 07 — Interfaces (Інтерфейси)

## Мета

Навчитись оголошувати інтерфейси, реалізовувати їх у класах та писати код, що залежить від контракту, а не від конкретного типу.

## Контекст

Після Lab 06 система вміє зберігати медичні записи. Але оплата прийомів досі не відстежується — немає поняття "оплачено / не оплачено", немає суми, немає можливості скасування через інтерфейс. Ця лаба додає **фінансовий блок**: інтерфейси `IPayable`, `ICancellable`, `ISchedulable` та новий розділ меню "Рахунки".

## Гілка

```bash
git checkout main
git pull
git checkout -b feature/interfaces
```

---

## Завдання 1 — IPayable та оплата записів ⭐

### Умова

Клініка хоче відстежувати оплату прийомів. Кожен запис має вартість і статус оплати.

### Що реалізувати

**`Interfaces/IPayable.cs`** — новий файл:

```csharp
namespace YourApp.Interfaces;

public interface IPayable
{
    decimal GetCost();
    bool IsPaid { get; }
    void MarkPaid();
}
```

**`Models/Appointment.cs`** — клас реалізує `IPayable`:

```csharp
public class Appointment : IPayable
```

Додати:
- Приватне поле `bool _isPaid`
- `decimal GetCost()` — повертає вартість (наприклад, `(decimal)DurationMinutes * 10m`)
- `bool IsPaid => _isPaid;`
- `void MarkPaid()` — якщо не скасовано, встановлює `_isPaid = true`

**`Managers/AppointmentManager.cs`** — додати метод:

```csharp
public Appointment[] GetAll() { ... }
```

Повертає копію всіх записів у масиві.

### Підказки

1. Інтерфейс — це контракт без реалізації. Клас що його реалізує **зобов'язаний** надати тіло кожного члена.
2. `decimal` (не `double`) — для грошових розрахунків. Літерал: `10m`.
3. `MarkPaid()` не повинен дозволяти оплату скасованого запису.
4. [Interfaces — C# docs](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/interface)

### Адаптація до вашого домену

| Клініка | Готель | Ресторан | Університет | Прокат авто | Бібліотека | Спортзал |
|---------|--------|----------|-------------|-------------|------------|---------|
| `Appointment` реалізує `IPayable` | `Booking` реалізує `IPayable` | `TableReservation` реалізує `IPayable` | `Enrollment` реалізує `IPayable` | `Rental` реалізує `IPayable` | `BookLoan` реалізує `IPayable` | `Session` реалізує `IPayable` |
| `GetCost()` = `DurationMinutes * 10m` | `GetCost()` = `StayNights * RoomRate` | `GetCost()` = фіксована ставка | `GetCost()` = `CourseFee` | `GetCost()` = `RentalDays * DailyRate` | `GetFine()` = `OverdueDays * DailyFine` | `GetCost()` = `DurationMinutes * Rate` |
| `IsPaid` / `MarkPaid()` | `IsPaid` / `MarkPaid()` | `IsPaid` / `MarkPaid()` | `IsPaid` / `MarkPaid()` | `IsPaid` / `MarkPaid()` | `IsPaid` / `MarkPaid()` | `IsPaid` / `MarkPaid()` |

### Коміт

```bash
git add src/Interfaces/IPayable.cs src/Models/Appointment.cs src/Managers/AppointmentManager.cs
git commit -m "Lab07 Task1: add IPayable, implement in Appointment, add AppointmentManager.GetAll()"
```

---

## Завдання 2 — ICancellable та скасування через контракт ⭐⭐

### Умова

Менеджер хоче скасувати всі прострочені записи одним викликом — незалежно від того, що це за об'єкт. Потрібен спільний контракт.

### Що реалізувати

**`Interfaces/ICancellable.cs`** — новий файл:

```csharp
namespace YourApp.Interfaces;

public interface ICancellable
{
    bool IsCancelled { get; }
    string CancellationReason { get; }
    bool Cancel(string reason = "");
}
```

**`Models/Appointment.cs`** — додати другий інтерфейс:

```csharp
public class Appointment : IPayable, ICancellable
```

Додати:
- `bool IsCancelled => Status == AppointmentStatus.Cancelled;`
- `string CancellationReason => IsCancelled ? Notes : "";`
- Метод `Cancel(string reason)` вже існує — нічого не міняти.

### Підказки

1. Клас може реалізовувати **кілька інтерфейсів** — через кому: `class X : IA, IB`.
2. Якщо метод вже є в класі з правильною сигнатурою — компілятор вважає його реалізацією інтерфейсу автоматично.
3. Спробуй написати метод що приймає `ICancellable[]` і скасовує всі де `!IsCancelled` та дата вже минула. Він не знає нічого про `Appointment` — тільки про контракт.
4. [Multiple interfaces — C# docs](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/types/interfaces)

### Адаптація до вашого домену

| Клініка | Готель | Ресторан | Університет | Прокат авто | Бібліотека | Спортзал |
|---------|--------|----------|-------------|-------------|------------|---------|
| `Appointment` реалізує `ICancellable` | `Booking` реалізує `ICancellable` | `TableReservation` реалізує `ICancellable` | `Enrollment` реалізує `ICancellable` | `Rental` реалізує `ICancellable` | `BookLoan` реалізує `ICancellable` | `Session` реалізує `ICancellable` |
| `Cancel(reason)` | `Cancel(reason)` | `Cancel(reason)` | `Withdraw(reason)` | `Cancel(reason)` | `Cancel(reason)` | `Cancel(reason)` |
| `CancellationReason` | `CancellationReason` | `CancellationReason` | `WithdrawalReason` | `CancellationReason` | `CancellationReason` | `CancellationReason` |

### Коміт

```bash
git add src/Interfaces/ICancellable.cs src/Models/Appointment.cs
git commit -m "Lab07 Task2: add ICancellable, implement in Appointment"
```

---

## Завдання 3 — ISchedulable та BillingManager ⭐⭐⭐

### Умова

Потрібно: (а) перевіряти чи лікар приймає в певний час через єдиний контракт; (б) зібрати всі неоплачені записи та порахувати борги.

### Що реалізувати

**`Interfaces/ISchedulable.cs`** — новий файл:

```csharp
namespace YourApp.Interfaces;

public interface ISchedulable
{
    bool CanSchedule(DateTime at);
    DateTime[] GetAvailableSlots(DateTime date, int slotCount);
}
```

**`Models/Doctor.cs`** — реалізує `ISchedulable`:

```csharp
public class Doctor : ISchedulable
```

Додати:
- `bool CanSchedule(DateTime at) => Schedule.Contains(at.Hour);`
- `DateTime[] GetAvailableSlots(DateTime date, int slotCount)` — повертає масив вільних слотів (цілі години в межах `Schedule.Start`..`Schedule.End`).

**`Managers/BillingManager.cs`** — новий файл. Приймає `AppointmentManager` у конструкторі.

Методи:
- `IPayable[] GetAllUnpaid()` — всі записи де `!IsPaid && !IsCancelled`
- `IPayable[] GetUnpaidByPatient(int patientId)` — фільтр по пацієнту
- `decimal GetTotalDebt()` — сума `GetCost()` по всіх неоплачених
- `decimal GetPatientDebt(int patientId)` — сума по пацієнту
- `bool PayAppointment(int appointmentId)` — знаходить запис та викликає `MarkPaid()`
- `void DisplayUnpaid(IPayable[] items)` — виводить список з сумами

### Підказки

1. `GetAllUnpaid()` повертає `IPayable[]` — не `Appointment[]`. Метод не "знає" що це `Appointment`.
2. `GetTotalDebt()` ітерує `IPayable[]` та викликає `.GetCost()` — без жодного знання про конкретний тип.
3. `DisplayUnpaid` може використати `is Appointment a` щоб показати деталі (пацієнт, лікар, дата), але це необов'язково — можна вивести лише суму через інтерфейс.
4. [Interface as parameter type](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/types/interfaces#interface-usage)

### Адаптація до вашого домену

| Клініка | Готель | Ресторан | Університет | Прокат авто | Бібліотека | Спортзал |
|---------|--------|----------|-------------|-------------|------------|---------|
| `Doctor` реалізує `ISchedulable` | `Staff` реалізує `ISchedulable` | `Waiter` реалізує `ISchedulable` | `Lecturer` реалізує `ISchedulable` | `Manager` реалізує `ISchedulable` | `Librarian` реалізує `ISchedulable` | `Trainer` реалізує `ISchedulable` |
| `CanSchedule(DateTime)` | `CanCheckIn(DateTime)` | `CanServe(DateTime)` | `CanTeach(DateTime)` | `CanHandle(DateTime)` | `CanIssue(DateTime)` | `CanTrain(DateTime)` |
| `BillingManager` | `BillingManager` | `BillingManager` | `BillingManager` | `BillingManager` | `FineManager` | `BillingManager` |
| `GetUnpaidByPatient` | `GetUnpaidByGuest` | `GetUnpaidByCustomer` | `GetUnpaidByStudent` | `GetUnpaidByClient` | `GetUnpaidFinesByReader` | `GetUnpaidByMember` |

### Коміт

```bash
git add src/Interfaces/ISchedulable.cs src/Models/Doctor.cs src/Managers/BillingManager.cs
git commit -m "Lab07 Task3: add ISchedulable, implement in Doctor, add BillingManager"
```

---

## Завдання 4 — Інтеграція та меню "Рахунки" ⭐⭐⭐

### Умова

Підключити `BillingManager` до системи та показати роботу інтерфейсів через консоль.

### Що реалізувати

**`Clinic.cs`** — додати:
```csharp
public BillingManager Billing { get; }
// у конструкторі:
Billing = new BillingManager(Appointments);
```

**`Program.cs`** — нове підменю "Рахунки":
```
── Рахунки ───────────────────
  1. Борги пацієнта
  2. Всі неоплачені записи
  3. Оплатити запис
  4. Загальний борг клініки
  0. Назад
```

Також оновити головне меню — додати короткий опис кожного пункту через `—`:
```
║  1. Пацієнти       — реєстрація, пошук      ║
║  2. Лікарі         — персонал, розклад      ║
...
║  5. Рахунки        — оплата, борги          ║
```

### Підказки

1. У `BillingMenu` тип змінної: `IPayable[] unpaid = clinic.Billing.GetUnpaidByPatient(pId);` — не `Appointment[]`.
2. `GetPatientDebt` і `DisplayUnpaid` — два окремі виклики для одного пацієнта.
3. Порядок `catch`: якщо додаєш `try/catch` — спочатку конкретніший виняток.

### Коміт

```bash
git add src/Clinic.cs src/Program.cs
git commit -m "Lab07 Task4: integrate BillingManager into Clinic, add BillingMenu and menu descriptions"
```

---

## Перевірка перед здачею

```bash
cd src
dotnet build
dotnet run
```

Переконайтесь, що:

- [ ] Головне меню має описи через `—` для кожного пункту
- [ ] Пункт "5. Рахунки" присутній і відкриває підменю
- [ ] "Борги пацієнта" виводить список і суму
- [ ] "Оплатити запис" повертає підтвердження, після чого запис зникає зі списку неоплачених
- [ ] "Оплатити" вже оплачений запис — повідомлення про помилку, не крах
- [ ] "Загальний борг" рахується правильно (сума по всіх неоплачених)
- [ ] `IPayable[] items = clinic.Billing.GetAllUnpaid()` компілюється — тип змінної інтерфейс, не клас
- [ ] `BillingManager.GetTotalDebt()` не містить жодного `is Appointment` — тільки `IPayable`

---

## Питання для самоперевірки

1. Чим інтерфейс відрізняється від `abstract class`? Коли обираєш одне, коли інше?
2. Чому `GetTotalDebt()` приймає `IPayable[]`, а не `Appointment[]`? Що зміниться якщо завтра `Prescription` теж стане платним?
3. Клас `BillingManager` не має жодного `import` на `Appointment` напряму (тільки через `AppointmentManager`). Чому це добре?
4. Що означає "реалізувати кілька інтерфейсів"? Чому C# не дозволяє успадкування від кількох класів, але дозволяє від кількох інтерфейсів?
5. `IPayable[] items = new Appointment[3]` — чому це компілюється? Що за механізм?
6. Спробуй написати метод `static decimal TotalCost(IPayable[] items)` — де він може жити і чому він не залежить від жодного конкретного класу?

---

## Злиття

```bash
git checkout main
git merge --no-ff feature/interfaces -m "Merge feature/interfaces: Lab07 Interfaces"
git push
```

> Наступна лаба: `git checkout -b feature/polymorphism` — `RegularAppointment`, `UrgentAppointment`, `new` keyword, `sealed`.
