# Лабораторна робота №13. Events & Delegates

## Мета

Зрозуміти проблему жорсткого зв'язування між класами та навчитись її вирішувати через механізм подій. Опанувати `delegate`, `EventArgs`, `event EventHandler<T>`, підписку через `+=` і побудову системи де компоненти реагують на зміни **не знаючи один про одного**.

## Гілка

```
feature/events
```

---

## Проблема, яку вирішує ця лаба

Відкрийте `src/Program.cs` і подивіться на будь-який пункт меню. Після кожної дії ви побачите щось подібне:

```csharp
clinic.Appointments.Book(patientId, doctorId, scheduledAt);
clinic.Logger.LogInfo($"Запис створено: пацієнт {patientId}...");
```

Тобто **кожна дія в меню вручну повідомляє Logger**. Зараз Logger один — але що якщо потрібно ще й записати у файл статистики? Або оновити паспорт пацієнта? Тоді після кожної дії буде три рядки, потім чотири, потім п'ять.

Це **жорстке зв'язування** — `Program.cs` знає про `Logger`, `PassportWriter`, `Tracker` і повинен викликати кожен з них вручну. Якщо додати нового підписника — доведеться змінювати `Program.cs`.

**Правильно** було б щоб менеджер просто **повідомляв**: "запис створено". А всі зацікавлені слухачі реагують самі — незалежно, не знаючи одне про одного. Саме це і є **патерн Publisher-Subscriber**, реалізований через механізм подій у C#.

---

## Завдання 1. Перша подія — зрозуміти механіку ⭐⭐

### Що зараз не так

`AppointmentManager.Book()` створює запис — але нікому про це не повідомляє. `Program.cs` мусить сам писати у лог після кожного виклику. Якщо десь забудеш — лог неповний.

### Що таке delegate і event

**Delegate** — це тип що описує сигнатуру методу. Думайте про нього як про "контракт обробника": "я очікую метод, що приймає `object? sender` і `AppointmentEventArgs e` і нічого не повертає".

`EventHandler<T>` — це вбудований у .NET delegate з саме такою сигнатурою. Вам не потрібно оголошувати delegate вручну — достатньо `EventHandler<T>` де `T` — ваш клас аргументів.

**Event** — це поле типу delegate з обмеженнями: ззовні класу дозволено лише `+=` і `-=`. Не можна присвоїти `= null` або викликати напряму. Це захищає від випадкового знищення всіх підписників.

### Що таке EventArgs

`EventArgs` — базовий клас для "посилки з даними про подію". Коли `AppointmentManager` повідомляє про створення запису, він передає `AppointmentEventArgs` з усіма деталями: id запису, id пацієнта, id лікаря, час. Підписник отримує цю посилку і робить з нею що потрібно.

### Що потрібно зробити

**Крок 1.** Створіть папку `src/Events/` і в ній файл `AppointmentEventArgs.cs`.

Цей клас успадковує `EventArgs` і містить readonly-властивості з даними про подію: `AppointmentId`, `PatientId`, `DoctorId`, `ScheduledAt`, `Notes`. Всі властивості заповнюються через конструктор — після створення об'єкт незмінний, бо подія вже відбулась.

Подумайте: чому `Notes` має значення за замовчуванням `""`? Коли воно може бути порожнім?

**Крок 2.** Відкрийте `src/Managers/AppointmentManager.cs`.

Додайте поле події:
```csharp
public event EventHandler<AppointmentEventArgs>? AppointmentBooked;
```

Знак `?` означає що підписників може не бути — і це нормально. Якщо не перевірити, виклик кине `NullReferenceException`.

Після успішного створення запису в методі `Book()` підніміть подію:
```csharp
AppointmentBooked?.Invoke(this, new AppointmentEventArgs(...));
```

`?.Invoke` — безпечний виклик: якщо немає підписників, просто нічого не відбувається.

**Крок 3.** Відкрийте `src/Program.cs`.

Підпишіть простий обробник ще **до** ініціалізації тестових даних:
```csharp
clinic.Appointments.AppointmentBooked += OnAppointmentBookedConsole;
```

Нижче (поза циклом меню) оголосіть статичний метод:
```csharp
static void OnAppointmentBookedConsole(object? sender, AppointmentEventArgs e)
{
    Console.WriteLine($"  [EVENT] Запис #{e.AppointmentId} створено...");
}
```

Зверніть увагу: `Program.cs` не викликає `Logger` — він просто "слухає" подію від менеджера.

**Перевірка.** Запустіть програму, запишіть пацієнта через меню. Рядок `[EVENT]` з'являється автоматично — без жодного виклику у коді меню.

