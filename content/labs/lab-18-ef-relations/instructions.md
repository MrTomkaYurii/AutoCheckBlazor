# Лабораторна робота 18 — EF Core: зв'язки та Navigation Properties

## Проблема

Після Lab 17 у базі даних є дві незалежні таблиці: `Patients` і `Doctors`. Але в реальній системі запис на прийом (`Appointment`) пов'язаний з конкретним пацієнтом і лікарем.

Якщо зберігати `Appointment` як окрему сутність і при відображенні хотіти показати ім'я пацієнта і лікаря, виникає проблема — дані в різних таблицях. Можна завантажити всі три таблиці окремо і зіставити вручну, але це:
1. Багато коду
2. Якщо на 100 записів треба ім'я пацієнта — **101 запит до БД** (проблема N+1)
3. Нема гарантії узгодженості (FK не відстежується)

Реляційні бази даних вирішують це через **зовнішні ключі** (Foreign Keys). EF Core додає до цього **Navigation Properties** — C#-властивості які автоматично описують зв'язки між класами.

---

## Ключові концепції

### Navigation Properties

Navigation Property — це властивість одного класу, що посилається на інший клас (або колекцію):

```csharp
// В Patient: колекція всіх записів цього пацієнта
public ICollection<Appointment> Appointments { get; private set; } = new List<Appointment>();

// В Appointment: посилання на конкретного пацієнта
public Patient? Patient { get; set; }
```

Самі по собі ці властивості — звичайні C#-поля. EF Core "оживляє" їх — при відповідному запиті заповнює даними з бази.

### Eager Loading та проблема N+1

**Проблема N+1**: завантажити 100 записів, потім для кожного окремим запитом завантажити пацієнта — 101 запит.

**Рішення**: `.Include()` — EF виконує один SQL `JOIN` і повертає все разом:

```csharp
// Без Include: 1 запит на Appointments + 100 на Patient = 101 запити
var appointments = context.Appointments.ToList();
foreach (var a in appointments)
    Console.WriteLine(a.Patient.FullName);  // N+1!

// З Include: 1 запит з JOIN = 1 запит
var appointments = context.Appointments.Include(a => a.Patient).ToList();
foreach (var a in appointments)
    Console.WriteLine(a.Patient!.FullName);  // OK
```

### One-to-Many (Fluent API)

Зв'язок "один до багатьох" між Patient і Appointment:

```
Patient (1) ←──→ (N) Appointment
```

Fluent API описує це з боку Appointment (де живе FK стовпець):

```
HasOne(a => a.Patient)         — Appointment має одного Patient
.WithMany(p => p.Appointments) — Patient має багато Appointments
.HasForeignKey(a => a.PatientId) — FK стовпець у таблиці Appointments
.OnDelete(DeleteBehavior.Cascade) — якщо Patient видалено → видалити всі Appointments
```

### TPH — Table Per Hierarchy

Коли є ієрархія класів (`Appointment` → `RegularAppointment`, `UrgentAppointment`, `SpecialistAppointment`), EF Core зберігає всі підтипи в **одній таблиці** з додатковим стовпцем-дискримінатором:

```
Appointments
├── Id, PatientId, DoctorId, ScheduledAt, Status, ...  ← спільні поля
├── AppointmentType: "Regular" / "Urgent" / "Specialist"  ← дискримінатор
├── UrgencyNote  ← тільки для Urgent (NULL для інших)
└── ConsultationTopic  ← тільки для Specialist (NULL для інших)
```

Перевага: немає JOIN між таблицями при завантаженні ієрархії. Недолік: null-стовпці для невластивих полів.

### AsNoTracking

EF Core за замовчуванням **відстежує** кожен завантажений об'єкт (Change Tracker). Це потрібно для `Update/Delete`, але марнує пам'ять і час при read-only запитах.

`.AsNoTracking()` відключає відстеження — на 20-30% швидше для запитів тільки на читання.

---

## Завдання

### Завдання 1. Navigation Properties та EF Core сумісність

**Задача:** додати navigation properties до моделей і підготувати їх для EF Core.

**Проблема 1: Navigation properties**

Додайте до `Patient`:
```csharp
public ICollection<Appointment> Appointments { get; private set; } = new List<Appointment>();
```

Додайте до `Doctor` аналогічно.

Додайте до `Appointment` зворотні посилання:
```csharp
public Patient? Patient { get; set; }
public Doctor? Doctor { get; set; }
```

**Проблема 2: EF Core сумісність**

EF Core при завантаженні об'єкта з БД:
1. Викликає parameterless constructor
2. Встановлює кожну властивість через setter

Клас `Appointment` не має parameterless constructor — додайте `protected Appointment()`. Всередині встановіть безпечні значення за замовчуванням для полів з `private set`.

**Проблема 3: readonly властивості підкласів**

`UrgentAppointment.UrgencyNote` оголошено як `{ get; }` — EF не може встановити після конструктора. Змініть на `{ get; private set; }` і додайте protected ctor.

`SpecialistAppointment` — sealed клас. `sealed` + `protected` = безглузда комбінація. EF Core може викликати `private` constructor через рефлексію. Використайте `private`.

**Ключові питання:**
- Навіщо `ICollection<T>` а не просто `List<T>` або `T[]`?
- Чому `private set` достатньо для EF Core, хоча setter "закритий"?

---

### Завдання 2. Fluent API для One-to-Many

**Задача:** описати зв'язки між `Appointment`, `Patient`, `Doctor` і налаштувати TPH.

**Таблиця Appointments:**

