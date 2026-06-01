# Лаба 09 — Generics (Узагальнені типи)

## Мета

Навчитись використовувати `List<T>` замість масивів з ручним лічильником, і самостійно писати generic класи з параметром типу `<T>`. Побачити, як один клас може працювати з різними типами без дублювання коду.

## Контекст

Після восьми лаб система працює, але з обмеженнями: `PatientManager` має фіксований масив `Patient[100]` і вручну керує лічильником. `Remove()` вимагає зсуву всіх елементів. Це ті самі "навмисні обмеження" що були в Lab 03. Настав час замінити їх на `List<T>`.

Крім того, клініка потребує нову функціональність — **чергу очікування**: пацієнти приходять, стають у чергу, і приймаються по порядку. Це окрема функція, яка природньо виражається через `Queue<T>`.

## Гілка

```bash
git checkout main
git pull
git checkout -b feature/generics
```

---

## Завдання 1 — List\<T\>: прощаємось з масивом і лічильником ⭐

### Умова

`PatientManager` зберігає пацієнтів у `Patient[] _patients` з ручним `int _count`. Через це:
- є штучне обмеження (`MaxPatients = 100`)
- `Remove()` потребує ручного зсуву елементів
- `GetAll()`, `FindByName()`, `FindByBloodType()` використовують двопрохідний паттерн

Заміни внутрішнє сховище на `List<Patient>`. Зовнішній API (`Add`, `FindById`, `DisplayAll`, `Remove` тощо) **не змінюється** — тільки внутрішня реалізація.

### Що зміниться

- Поле `_patients` — з `Patient[]` на `List<Patient>`, поле `_count` і константа `MaxPatients` зникають
- `Count` — тепер `_patients.Count`
- `Add()` — більше не перевіряє ліміт, використовує `_patients.Add(...)`
- `Remove()` — замість ручного зсуву використовує `_patients.RemoveAt(i)`
- `FindByName()`, `FindByBloodType()` — замість двопрохідного паттерну: наповнюй проміжний `List<Patient>` і в кінці виклич `.ToArray()`
- `GetAll()` — одна пряма операція замість циклу

### Підказки

1. `List<T>` — це динамічний масив зі стандартної бібліотеки. Розмір зростає автоматично при додаванні.
2. `.Add(item)` — додає в кінець. `.RemoveAt(index)` — видаляє за індексом і зсуває решту автоматично.
3. `.Count` — кількість елементів (аналог `_count`). `[i]` — доступ за індексом (як у масиві).
4. `.ToArray()` — повертає звичайний масив `T[]` зі всіх елементів `List<T>`.
5. Конструктор без параметрів `new List<Patient>()` — порожній список. Або `new List<Patient>(capacity)` якщо знаєш приблизний розмір.

📖 [List\<T\> — Microsoft Docs](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.list-1)

### Що перевірити

Після змін поведінка системи не повинна змінитись. Запусти `dotnet run` — меню `1. Пацієнти` працює так само, але тепер немає обмеження на 100 пацієнтів.

### Адаптація до вашого домену

| Клініка | Готель | Ресторан | Університет | Прокат авто | Бібліотека | Спортзал |
|---------|--------|----------|-------------|-------------|------------|---------|
| `PatientManager` | `GuestManager` | `CustomerManager` | `StudentManager` | `ClientManager` | `ReaderManager` | `MemberManager` |
| `Patient[]` → `List<Patient>` | `Guest[]` → `List<Guest>` | `Customer[]` → `List<Customer>` | `Student[]` → `List<Student>` | `Client[]` → `List<Client>` | `Reader[]` → `List<Reader>` | `Member[]` → `List<Member>` |
| `_count` → `_patients.Count` | `_count` → `_guests.Count` | `_count` → `_customers.Count` | `_count` → `_students.Count` | `_count` → `_clients.Count` | `_count` → `_readers.Count` | `_count` → `_members.Count` |

### Коміт

```bash
git add src/Managers/PatientManager.cs
git commit -m "Lab09 Task1: replace Patient[] array with List<Patient> in PatientManager"
```

---

## Завдання 2 — WaitingQueue\<T\>: власний generic клас ⭐⭐

### Умова

Клініці потрібна черга очікування. Пацієнти приходять і стають у чергу (FIFO: перший прийшов — перший приймається). Це класична структура `Queue<T>` — але нам потрібна обгортка з зрозумілим API і захистом від помилок.

Створи generic клас `WaitingQueue<T>` у `src/Models/WaitingQueue.cs`.

### Що реалізувати

Клас `WaitingQueue<T>` — обгортка над `Queue<T>` з такими членами:

- `int Count` — кількість у черзі (тільки читання)
- `bool IsEmpty` — чи порожня черга
- `void Enqueue(T item)` — додати в кінець
- `T Dequeue()` — прийняти першого (видаляє з черги); якщо порожня — кинути `InvalidOperationException`
- `T Peek()` — подивитись хто перший (не видаляє); якщо порожня — кинути `InvalidOperationException`
- `T[] ToArray()` — поточний стан черги у вигляді масиву (для виводу)

