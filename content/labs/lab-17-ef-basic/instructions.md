# Лабораторна робота 17 — Entity Framework Core: основи

## Проблема

До цього моменту клініка існує тільки в оперативній пам'яті: кожен запуск програми починається з нуля. Усі введені пацієнти, лікарі та прийоми зникають після завершення сесії.

Як тільки програма набуває реального застосування, з'являються очевидні вимоги:
- дані мають зберігатися між сесіями
- кілька користувачів мають бачити одні й ті самі записи
- пошук і фільтрація мають масштабуватися до тисяч записів

Найпоширеніше рішення — реляційна база даних. Але взаємодія з БД напряму (через SQL-запити) потребує значних зусиль: формування рядків-запитів, ручне відображення ResultSet у C#-об'єкти, відстеження змін. Object-Relational Mapper (ORM) автоматизує цю роботу.

**Entity Framework Core** — офіційний ORM від Microsoft для .NET. Він дозволяє:
- описати структуру таблиць звичайними C#-класами
- не писати SQL вручну — він генерується автоматично
- відстежувати які об'єкти змінилися і формувати `UPDATE` тільки для них
- управляти схемою БД через **міграції** — версійовані скрипти змін

---

## Ключові концепції

### DbContext

`DbContext` — центральний клас EF Core. Він грає роль **посередника** між вашим C#-кодом і базою даних:

```
Program.cs → Manager → DbContext → SQL Server
```

Кожен `DbContext`:
- підключається до БД (`OnConfiguring`)
- оголошує які таблиці існують (`DbSet<T>`)
- описує правила відображення класів у таблиці (`OnModelCreating`)
- **відстежує зміни** об'єктів: EF пам'ятає початковий стан кожного об'єкта і під час `SaveChanges()` порівнює з поточним — формуючи `INSERT`, `UPDATE` або `DELETE`

### DbSet&lt;T&gt;

`DbSet<Patient>` — це "таблиця" в термінах C#. LINQ-запити до `DbSet` EF перетворює на SQL:

```csharp
// C# LINQ
context.Patients.Where(p => p.LastName == "Коваль").ToList()
// ↓ EF генерує
// SELECT * FROM Patients WHERE LastName = 'Коваль'
```

### Fluent API

Правила відображення можна задавати двома способами:
1. **Data Annotations** — атрибути прямо на класах: `[Required]`, `[MaxLength(100)]`
2. **Fluent API** — конфігурація в `OnModelCreating` через ланцюжок викликів

Fluent API має вищий пріоритет і тримає конфігурацію БД **окремо від моделі** — модель залишається чистим доменним об'єктом. Саме цей підхід використовується в Lab 17.

### Value Conversion

Деякі C#-типи не мають прямого відповідника у SQL. Наприклад:
- `enum BloodType` — в C# це `int`, але в БД краще зберігати як `"APositive"` (читабельно, не ламається при зміні порядку values)
- `struct WorkSchedule` — складений об'єкт, але логічно це один стовпець

EF Core дозволяє визначити **конвертер**: як серіалізувати тип у SQL і десеріалізувати назад. `WorkSchedule { Start=8, End=17 }` → `"8-17"` → `new WorkSchedule(8, 17)`.

### Міграції

База даних не знає про ваші C#-класи. Міграція — це **автоматично згенерований C#-клас**, що описує зміни схеми БД. Workflow:

```
Змінив модель → dotnet ef migrations add Назва → dotnet ef database update
```

`migrations add` аналізує різницю між поточними моделями та попередньою міграцією і генерує `Up()` / `Down()` методи. `database update` виконує `Up()` — застосовує зміни до БД.

---

## Завдання

### Завдання 1. Встановлення пакетів та DbContext

**Задача:** підключити EF Core до проєкту та налаштувати з'єднання з локальною БД.

EF Core складається з кількох NuGet-пакетів. Базовий пакет `Microsoft.EntityFrameworkCore` не знає ні про SQL Server, ні про SQLite — він лише оголошує абстракції. Конкретний провайдер бази даних — це окремий пакет.