### Ключові питання для розуміння

- Чому обробник має саме таку сигнатуру `(object? sender, T e)`? Що означає `sender`?
- Що станеться якщо написати `AppointmentBooked.Invoke(...)` без `?.`?
- Спробуйте написати `clinic.Appointments.AppointmentBooked = null` — чому компілятор не дозволяє?

---

## Завдання 2. Всі події + Logger як підписник ⭐⭐

### Чому Logger не повинен викликатись вручну

Подивіться на `Program.cs`: скрізь де є дія — є `clinic.Logger.LogInfo(...)`. Це означає що `Program.cs` **знає** про Logger і **пам'ятає** його викликати. Якщо хтось додасть новий пункт меню і забуде — дія не залогується.

**Правильніше**: нехай `AppointmentManager` підніме подію → Logger як підписник сам відреагує. Автоматично. Завжди. Без залежності від `Program.cs`.

### Що потрібно зробити

**Крок 1. Нові EventArgs.**

Аналогічно Task 1 створіть в `src/Events/`:
- `PatientEventArgs.cs` — `PatientId`, `FullName`
- `PaymentEventArgs.cs` — `AppointmentId`, `Amount`
- `TreatmentPlanEventArgs.cs` — `PlanId`, `PatientId`, `Diagnosis`

Поля беріть ті, що логічно описують "що сталося": мінімально достатньо для обробника.

**Крок 2. Нові події в менеджерах.**

В `AppointmentManager` додайте ще три події — `AppointmentCancelled`, `AppointmentCompleted`, `UrgentAppointmentBooked`. Підніміть їх у відповідних методах: `Cancel()`, `Complete()`, `BookUrgent()`.

Зверніть увагу: в `BookUrgent()` варто підняти **дві** події — `AppointmentBooked` і `UrgentAppointmentBooked`. Терміновий запис — це все одно запис, тому перша подія теж доречна. Logger підписаний на обидві і залогує обидві — це задумана поведінка.

В `PatientManager` — `PatientAdded` у методі `Add()`.

В `BillingManager` — `PaymentReceived` у методі `PayAppointment()`. Яку суму передавати в `PaymentEventArgs`? Подумайте: `GetCost()` доступний через `IPayable`.

В `TreatmentPlanManager` — `PlanCompleted`. Але зараз зміна статусу плану відбувається всередині самого `TreatmentPlan`. Вам потрібно додати метод-обгортку `Complete(int id)` у менеджері — саме він підніме подію після успішного завершення.

**Крок 3. Logger як підписник.**

Відкрийте `src/Utils/ClinicLogger.cs`. Додайте методи-обробники — по одному на кожну подію:

```csharp
public void OnPatientAdded(object? sender, PatientEventArgs e)
    => LogInfo($"Новий пацієнт #{e.PatientId}: {e.FullName}");
```

Аналогічно для всіх інших подій. Для `OnUrgentBooked` — використайте `LogWarning` і додатково запишіть у файл `alerts/urgent_{дата}.txt` через `File.AppendAllText`.

Підказка для методів що вже мали `LogInfo` у меню: після цього кроку ці ручні виклики в `Program.cs` **видаляються** — Logger сам знає коли логувати.

**Крок 4. Підписка в Clinic.cs.**

Відкрийте `src/Clinic.cs`. Додайте приватний метод `SubscribeEvents()` і викличте його в кінці конструктора.

Чому саме тут, а не в `Program.cs`? Тому що `Clinic` — це оркестратор системи. Саме він знає про всі менеджери і вирішує хто на що підписаний. `Program.cs` не повинен знати про внутрішні зв'язки між компонентами.

```csharp
private void SubscribeEvents()
{
    Patients.PatientAdded             += Logger.OnPatientAdded;
    Appointments.AppointmentBooked    += Logger.OnAppointmentBooked;
    // ... решта подій
}
```

**Крок 5. Прибрати ручні виклики Logger з Program.cs.**

Тепер всі `clinic.Logger.LogInfo(...)` що стосуються подій — зайві. Logger підписаний і реагує сам. Знайдіть і видаліть їх.

**Перевірка.** Два підписники на `AppointmentBooked`: ваш `OnAppointmentBookedConsole` з Task 1 і `Logger.OnAppointmentBooked`. При записі пацієнта — обидва спрацьовують. У консолі з'явиться рядок `[EVENT]`, у `clinic.log` — рядок від Logger. Менеджер не знає про жодного з них.

### Ключові питання для розуміння