### Підказки

1. Generic клас оголошується як `public class WaitingQueue<T>`. Використовуй `T` скрізь де раніше писав би конкретний тип.
2. `Queue<T>` — стандартна колекція FIFO. `Enqueue` — додати, `Dequeue` — взяти перший, `Peek` — подивитись на перший.
3. `Queue<T>` сама кидає `InvalidOperationException` при `Dequeue`/`Peek` на порожній черзі — але явна перевірка через `IsEmpty` дає зрозуміліше повідомлення.
4. Параметр `<T>` не накладає жодних обмежень — `WaitingQueue<Patient>`, `WaitingQueue<Doctor>`, `WaitingQueue<string>` — все компілюється.

📖 [Queue\<T\> — Microsoft Docs](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.queue-1)  
📖 [Generic classes — Microsoft Docs](https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/generics/generic-classes)

### Що перевірити

Склади кілька рядків тесту в тимчасовому місці або прямо в `Program.cs` на початку:
```
WaitingQueue<string> q = new WaitingQueue<string>();
q.Enqueue("A"); q.Enqueue("B"); q.Enqueue("C");
// q.Count == 3, q.Peek() == "A", q.Dequeue() == "A", q.Count == 2
```
Переконайся що `Dequeue` на порожній черзі кидає виняток.

### Адаптація до вашого домену

| Клініка | Готель | Ресторан | Університет | Прокат авто | Бібліотека | Спортзал |
|---------|--------|----------|-------------|-------------|------------|---------|
| `WaitingQueue<Patient>` | `WaitingQueue<Guest>` | `WaitingQueue<Customer>` | `WaitingQueue<Student>` | `WaitingQueue<Client>` | `WaitingQueue<Reader>` | `WaitingQueue<Member>` |
| черга на прийом | черга на заїзд | черга на столик | черга на зарахування | черга на авто | черга на книгу | черга до тренера |
| `Enqueue` / `Dequeue` / `Peek` | `Enqueue` / `Dequeue` / `Peek` | `Enqueue` / `Dequeue` / `Peek` | `Enqueue` / `Dequeue` / `Peek` | `Enqueue` / `Dequeue` / `Peek` | `Enqueue` / `Dequeue` / `Peek` | `Enqueue` / `Dequeue` / `Peek` |

### Коміт

```bash
git add src/Models/WaitingQueue.cs
git commit -m "Lab09 Task2: add generic WaitingQueue<T> over Queue<T>"
```

---

## Завдання 3 — Черга в системі: нове меню ⭐⭐⭐

### Умова

`WaitingQueue<T>` готова — тепер підключи її до реальної клініки. Додай чергу пацієнтів у `Clinic` і новий пункт меню **"6. Черга — очікування, прийом"**.

### Що реалізувати

**`Clinic.cs`** — нова властивість:

```
public WaitingQueue<Patient> WaitingRoom { get; }
```

Ініціалізуй у конструкторі: `WaitingRoom = new WaitingQueue<Patient>()`.

**`Program.cs`** — новий пункт у головному меню та функція `WaitingRoomMenu(Clinic clinic)` з чотирма діями:

1. Додати пацієнта до черги (вибрати з існуючих пацієнтів за ID)
2. Прийняти першого (`Dequeue`) — вивести ім'я і кількість що залишились
3. Хто перший? (`Peek`) — показати без видалення
4. Переглянути всю чергу — вивести список з нумерацією

Для дій 2 і 3 використовуй `try/catch` на `InvalidOperationException`.

### Підказки

1. `WaitingRoom` зберігає об'єкти `Patient` — тому можна одразу виводити `patient.FullName`.
2. `clinic.WaitingRoom.ToArray()` — отримати масив для виводу переліку черги.
3. `Queue<T>` гарантує порядок FIFO — порядок виводу в `ToArray()` відповідає порядку додавання.

### Що перевірити

Запусти `dotnet run`. Відкрий `6. Черга`. Додай кількох пацієнтів. Виклич "Прийняти першого" двічі — переконайся що порядок правильний (FIFO). Спробуй "Прийняти" з порожньої черги — повинно вивести повідомлення, а не крашнутись.

### Адаптація до вашого домену

| Клініка | Готель | Ресторан | Університет | Прокат авто | Бібліотека | Спортзал |
|---------|--------|----------|-------------|-------------|------------|---------|
| `Clinic.WaitingRoom` | `Hotel.CheckInQueue` | `Restaurant.DiningQueue` | `University.EnrollmentQueue` | `CarRental.RentalQueue` | `Library.BorrowQueue` | `GymCenter.TrainingQueue` |
| `6. Черга — очікування, прийом` | `6. Черга — реєстрація` | `6. Черга — на столик` | `6. Черга — зарахування` | `6. Черга — на авто` | `6. Черга — на книгу` | `6. Черга — до тренера` |