Встановіть три пакети через `dotnet add package`:
- `Microsoft.EntityFrameworkCore` — ядро ORM
- `Microsoft.EntityFrameworkCore.SqlServer` — провайдер SQL Server / LocalDB
- `Microsoft.EntityFrameworkCore.Design` — інструменти для генерації міграцій (потрібен тільки під час розробки)

**Алгоритм:**
1. Перевірте версію .NET у `ClinicApp.csproj` — для `net8.0` підходить EF Core `8.0.x`
2. Встановіть пакети з конкретною версією (`--version 8.0.0`)
3. Переконайтеся що `<PackageReference>` з'явився у `.csproj`

Після встановлення створіть папку `src/Data/` і в ній клас `ClinicDbContext : DbContext`.

**Ключові питання:**
- Що робить `OnConfiguring`? Яку роль він грає в циклі роботи DbContext?
- Чому `UseSqlServer` — це метод розширення, а не частина базового EF Core?

**Рядок підключення до LocalDB:**
```
Server=(localdb)\mssqllocaldb;Database=ClinicApp;Trusted_Connection=True;TrustServerCertificate=True;
```

LocalDB — вбудований SQL Server для розробки. Не потребує окремого встановлення сервера; автоматично запускається при першому підключенні.

---

### Завдання 2. Fluent API для Patient

**Задача:** описати правила відображення класу `Patient` у таблицю `Patients`.

Перш ніж EF може працювати з моделлю, слід вирішити дві проблеми:

**Проблема 1: Id readonly**
Поле `public int Id { get; }` в `Patient` не може бути встановлено ззовні — тобто EF Core після `INSERT` не зможе записати у властивість нове значення з БД. Потрібно дозволити EF встановлювати Id, зберігши при цьому публічний API (ніхто ззовні не повинен мати можливість змінити Id вручну).

Підказка: `private set` вирішує обидва вимоги одночасно.

**Проблема 2: Конструктор**
EF Core при завантаженні об'єкта з БД викликає конструктор і потім встановлює властивості через setters. Якщо конструктор без параметрів вже є в класі — EF використає його. Якщо ні — потрібен захищений (`protected`) або приватний parameterless ctor.

Перевірте: чи є в `Patient` вже `public Patient()`? Якщо так — EF Core вже може його використати.

**Fluent API в `OnModelCreating`:**

Структура конфігурації для сутності:
```
modelBuilder.Entity<Patient>(entity =>
{
    entity.ToTable("...");
    entity.HasKey(p => p.Id);
    entity.Property(p => p.Id).ValueGeneratedOnAdd();
    entity.Property(p => p.FirstName).HasMaxLength(100).IsRequired();
    // ...
    entity.HasIndex(p => p.LastName).HasDatabaseName("IX_Patients_LastName");
});
```

`ValueGeneratedOnAdd` вказує: "БД генерує значення при INSERT" — це і є IDENTITY у SQL Server.

Для `BloodType` (enum): збережіть як рядок `HasConversion<string>()`. Без цього EF зберігатиме ціле число (0, 1, 2...) — при зміні порядку enum-значень дані в БД стануть некоректними.

**Ключові питання:**
- Навіщо явно вказувати `HasKey`, якщо EF і так знаходить `Id` за іменем?
- Що відбувається з `_nextId` статичним лічильником при завантаженні об'єктів з БД?

---

### Завдання 3. Fluent API для Doctor (Value Conversion)

**Задача:** описати відображення `Doctor` з особливою увагою до `WorkSchedule`.

`WorkSchedule` — це `struct` з двома полями `Start` і `End`. Зберігати його в окремій таблиці (через JOIN) надлишково для такої простої структури. Натомість використаємо **Value Conversion**: серіалізуємо в рядок `"8-17"` і десеріалізуємо назад.

**Проблема:** `HasConversion` приймає лямбди, але компілює їх у **expression trees** — обмежений підмножина C#. Expression trees не підтримують виклики методів з опціональними параметрами (обмеження CS0854).

Зокрема, `int.Parse(string)` технічно має опціональний параметр `IFormatProvider`, тому він може не компілюватися в expression tree залежно від версії .NET.

**Рішення:** винести парсинг у статичний метод класу та передати делегат:

