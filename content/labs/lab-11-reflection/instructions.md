# Лаба 11 — Reflection & Attributes (Рефлексія та атрибути)

## Мета

Навчитись створювати власні атрибути через спадкування від `System.Attribute`, зчитувати метадані типів та властивостей через рефлексію, і будувати узагальнені утиліти (валідатор, конструктор форм), що працюють з **будь-яким класом** без явного знання про його поля.

## Контекст

Після Lab 10 система має повний аналітичний модуль. Ця лаба додає **плани лікування** (`TreatmentPlan`) як новий тип даних та інструментарій рефлексії, що перевіряє валідність об'єктів і генерує форми введення **автоматично** — зчитуючи атрибути з властивостей класу під час виконання.

## Гілка

```bash
git checkout main
git pull
git checkout -b feature/reflection
```

---

## Завдання 1 — Власні атрибути ⭐

### Умова

Потрібно описати правила валідації безпосередньо в класі — над кожною властивістю. Замість `System.ComponentModel.DataAnnotations` пишемо атрибути **з нуля**.

### Що реалізувати

Створіть теку `src/Attributes/` і три файли:

**`Attributes/RequiredAttribute.cs`**

```csharp
namespace YourApp.Attributes;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class RequiredAttribute : Attribute
{
    public string ErrorMessage { get; }
    public RequiredAttribute(string errorMessage = "Field is required.") { ... }
}
```

**`Attributes/MaxLengthAttribute.cs`**

```csharp
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class MaxLengthAttribute : Attribute
{
    public int Length { get; }
    public string ErrorMessage { get; }
    public MaxLengthAttribute(int length, string errorMessage = "") { ... }
}
```

**`Attributes/MinValueAttribute.cs`**

```csharp
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class MinValueAttribute : Attribute
{
    public double Min { get; }
    public string ErrorMessage { get; }
    public MinValueAttribute(double min, string errorMessage = "") { ... }
}
```

### Підказки