### Коміт

```bash
git add src/Clinic.cs src/Program.cs
git commit -m "Lab09 Task3: add WaitingRoom to Clinic, add menu item 6 Черга очікування"
```

---

## Завдання 4 — Repository\<T\>: generic CRUD з обмеженням типу ⭐⭐⭐⭐

### Умова

`WaitingQueue<T>` не має обмежень — до неї можна додати будь-який тип. Іноді generic клас повинен **гарантувати** що `T` має певні властивості. Наприклад, `Repository<T>` потрібен метод `GetById(int id)` — але для цього він повинен знати що у `T` є поле `Id`.

Вирішення: **constraint** `where T : IIdentifiable`.

### Що реалізувати

**`Interfaces/IIdentifiable.cs`** — новий інтерфейс:

```
public interface IIdentifiable
{
    int Id { get; }
}
```

**`Models/Patient.cs`, `Models/Doctor.cs`, `Models/Appointment.cs`** — додай `: IIdentifiable` до оголошення класу. Поле `Id` вже є — реалізація автоматична.

**`Managers/Repository.cs`** — generic клас `Repository<T> where T : IIdentifiable` з методами:

- `void Add(T item)` — додати
- `T GetById(int id)` — знайти за Id (або `default!` якщо не знайдено)
- `T[] GetAll()` — всі елементи
- `bool Remove(int id)` — видалити за Id
- `int Count` — кількість

Внутрішньо використовуй `List<T>`.

### Ключові питання

- Що дає `where T : IIdentifiable`? Що буде якщо прибрати constraint і спробувати викликати `item.Id`?
- Чим `Repository<T>` відрізняється від `PatientManager`? Коли є сенс використовувати один, коли інший?
- Спробуй: `Repository<Patient> repo = new Repository<Patient>()`. Чи компілюється? А `Repository<string>`?

### Підказки

1. `where T : IIdentifiable` — компілятор дозволяє звертатись до `item.Id` всередині класу, бо гарантовано що у `T` є цей член.
2. `default!` — повертає `null` для reference types, підходить як "не знайдено" аналогічно до `null!` в існуючому коді.
3. `Repository<T>` — не замінює `PatientManager`. Це окремий generic інструмент. `PatientManager` містить специфічну логіку (пошук за ім'ям, статистика) яку `Repository` не знає.

📖 [Generic constraints — Microsoft Docs](https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/generics/constraints-on-type-parameters)  
📖 [default keyword — Microsoft Docs](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/default)

### Що перевірити

У `Program.cs` (тимчасово, для перевірки):

```
Repository<Patient> repo = new Repository<Patient>();
// додай кількох пацієнтів через repo.Add(...)
// спробуй repo.GetById(id), repo.GetAll(), repo.Remove(id)
```

Переконайся що `Repository<string>` **не** компілюється — `string` не реалізує `IIdentifiable`.

### Коміт

```bash
git add src/Interfaces/IIdentifiable.cs src/Managers/Repository.cs src/Models/Patient.cs src/Models/Doctor.cs src/Models/Appointment.cs
git commit -m "Lab09 Task4: add IIdentifiable, Repository<T> where T : IIdentifiable"
```

---

## Перевірка перед здачею

```bash
cd src
dotnet build
dotnet run
```

- [ ] `1. Пацієнти` — поведінка не змінилась, але немає ліміту на кількість
- [ ] `6. Черга` — з'явився новий пункт у головному меню
- [ ] Додати 3 пацієнтів у чергу → прийняти двох → у черзі 1
- [ ] "Прийняти" з порожньої черги → повідомлення про помилку, програма не крашиться
- [ ] `Repository<Patient>` компілюється; `Repository<string>` — ні
- [ ] `WaitingQueue<string>`, `WaitingQueue<int>` — обидва компілюються (без constraint)

---

## Питання для самоперевірки

1. В чому різниця між `List<T>` і `T[]`? Коли перевага у масиву, коли у `List<T>`?
2. Що значить `<T>` в оголошенні класу? Хто вказує конкретний тип — і коли?
3. Навіщо `where T : IIdentifiable`? Що дає constraint порівняно з `WaitingQueue<T>` без нього?
4. Чому `WaitingQueue<T>` не має constraint, а `Repository<T>` має? В чому принципова різниця між ними?
5. FIFO vs LIFO: `Queue<T>` або `Stack<T>` — для черги очікування який підходить і чому?
6. `PatientManager` тепер використовує `List<Patient>`. Чи є сенс замінити весь `PatientManager` на `Repository<Patient>`? Що б втратилось?

---

## Злиття

```bash
git checkout main
git merge --no-ff feature/generics -m "Merge feature/generics: Lab09 Generics — List<T>, WaitingQueue<T>, Repository<T>"
git push
```

> Наступна лаба: `git checkout -b feature/iterators` — `IEnumerable<T>`, `yield return`, `IComparable<T>`.