```csharp
private static WorkSchedule ParseWorkSchedule(string value)
{
    string[] parts = value.Split('-');
    return new WorkSchedule(int.Parse(parts[0]), int.Parse(parts[1]));
}
```

Після чого конвертер:
```csharp
var converter = new ValueConverter<WorkSchedule, string>(
    s => s.Start.ToString() + "-" + s.End.ToString(),
    v => ParseWorkSchedule(v));
entity.Property(d => d.Schedule).HasConversion(converter)...
```

Чому `ValueConverter<TModel, TProvider>` замість прямих лямбд у `HasConversion`? Тому що `new ValueConverter<>()` отримує `Func<,>` делегати, а не виразові дерева — немає обмежень CS0854.

**Ключові питання:**
- У чому різниця між expression tree і `Func<>` делегатом?
- Чи можна замість рядка "8-17" зберігати два окремі стовпці? Як би виглядала конфігурація?

---

### Завдання 4. DbSeeder та перший запуск

**Задача:** створити початкові дані та запустити міграцію.

**DbSeeder:**

Тестові дані — окремий клас, а не в `Program.cs`. Причина: `Program.cs` керує навігацією, DbSeeder відповідає за початковий стан БД — це різні обов'язки (SRP).

Ключовий принцип: **ідемпотентність**. Seeder повинен безпечно запускатися при кожному старті:

```
if (context.Patients.Any()) return;  // вже є дані — пропускаємо
```

`Any()` генерує `SELECT TOP 1 FROM Patients` — не завантажує всі записи, лише перевіряє наявність хоча б одного.

Додайте 5 пацієнтів і 5 лікарів різних спеціальностей.

**Міграція:**

Після того як `ClinicDbContext` написано і проєкт компілюється, запустіть:
```
dotnet ef migrations add InitialCreate
dotnet ef database update
```

Перша команда генерує клас `InitialCreate` у папці `Migrations/` — відкрийте його і прочитайте метод `Up()`. Що він створює? Чи відповідає структура таблиць вашій Fluent API конфігурації?

**Алгоритм виклику Seeder:**

У `Program.cs` або `Clinic.cs` при старті:
```
using var context = new ClinicDbContext();
context.Database.EnsureCreated(); // або вже зроблено через database update
DbSeeder.Seed(context);
```

Після запуску перевірте через SQL Server Object Explorer (у Visual Studio) або виконайте `SELECT * FROM Patients` у команді — дані мають бути в БД.

**Ключові питання:**
- Що робить `context.Database.EnsureCreated()` порівняно з `dotnet ef database update`?
- Навіщо `using var context = ...`? Що відбудеться якщо не dispose контекст?
- Чому краще використовувати `Any()` замість `Count() == 0` для перевірки?

---

## Рефлексійні питання

1. **DbContext як Unit of Work.** EF Core реалізує патерн Unit of Work — всі зміни в одній "одиниці роботи" зберігаються разом через `SaveChanges()`. Як це пов'язано з принципом транзакцій у БД?

2. **Fluent API vs Data Annotations.** Data Annotations (`[Required]`, `[MaxLength]`) — простіші, але змішують доменну модель з технічними деталями БД. Fluent API складніший, але чистіший. В якому випадку ви б вибрали Annotations?

3. **Value Conversion і validatior.** `WorkSchedule` конструктор валідує `start < end`. Що відбудеться, якщо в БД є рядок "17-8" (corrupted data)? Як захиститися від цього?

4. **IDENTITY vs клієнтський Id.** Зараз `_nextId` в Patient і DB IDENTITY — два незалежних лічильники. Після `SaveChanges()` EF оновлює Id з БД. Що станеться з `_nextId`, якщо завтра в БД вже 1000 записів і програма стартує з нуля?

5. **Міграція як версія схеми.** Міграція — це як Git для структури БД. Що буде, якщо один розробник застосує міграцію `AddAppointments`, а інший ще ні — і обидва намагаються запустити додаток з однієї БД?

6. **`using var context`.** Чому `DbContext` реалізує `IDisposable`? Що відбувається під час `Dispose()` — чи зберігаються незбережені зміни?