Структура аналогічна до Lab 17, але з двома FK і дискримінатором:
- `PatientId` — Foreign Key на Patients(Id)
- `DoctorId` — Foreign Key на Doctors(Id)
- `AppointmentType` — дискримінатор для TPH (тип: рядок)
- `UrgencyNote` — nullable, тільки для Urgent
- `ConsultationTopic` — nullable, тільки для Specialist

**Cascade Delete — важлива деталь:**

SQL Server не дозволяє дві каскадні доріжки (cascade paths) до однієї таблиці. Якщо обидва FK (`PatientId` і `DoctorId`) мають `OnDelete(Cascade)`, SQL Server видасть помилку при міграції.

Рішення: один FK — `Cascade`, другий — `Restrict`:
- `Patient → Appointments`: Cascade (видалення пацієнта → видалення його записів)
- `Doctor → Appointments`: Restrict (заборона видалити лікаря, якщо є записи)

**HasDiscriminator:**
```
entity.HasDiscriminator<string>("AppointmentType")
      .HasValue<Appointment>("Base")
      .HasValue<RegularAppointment>("Regular")
      ...
```

Після оголошення дискримінатора, підтипи потребують окремої мінімальної конфігурації:
```
modelBuilder.Entity<UrgentAppointment>()
    .Property(u => u.UrgencyNote).HasMaxLength(200).HasDefaultValue("");
```

**Ключові питання:**
- Чому не можна два `OnDelete(Cascade)` в одній таблиці при SQL Server?
- Що означає `HasValue<RegularAppointment>("Regular")` — де "Regular" зберігається?

---

### Завдання 3. Міграція та DbSeeder з Appointments

**Задача:** застосувати нову схему та заповнити тестовими даними.

**Проблема Seeder:** пацієнти і лікарі вже мають Ids з БД, але в Seeder вони невідомі заздалегідь. Рішення — завантажити їх після `SaveChanges`:

```
SeedPatients(context);   // patients отримують DB-Id
SeedDoctors(context);    // doctors отримують DB-Id
SeedAppointments(context); // тепер можна читати реальні Ids
```

Всередині `SeedAppointments`:
```
var patients = context.Patients.ToList();  // завантажує реальні записи з Id
var doctors  = context.Doctors.ToList();
```

Тепер `patients[0].Id` — це реальний DB Id, а не `_nextId`.

Додайте 4 записи різних типів (Regular, Urgent, Specialist), деякі — Complete+Paid.

Запустіть:
```
dotnet ef migrations add AddAppointmentsWithRelations
dotnet ef database update
```

Відкрийте сгенерований клас міграції — знайдіть:
- Де стовпець `AppointmentType`?
- Де FK constraint з `ON DELETE CASCADE`?
- Де `ON DELETE NO ACTION` (Restrict)?

**Ключові питання:**
- Чому `context.Patients.ToList()` а не `context.Patients` безпосередньо для отримання Ids?
- Що станеться, якщо SeedAppointments викликати до SeedDoctors?

---

### Завдання 4. ClinicRepository — запити з .Include()

**Задача:** створити `src/Data/ClinicRepository.cs` з методами які демонструють Eager Loading.

`ClinicRepository` приймає `ClinicDbContext` через конструктор (ін'єкція залежності — тема Lab 21, але патерн правильний вже зараз).

Реалізуйте методи:

**1. GetPatientWithAppointments(int patientId)**
Повертає `Patient?` з заповненою колекцією `Appointments`. Використайте `.Include(p => p.Appointments)`.

**2. GetUpcomingAppointments()**
Повертає заплановані записи у майбутньому. Потребує даних і пацієнта, і лікаря — два `.Include()`.

**3. GetAppointmentsByPatient(int patientId)**
Всі записи пацієнта, відсортовані по даті (newest first). Include Doctor для відображення імені.

**4. GetDoctorStats()**
Для кожного лікаря: кількість записів і загальна виручка. Використайте `.AsNoTracking()` — дані тільки для читання.

**Алгоритм для AsNoTracking:**
```
context.Doctors.AsNoTracking().Include(d => d.Appointments).Select(d => new { ... })
```

**Чому AsNoTracking:**
Change Tracker EF Core зберігає копію кожного завантаженого об'єкта в пам'яті для порівняння. При 1000+ записів це суттєво. `.AsNoTracking()` пропускає цей крок.

**Ключові питання:**
- Що відбудеться, якщо `.Include()` немає, а ми звертаємось до `appointment.Patient.FullName`?
- Чи можна зробити `.Include().ThenInclude()` — навіщо це?
- У чому різниця між `AsNoTracking()` і відключенням Change Tracker взагалі?

---

## Рефлексійні питання

1. **Navigation vs Id.** В `Appointment` є і `PatientId` (FK), і `Patient?` (navigation property). Навіщо зберігати FK окремо, якщо є навігаційне посилання?

2. **Cascade vs Restrict.** Клініка вирішила: видалення пацієнта видаляє його записи. Але видалення лікаря забороняється. Чи це правильно з бізнес-точки зору? Яка альтернатива?

3. **TPH vs TPT (Table Per Type).** TPH (один рядок включає nullable стовпці) vs TPT (окремі таблиці для кожного підтипу з JOIN). Коли TPH краще? Коли TPT?

4. **LazyLoading.** У EF Core є механізм LazyLoading — navigation property завантажується автоматично при першому зверненні. Чому ми його не вмикаємо за замовчуванням?

5. **Include depth.** Чи можна зробити `.Include(p => p.Appointments).ThenInclude(a => a.Doctor)`? Що це дасть? Чи є небезпека?

6. **Repository pattern.** `ClinicRepository` агрегує складні запити. Але це збільшує кількість файлів. Чи варто тримати прості запити (`context.Patients.ToList()`) прямо в Program.cs, а складні — в Repository?