1. Атрибут — це клас, що наслідує `System.Attribute`. Компілятор дозволяє опускати суфікс `Attribute` при застосуванні: `[MaxLength(200)]` замість `[MaxLengthAttribute(200)]`.
2. `[AttributeUsage]` обмежує де атрибут можна поставити. `AttributeTargets.Property` — тільки на властивостях.
3. `AllowMultiple = false` означає: один атрибут одного типу на одну властивість.
4. `sealed` — атрибут не підлягає подальшому спадкуванню (хороша практика).
5. [Attribute — C# docs](https://learn.microsoft.com/en-us/dotnet/csharp/advanced-topics/reflection-and-attributes/creating-custom-attributes)
6. [AttributeUsage — C# docs](https://learn.microsoft.com/en-us/dotnet/api/system.attributeusageattribute)

### Адаптація до вашого домену

| Клініка | Готель | Ресторан | Університет | Прокат авто | Бібліотека | Спортзал |
|---------|--------|----------|-------------|-------------|------------|---------|
| `RequiredAttribute` | `RequiredAttribute` | `RequiredAttribute` | `RequiredAttribute` | `RequiredAttribute` | `RequiredAttribute` | `RequiredAttribute` |
| `MaxLengthAttribute` на `Diagnosis`, `Treatment` | `MaxLengthAttribute` на `GuestNote`, `RoomType` | `MaxLengthAttribute` на `DishNote`, `Allergens` | `MaxLengthAttribute` на `CourseName`, `Description` | `MaxLengthAttribute` на `VehicleModel`, `Notes` | `MaxLengthAttribute` на `BookTitle`, `ReturnNote` | `MaxLengthAttribute` на `ExercisePlan`, `Notes` |
| `MinValueAttribute` на `DurationDays` | `MinValueAttribute` на `StayNights` | `MinValueAttribute` на `GuestCount` | `MinValueAttribute` на `Credits` | `MinValueAttribute` на `RentalDays` | `MinValueAttribute` на `OverdueDays` | `MinValueAttribute` на `DurationMinutes` |

### Коміт

```bash
git add src/Attributes/
git commit -m "Lab11 Task1: add RequiredAttribute, MaxLengthAttribute, MinValueAttribute"
```

---

## Завдання 2 — Модель із атрибутами ⭐⭐

### Умова

Потрібна нова сутність, властивості якої описані атрибутами з Завдання 1.

### Що реалізувати

**`Enums/TreatmentStatus.cs`**:

```csharp
public enum TreatmentStatus { Planned, Active, Completed, Cancelled }
```

**`Models/TreatmentPlan.cs`** — клас із атрибутами на властивостях:

```csharp
using YourApp.Attributes;

public class TreatmentPlan
{
    private static int _nextId = 1;
    public int Id { get; }

    [Required("Patient ID is required.")]
    public int PatientId { get; set; }

    [Required("Diagnosis cannot be empty.")]
    [MaxLength(200, "Diagnosis must not exceed 200 characters.")]
    public string Diagnosis { get; set; } = "";

    [MinValue(1, "Duration must be at least 1 day.")]
    public int DurationDays { get; set; }

    public TreatmentStatus Status { get; private set; } = TreatmentStatus.Planned;
    // ... та інші поля
}
```

Додати методи стану:
- `bool Activate()` — переводить `Planned → Active`
- `bool Complete()` — переводить `Active → Completed`
- `bool Cancel()` — скасовує якщо не `Completed` / `Cancelled`

### Підказки

1. Атрибути — це метадані. Вони не виконують перевірку самі по собі. Перевірку виконає `ModelValidator` у наступному завданні.
2. Можна ставити **кілька різних** атрибутів на одну властивість: `[Required]` і `[MaxLength(200)]` — обидва будуть прочитані рефлексією.
3. `private set` для `Status` — стан змінюється тільки через методи, зберігаючи інваріант переходів.
4. [Custom attributes usage — C# docs](https://learn.microsoft.com/en-us/dotnet/csharp/advanced-topics/reflection-and-attributes/attribute-parameter-types)

### Адаптація до вашого домену

| Клініка | Готель | Ресторан | Університет | Прокат авто | Бібліотека | Спортзал |
|---------|--------|----------|-------------|-------------|------------|---------|
| `TreatmentPlan` | `ServiceRequest` | `SpecialOrder` | `CourseProject` | `ServiceOrder` | `RestorationRequest` | `TrainingProgram` |
| `TreatmentStatus` | `ServiceStatus` | `OrderStatus` | `ProjectStatus` | `ServiceStatus` | `RestorationStatus` | `ProgramStatus` |
| `Activate/Complete/Cancel` | `Start/Finish/Cancel` | `Confirm/Serve/Cancel` | `Start/Submit/Withdraw` | `Start/Return/Cancel` | `Begin/Finish/Cancel` | `Begin/Complete/Cancel` |

### Коміт

```bash
git add src/Enums/TreatmentStatus.cs src/Models/TreatmentPlan.cs
git commit -m "Lab11 Task2: add TreatmentStatus enum and TreatmentPlan model with attributes"
```

---

## Завдання 3 — ModelValidator через рефлексію ⭐⭐⭐

### Умова

Написати статичний клас, що **автоматично** перевіряє будь-який об'єкт — зчитуючи атрибути з його властивостей через рефлексію, без жодного `if (plan.Diagnosis == null)` у коді валідатора.

### Що реалізувати

**`Utils/ValidationResult.cs`** — контейнер помилок:

```csharp
public class ValidationResult
{
    private readonly List<string> _errors = new();
    public bool IsValid => _errors.Count == 0;
    public IReadOnlyList<string> Errors => _errors;
    public void AddError(string error) => _errors.Add(error);
    public void Print() { foreach (var e in _errors) Console.WriteLine($"  [!] {e}"); }
}
```

**`Utils/ModelValidator.cs`** — серце рефлексії:

```csharp
using System.Reflection;
using YourApp.Attributes;

public static class ModelValidator
{
    public static ValidationResult Validate(object obj)
    {
        var result = new ValidationResult();
        var type = obj.GetType();

        foreach (var prop in type.GetProperties())
        {
            var value = prop.GetValue(obj);

            var required = prop.GetCustomAttribute<RequiredAttribute>();
            if (required != null)
            {
                if (value is null || (value is string s && string.IsNullOrWhiteSpace(s)))
                    result.AddError($"{prop.Name}: {required.ErrorMessage}");
            }

            // перевірка MaxLength та MinValue — аналогічно
        }

        return result;
    }
}
```

Також додати:

```csharp
public static void PrintInfo(Type type)
{
    // виводить назву типу та список властивостей з їх атрибутами
}
```

### Підказки

1. `obj.GetType()` — тип конкретного об'єкта під час виконання. Відрізняється від `typeof(T)` — статичного типу, відомого на час компіляції.
2. `type.GetProperties()` — масив `PropertyInfo[]`. Кожен `PropertyInfo` описує одну властивість: ім'я, тип, атрибути.
3. `prop.GetValue(obj)` — зчитує значення властивості з конкретного об'єкта. Повертає `object?`.
4. `prop.GetCustomAttribute<RequiredAttribute>()` — шукає атрибут цього типу. Якщо не знайдено — `null`.
5. `prop.GetCustomAttributes()` — усі атрибути на властивості (для виводу в `PrintInfo`).
6. [Reflection — C# docs](https://learn.microsoft.com/en-us/dotnet/csharp/advanced-topics/reflection-and-attributes/)
7. [PropertyInfo.GetValue — docs](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.propertyinfo.getvalue)
8. [GetCustomAttribute — docs](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.customattributeextensions.getcustomattribute)

### Адаптація до вашого домену

| Клініка | Готель | Ресторан | Університет | Прокат авто | Бібліотека | Спортзал |
|---------|--------|----------|-------------|-------------|------------|---------|
| Валідує `TreatmentPlan` | Валідує `ServiceRequest` | Валідує `SpecialOrder` | Валідує `CourseProject` | Валідує `ServiceOrder` | Валідує `RestorationRequest` | Валідує `TrainingProgram` |
| `ModelValidator.Validate(plan)` | однаково | однаково | однаково | однаково | однаково | однаково |

### Коміт

```bash
git add src/Utils/ValidationResult.cs src/Utils/ModelValidator.cs
git commit -m "Lab11 Task3: add ValidationResult and ModelValidator with reflection"
```

---

## Завдання 4 — TreatmentPlanManager та FormBuilder ⭐⭐⭐

### Умова

Підключити валідацію до менеджера та написати generic утиліту, що будує консольну форму введення автоматично — за атрибутами на властивостях.

### Що реалізувати

**`Managers/TreatmentPlanManager.cs`**:

```csharp
public class TreatmentPlanManager
{
    private readonly List<TreatmentPlan> _plans = new();

    public bool Add(TreatmentPlan plan)
    {
        var validation = ModelValidator.Validate(plan);
        if (!validation.IsValid) { validation.Print(); return false; }
        _plans.Add(plan);
        return true;
    }

    public TreatmentPlan? GetById(int id) => ...
    public TreatmentPlan[] GetByPatient(int patientId) => ...
    public TreatmentPlan[] GetByStatus(TreatmentStatus status) => ...
    public TreatmentPlan[] GetAll() => ...
}
```

**`Utils/FormBuilder.cs`** — generic метод що читає атрибути й ставить питання:

```csharp
public static class FormBuilder
{
    public static T Build<T>() where T : new()
    {
        var obj = new T();
        var type = typeof(T);

        foreach (var prop in type.GetProperties().Where(p => p.CanWrite))
        {
            // прочитати атрибути для підказки
            // Console.Write($"  {prop.Name}{hint}: ")
            // Console.ReadLine() → Convert.ChangeType() → prop.SetValue(obj, converted)
        }

        return obj;
    }
}
```

Зверніть увагу: `typeof(T)` — тут `T` відомий на час компіляції. `prop.SetValue(obj, converted)` — рефлексія дозволяє встановити значення через `PropertyInfo`.

### Підказки

1. `where T : new()` — обмеження: тип `T` повинен мати конструктор без параметрів. Без цього `new T()` не компілюється.
2. `typeof(T)` vs `obj.GetType()` — обидва дають `Type`, але `typeof(T)` статичний (час компіляції), `obj.GetType()` — динамічний (час виконання). У `FormBuilder` вони дають однаковий результат, бо `T` відомий.
3. `prop.CanWrite` — перевіряє чи є публічний setter. Пропускаємо `Id` та інші readonly властивості.
4. `Convert.ChangeType(input, prop.PropertyType)` — конвертує рядок у потрібний тип (`int`, `decimal`, тощо). Обгорніть у `try/catch`.
5. `prop.SetValue(obj, converted)` — записує значення у властивість через рефлексію.
6. [PropertyInfo.SetValue — docs](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.propertyinfo.setvalue)
7. [typeof operator — C# docs](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/type-testing-and-cast#typeof-operator)

### Адаптація до вашого домену

| Клініка | Готель | Ресторан | Університет | Прокат авто | Бібліотека | Спортзал |
|---------|--------|----------|-------------|-------------|------------|---------|
| `TreatmentPlanManager` | `ServiceRequestManager` | `SpecialOrderManager` | `CourseProjectManager` | `ServiceOrderManager` | `RestorationManager` | `TrainingProgramManager` |
| `FormBuilder.Build<TreatmentPlan>()` | `FormBuilder.Build<ServiceRequest>()` | однаково | однаково | однаково | однаково | `FormBuilder.Build<TrainingProgram>()` |

### Коміт

```bash
git add src/Managers/TreatmentPlanManager.cs src/Utils/FormBuilder.cs
git commit -m "Lab11 Task4: add TreatmentPlanManager and generic FormBuilder"
```

---

## Завдання 5 — Інтеграція та меню "Плани лікування" ⭐⭐

### Умова

Підключити `TreatmentPlanManager` до системи та показати роботу рефлексії через консоль.

### Що реалізувати

**`Clinic.cs`** — додати:

```csharp
public TreatmentPlanManager TreatmentPlans { get; }
// у конструкторі:
TreatmentPlans = new TreatmentPlanManager();
```

**`Program.cs`** — нове підменю "Плани лікування":

```
── Плани лікування ────────────────────
  1. Показати всі плани
  2. Додати план лікування
  3. Плани пацієнта
  4. Активувати план
  5. Завершити план
  6. Скасувати план
  7. Інформація про тип TreatmentPlan
  0. Назад
```

Пункт "7. Інформація про тип" — виклик `ModelValidator.PrintInfo(typeof(TreatmentPlan))`. Він виводить усі властивості класу та їх атрибути — без жодного екземпляра об'єкта.

Головне меню — новий пункт:

```
║  9. Плани лікування — рефлексія, атрибути   ║
```

### Підказки

1. Пункт 2 меню використовує `FormBuilder.Build<TreatmentPlan>()` — форма генерується автоматично.
2. `ModelValidator.PrintInfo(typeof(TreatmentPlan))` — передається `Type`, а не об'єкт. Рефлексія працює і без екземпляра.
3. Пункт 7 — гарна демонстрація для студентів: як рефлексія читає структуру класу під час виконання.

### Адаптація до вашого домену

| Клініка | Готель | Ресторан | Університет | Прокат авто | Бібліотека | Спортзал |
|---------|--------|----------|-------------|-------------|------------|---------|
| `TreatmentPlansMenu` | `ServiceRequestsMenu` | `SpecialOrdersMenu` | `ProjectsMenu` | `ServiceOrdersMenu` | `RestorationMenu` | `ProgramsMenu` |
| `FormBuilder.Build<TreatmentPlan>()` | `FormBuilder.Build<ServiceRequest>()` | однаково | однаково | однаково | однаково | `FormBuilder.Build<TrainingProgram>()` |

### Коміт

```bash
git add src/Clinic.cs src/Program.cs
git commit -m "Lab11 Task5: integrate TreatmentPlanManager, add TreatmentPlansMenu"
```

---

## Перевірка перед здачею

```bash
cd src
dotnet build
dotnet run
```

Переконайтесь, що:

- [ ] Атрибути `RequiredAttribute`, `MaxLengthAttribute`, `MinValueAttribute` — власні класи, що наслідують `Attribute`
- [ ] `[AttributeUsage(AttributeTargets.Property)]` присутній на кожному атрибуті
- [ ] `TreatmentPlan` має мінімум 3 властивості з різними атрибутами
- [ ] `ModelValidator.Validate(plan)` повертає помилки без жодного `if (plan.X == null)` у логіці валідатора
- [ ] `FormBuilder.Build<TreatmentPlan>()` виводить назви полів та підказки з атрибутів — не жорстко закодовані рядки
- [ ] Пункт "Інформація про тип" виводить список властивостей та атрибутів через `typeof(TreatmentPlan)`
- [ ] Спроба додати план без обов'язкових полів — виводить повідомлення про помилку, не крах

---

## Питання для самоперевірки

1. Чим відрізняється `obj.GetType()` від `typeof(T)`? Коли кожен з них корисний?
2. Чому `FormBuilder` оголошено як `static class` з `static T Build<T>() where T : new()`? Що б змінилось без `where T : new()`?
3. `ModelValidator.Validate()` приймає `object` — не `TreatmentPlan`. Що це дає? Які об'єкти ти міг би ще провалідувати без змін у коді валідатора?
4. Якби атрибути були з `System.ComponentModel.DataAnnotations` — чи відрізнявся б код `ModelValidator`? Що саме змінилось би?
5. `prop.GetCustomAttributes()` повертає `IEnumerable<Attribute>`. Чому не `object[]`?
6. `Convert.ChangeType(input, prop.PropertyType)` кидає виняток якщо `prop.PropertyType` — enum або nullable. Як це виправити?

---

## Злиття

```bash
git checkout main
git merge --no-ff feature/reflection -m "Merge feature/reflection: Lab11 Reflection & Attributes"
git push
```

> Наступна лаба: `git checkout -b feature/linq` — LINQ: `Where`, `Select`, `OrderBy`, `GroupBy`, `First/FirstOrDefault`, `Sum/Average`, `Any/All`.