- `AppointmentManager` не імпортує `ClinicLogger`. Де відбувається їх зв'язок?
- Чому Logger — підписник, а не навпаки (Logger не викликає менеджер)?
- `BookUrgent` піднімає дві події. Скільки рядків у лозі після одного `BookUrgent`? Чому?

---

## Завдання 3. PatientPassportWriter — другий незалежний підписник ⭐⭐⭐

### Нова вимога без зміни менеджерів

Уявіть: замовник каже "при реєстрації пацієнта і при кожному завершеному прийомі — генеруйте файл паспорту пацієнта". Скільки файлів треба змінити в поточній системі?

З подіями відповідь: **один новий файл** — `PatientPassportWriter`. Менеджери вже піднімають відповідні події. Потрібно лише підписати новий клас. `AppointmentManager`, `PatientManager`, `Program.cs` — жоден не змінюється.

Це і є головна перевага подій: **відкрита для розширення, закрита для змін**.

### Що таке паспорт пацієнта

Файл `patients/passport_{id}.txt` — повна картка пацієнта в текстовому форматі. Генерується заново при кожному тригері (простіше ніж відстежувати що змінилось). Містить:

- Особисті дані: ім'я, дата народження, вік, група крові, телефон
- Медичні записи: діагнози, аналізи, рецепти (з `MedicalRecordManager`)
- Записи на прийом (з `AppointmentManager`)
- Плани лікування (з `TreatmentPlanManager`)
- Фінансова заборгованість (з `BillingManager`)

### Що потрібно зробити

**Крок 1.** Створіть `src/Utils/PatientPassportWriter.cs`.

Клас отримує `Clinic` через конструктор — щоб мати доступ до всіх менеджерів при генерації файлу. Також приймає `baseDir` (за замовчуванням `"patients"`) — папка де зберігаються паспорти. Папку варто створити одразу в конструкторі через `Directory.CreateDirectory`.

Три публічні обробники, всі викликають один приватний метод `Write(int patientId)`:
```csharp
public void OnPatientAdded(object? sender, PatientEventArgs e)    => Write(e.PatientId);
public void OnAppointmentCompleted(object? sender, AppointmentEventArgs e) => Write(e.PatientId);
public void OnPlanCompleted(object? sender, TreatmentPlanEventArgs e)      => Write(e.PatientId);
```

Чому так? Тому що логіка генерації однакова — зібрати всі дані пацієнта і записати у файл. Не дублюйте цей код тричі.

**Крок 2.** Реалізуйте приватний метод `Write(int patientId)`.

Алгоритм:
1. Знайдіть пацієнта за ID. Якщо не знайдено — вийти (ранній вихід `return`).
2. Складіть шлях: `Path.Combine(_baseDir, $"passport_{patientId}.txt")`.
3. Відкрийте `StreamWriter` з `append: false` — **перезаписуємо** файл, не дописуємо. Паспорт завжди актуальний.
4. Запишіть кожну секцію.

Для секцій медичних записів використовуйте `is` з оголошенням змінної:
```csharp
if (records[i] is Diagnosis d) { /* записати d.DiagnosisCode, d.Description... */ }
```

Три окремих проходи по масиву для трьох секцій — простіше ніж один складний з кількома `if`.

**Крок 3.** Додайте `PatientPassportWriter` у `Clinic.cs`.

Оголосіть властивість `public PatientPassportWriter Passport { get; }`, ініціалізуйте в конструкторі. В `SubscribeEvents()` підпишіть:
```csharp
Patients.PatientAdded             += Passport.OnPatientAdded;
Appointments.AppointmentCompleted += Passport.OnAppointmentCompleted;
TreatmentPlans.PlanCompleted      += Passport.OnPlanCompleted;
```

**Перевірка.** Зареєструйте пацієнта → файл `patients/passport_N.txt` з'явився. Завершіть прийом → файл оновився: дата генерації змінилась, прийом тепер `Completed`. Logger при цьому також спрацював — обидва підписники незалежні.

### Ключові питання для розуміння

- `PatientPassportWriter` підписаний на події `PatientManager` і `AppointmentManager`. Чи знають ці менеджери про `PatientPassportWriter`?
- Чому `StreamWriter` з `append: false`, а не `append: true`?
- Якщо завтра замовник попросить "ще й надсилати email при завершенні прийому" — які файли потрібно змінити?

---

## Завдання 4. SessionEventTracker — крос-доменна реакція ⭐⭐⭐

### Що таке крос-доменна реакція

До цього кожен підписник реагував у своїй "зоні відповідальності": Logger пише у файл, PassportWriter генерує документ. Але інколи реакція на одну подію зачіпає інший підсистему.

