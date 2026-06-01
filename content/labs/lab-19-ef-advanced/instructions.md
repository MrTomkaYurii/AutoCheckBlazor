# Лабораторна робота 19 — EF Core: TPH глибоко, Owned Entity, Concurrency

## Проблема

Після Lab 18 таблиця `Appointments` вже зберігає ієрархію підтипів через TPH. Але `MedicalRecord` (діагноз, аналіз, рецепт) ще не в базі даних — і ця ієрархія складніша: `MedicalRecord` — абстрактний клас, а підтипи мають принципово різні набори полів.

Крім того, `Patient` зростає: йому потрібен контактний номер на випадок надзвичайної ситуації — але це не окрема сутність, а **частина** пацієнта (ім'я, телефон, роль). Зберігати це в окремій таблиці надлишково — краще кілька додаткових стовпців у Patients.

І ще: якщо два адміністратори одночасно редагують картку пацієнта — хто "переможе"? Без захисту від **паралельного доступу** останній запис мовчки перезапише перший.

---

## Ключові концепції

### TPH для абстрактної ієрархії

В Lab 18 TPH застосовувався до `Appointment` — конкретного класу. Тепер ієрархія `MedicalRecord` — абстрактна: не можна створити `new MedicalRecord()`.

EF Core підтримує TPH з abstract базовими класами — дискримінатор описується для базового типу, а всі concrete підтипи реєструються окремо.

**Складність:** поля підтипів `Diagnosis`, `LabResult`, `Prescription` — принципово різні. В одній таблиці вони є `NULL` для несумісних підтипів. Флюент API описує ці поля як `IsRequired(false)`.

### Owned Entity

**Проблема:** `EmergencyContact` — не незалежна сутність; вона існує тільки як частина `Patient`. Але у неї три поля (`Name`, `Phone`, `Relationship`).

ValueConverter (WorkSchedule) — серіалізує в один рядок. Для EmergencyContact це незручно — три поля трьох різних типів.

**Рішення — Owned Entity (`OwnsOne`):**

```
Patient → EC_Name, EC_Phone, EC_Relationship (стовпці у таблиці Patients)
```

EF Core вбудовує стовпці EmergencyContact прямо в таблицю власника. Немає JOIN, немає FK, немає окремої таблиці.

```
modelBuilder.Entity<Patient>().OwnsOne(p => p.EmergencyContact, ec =>
{
    ec.Property(e => e.Name).HasColumnName("EC_Name").HasMaxLength(100);
    ...
});
```

Важлива деталь: `OwnsOne` дозволяє `null` (пацієнт без контакту). В БД стовпці просто NULL.

### Concurrency Token

**Проблема паралельного доступу:**

1. Адміністратор А завантажує Patient (RowVersion = `[1,2,3,4]`)
2. Адміністратор Б завантажує той самий Patient
3. Адміністратор Б зберігає зміни → RowVersion стає `[1,2,3,5]`
4. Адміністратор А намагається зберегти — його версія `[1,2,3,4]` вже застаріла!

`IsRowVersion()` — EF додає до UPDATE:
```sql
UPDATE Patients SET ... WHERE Id = @id AND RowVersion = @original_version
```

Якщо за час між SELECT і UPDATE хтось вже змінив запис — `WHERE` не знаходить рядка → 0 рядків оновлено → EF кидає `DbUpdateConcurrencyException`.

SQL Server автоматично оновлює `rowversion` (тип timestamp) при кожному UPDATE.

---

## Завдання

### Завдання 1. MedicalRecord — EF Core сумісність

**Задача:** підготувати абстрактну ієрархію для EF Core.

**Проблема: абстрактний клас з readonly властивостями**

`MedicalRecord` — абстрактний. EF Core ніколи не створює його безпосередньо, лише конкретні підтипи. Але shared конфігурація для всіх підтипів (Id, PatientId, DoctorId, Date) — в базовому класі.

`public int Id { get; }` — потрібен `private set` для EF. Аналогічно для `PatientId`, `DoctorId`, `Date`.

Protected ctor для EF:
```csharp
protected MedicalRecord() { Date = DateTime.Today; }
```

Чому `Date = DateTime.Today`? Щоб властивість мала безпечне значення після EF-побудови, ще до того як EF встановить реальне значення через setter.

**Підкласи:**
- `Diagnosis`: додати `protected Diagnosis() { }`
- `LabResult`: додати `protected LabResult() { }`
- `Prescription`: додати `protected Prescription() { }`

Перевірте: чи потрібні зміни в приватних полях підкласів (`_diagnosisCode`, `_testName`, etc.)? EF викликає setter при завантаженні — а setter валідує. Чи небезпечно це?

**Navigation property:**
Додайте до `MedicalRecord`:
```csharp
public Patient? Patient { get; set; }  // navigation
```

**Ключові питання:**
- Чому EF Core може використовувати `protected` конструктор? Він же не `public`.
- Чи потрібен parameterless ctor для підкласів, якщо базовий `protected MedicalRecord()` вже є?

---

### Завдання 2. Fluent API для MedicalRecord TPH

**Задача:** описати TPH ієрархію MedicalRecord з правильними FK та nullable стовпцями.

**Структура таблиці MedicalRecords:**

```
Id            — PK, IDENTITY
PatientId     — FK на Patients, Cascade
DoctorId      — FK на Doctors, Restrict
Date          — datetime2
Notes         — nvarchar(500)
RecordType    — дискримінатор: "Diagnosis" / "LabResult" / "Prescription"
DiagnosisCode — nullable, тільки для Diagnosis
Description   — nullable, тільки для Diagnosis
IsChronic     — nullable bit, тільки для Diagnosis
TestName      — nullable, тільки для LabResult
Value         — nullable float, тільки для LabResult
Unit          — nullable, тільки для LabResult
ReferenceRange— nullable, тільки для LabResult
IsNormal      — nullable bit, тільки для LabResult
MedicationName— nullable, тільки для Prescription
Dosage        — nullable, тільки для Prescription
DurationDays  — nullable int, тільки для Prescription
Instructions  — nullable, тільки для Prescription
```

**Two Cascade Paths:**

Знову проблема двох каскадних шляхів. `MedicalRecord` має два FK: `PatientId` і `DoctorId`. Обидва не можуть бути `Cascade`.

Рішення: `Patient → MedicalRecords: Cascade`, `Doctor → MedicalRecords: Restrict`.

**Конфігурація підтипів:**

Після головної конфігурації — окремо для кожного підтипу:
```
modelBuilder.Entity<Diagnosis>(entity => {
    entity.Property(d => d.DiagnosisCode).HasMaxLength(20).IsRequired(false);
    ...
});
```

`IsRequired(false)` явно позначає стовпець як nullable — без нього EF може вимагати NOT NULL.

**Ключові питання:**
- Що означає `HasDiscriminator` для абстрактного базового класу? Чи потрібно `HasValue<MedicalRecord>("Base")`?
- Чому всі поля підтипів в одній таблиці є nullable?

---

### Завдання 3. Owned Entity: EmergencyContact

**Задача:** додати до пацієнта контактну особу як Owned Entity.

Створіть клас `EmergencyContact`:
```
Name         — ім'я контактної особи
Phone        — телефон
Relationship — хто вона для пацієнта (Дружина / Мати / Брат...)
```

Цей клас:
- не має `Id`
- не має власної таблиці
- існує тільки як частина Patient

**Власник (Patient):**
```csharp
public EmergencyContact? EmergencyContact { get; set; }
```

`?` — контакт не обов'язковий. Якщо `null` — в БД стовпці EC_* рівні NULL.

**Fluent API:**
```
modelBuilder.Entity<Patient>().OwnsOne(p => p.EmergencyContact, ec =>
{
    ec.Property(e => e.Name).HasColumnName("EC_Name").HasMaxLength(100);
    ec.Property(e => e.Phone).HasColumnName("EC_Phone").HasMaxLength(20);
    ec.Property(e => e.Relationship).HasColumnName("EC_Relationship").HasMaxLength(50);
});
```

Перевірте після міграції: чи є нові стовпці EC_* у таблиці Patients?

**Відмінності OwnsOne vs ValueConverter:**

| | `ValueConverter<TModel, TProvider>` | `OwnsOne` |
|---|---|---|
| Стовпців | 1 | N (по одному на поле) |
| Пошук | `WHERE WorkSchedule = '8-17'` | `WHERE EC_Name LIKE '%...%'` |
| Типи | Один серіалізований | Рідні SQL типи |
| Приклад | WorkSchedule → "8-17" | EmergencyContact → 3 стовпці |

**Ключові питання:**
- Чи можна зробити `OwnsMany` (колекцію Owned Entity)? Що EF робить з таблицею?
- Що відбудеться якщо встановити `patient.EmergencyContact = null` і зберегти?

---

### Завдання 4. Concurrency Token та DbSeeder

**Задача:** додати захист від паралельного редагування та наповнити БД медичними записами.

**RowVersion:**

Додайте до `Patient`:
```csharp
public byte[]? RowVersion { get; private set; }
```

Fluent API:
```
modelBuilder.Entity<Patient>()
    .Property(p => p.RowVersion)
    .IsRowVersion();
```

`IsRowVersion()` — це поєднання трьох налаштувань:
1. Тип `timestamp` / `rowversion` у SQL Server
2. `IsConcurrencyToken = true` — EF включає у WHERE при UPDATE
3. `ValueGeneratedOnAddOrUpdate = true` — SQL Server оновлює автоматично

**Демонстрація конфліктів:**

Напишіть метод (або тест) що симулює конфлікт:
```
1. Завантажити patient через context1
2. Завантажити той самий patient через context2
3. context1.SaveChanges() — успішно
4. context2.SaveChanges() — DbUpdateConcurrencyException
```

Зверніть увагу: потрібні два окремих `DbContext` екземпляри для симуляції двох сесій.

**DbSeeder — медичні записи:**

Додайте `SeedMedicalRecords(context)`:
- `Diagnosis`: принаймні один хронічний і один звичайний
- `LabResult`: один аналіз в нормі, один поза нормою
- `Prescription`: один активний рецепт

Важливо: медичні записи потребують реальних `PatientId` і `DoctorId` з БД. Порядок виклику: `SeedPatients` → `SeedDoctors` → `SeedAppointments` → `SeedMedicalRecords`.

**Ключові питання:**
- Що таке Optimistic Concurrency (оптимістичне блокування)? Чим відрізняється від Pessimistic (блокування рядка)?
- Коли використовувати RowVersion, а коли — `[ConcurrencyCheck]` на окремому полі?
- Що зробити у `catch (DbUpdateConcurrencyException)`? Перезавантажити дані чи інформувати користувача?

---

## Рефлексійні питання

1. **TPH nullable fields.** У таблиці MedicalRecords стовпці `DiagnosisCode`, `TestName`, `MedicationName` — nullable. Це "порушення" 1НФ (перша нормальна форма)? Чи це прийнятний компроміс?

2. **TPT як альтернатива.** Table Per Type: Diagnosis в `Diagnoses`, LabResult в `LabResults` тощо — немає NULL. Але при завантаженні `MedicalRecord[]` — JOIN для кожного підтипу. Де TPH краще? Де TPT?

3. **Owned Entity і агрегати.** EmergencyContact — це Value Object у термінах DDD (Domain-Driven Design). Що означає "value object"? Як воно відрізняється від Entity?

4. **RowVersion і репліка.** Якщо БД реплікована (primary + replica), `rowversion` не синхронізується між серверами. Як вирішити проблему конкурентного доступу в такому середовищі?

5. **Soft delete.** Замість видалення запису — встановити `IsDeleted = true`. Як це реалізувати в EF Core так, щоб всі запити автоматично фільтрували видалені записи?

6. **Validation in setters vs DB constraints.** Setter `DiagnosisCode` валідує не-порожній рядок. А в БД — nullable column. Хто відповідає за якість даних — C# код чи схема БД? Або обоє?