Приклад: лікар скасував прийом → звільнився часовий слот → логічно перевірити чи є хтось у черзі і повідомити. Скасування — це подія `AppointmentManager`. Черга — це `WaitingQueue` у `Clinic`. Як їх зв'язати без прямої залежності між менеджерами?

Відповідь: `SessionEventTracker` підписаний на подію скасування і має доступ до `Clinic` — він і робить перевірку черги.

### Що потрібно зробити

**Крок 1.** Створіть `src/Utils/SessionEventTracker.cs`.

Клас отримує `Clinic` через конструктор. Зберігає лічильники для кожного типу події як `public int` з `private set`:
```
PatientsAdded, AppointmentsBooked, UrgentBooked,
AppointmentsCancelled, AppointmentsCompleted,
PaymentsReceived, PlansCompleted
```

**Крок 2.** Реалізуйте обробники.

Більшість обробників просто збільшують лічильник. Винятком є `OnAppointmentCancelled` — він також перевіряє чергу:

```csharp
public void OnAppointmentCancelled(object? sender, AppointmentEventArgs e)
{
    AppointmentsCancelled++;
    if (!_clinic.WaitingRoom.IsEmpty)
    {
        Patient next = _clinic.WaitingRoom.Peek();
        Console.WriteLine($"  [ЧЕРГА] Слот звільнився. Наступний: {next.FullName}");
    }
}
```

**Крок 3.** Реалізуйте `PrintSummary()` і `SaveSummary(string path)`.

`PrintSummary()` — виводить підсумок сесії в консоль: скільки пацієнтів додано, записів створено, завершено, оплачено тощо.

`SaveSummary()` — записує те саме у файл `session_summary.txt` через `StreamWriter`. Додайте дату і час формування звіту.

**Крок 4.** Додайте `Tracker` у `Clinic.cs`, підпишіть всі обробники у `SubscribeEvents()`.

**Крок 5.** У `Program.cs` при виході (case `"0"`) перед збереженням сесії:
```csharp
clinic.Tracker.PrintSummary();
clinic.Tracker.SaveSummary();
```

Чому виклик `PrintSummary` залишається у `Program.cs` явним, а не через подію? Бо це не реакція на подію — це явна дія користувача "переглянь підсумок перед виходом".

**Перевірка.**
- Додайте пацієнта до черги, потім скасуйте запис → консоль: `[ЧЕРГА] Слот звільнився...`
- Вийдіть з програми → `session_summary.txt` зберігся з коректними лічильниками
- У `clinic.log` — ті ж події від Logger. Обидва підписники (Logger і Tracker) спрацювали незалежно на одні й ті ж події

### Ключові питання для розуміння

- `Logger` і `Tracker` підписані на ті самі події. В якому порядку вони спрацьовують? Чи важливий цей порядок?
- Чому `PrintSummary()` викликається явно, а не як підписник на якусь подію "вихід"?
- Спробуйте прибрати `?` з `event EventHandler<T>?` — що зміниться?

---

## Перевірка перед здачею

Запустіть:
```
dotnet run --project src
```

Виконайте послідовно і перевірте кожен пункт:

- [ ] Зареєстрував пацієнта → `patients/passport_N.txt` з'явився
- [ ] Записав пацієнта → рядок `[EVENT]` у консолі **і** рядок у `clinic.log` — два підписники
- [ ] Оформив терміновий запис → у `clinic.log` два рядки + файл `alerts/urgent_{дата}.txt`
- [ ] Завершив прийом → `passport_N.txt` оновився, дата генерації змінилась
- [ ] Скасував запис (черга не порожня) → консоль: `[ЧЕРГА] Слот звільнився...`
- [ ] Вийшов → `session_summary.txt` з коректними лічильниками
- [ ] Спробував `clinic.Appointments.AppointmentBooked = null` → помилка компілятора

---

## Питання для самоперевірки

1. У чому різниця між `delegate` полем і `event`? Що саме забороняє `event` ззовні класу?
2. `AppointmentManager` не знає ні про `ClinicLogger`, ні про `PatientPassportWriter`. Де відбувається їх зв'язок? Чому це добре?
3. `BookUrgent` піднімає дві події. Logger підписаний на обидві. Скільки рядків у лозі після одного `BookUrgent`?
4. Якщо підписати один і той же метод двічі: `event += handler; event += handler` — скільки разів він спрацює?
5. Чому `PatientPassportWriter.Write()` перезаписує файл а не дописує?
6. Яку ще автоматичну реакцію можна додати до системи не змінюючи жодного менеджера?
