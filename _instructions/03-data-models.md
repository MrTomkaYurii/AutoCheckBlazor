# Моделі даних

## Сутності БД (Data/Entities.cs)

### LabDef — лабораторна робота
```
Id, Number, Slug, Title, Goal
BranchName    ← гілка git (sandbox/intro, feature/catalog...)
SourceDir     ← де шукати проект відносно кореня репо
Deadline      ← DateTime? дедлайн здачі
MergesMain    ← чи зливається в main
FullMarkdown  ← повний текст MD (зберігається для рендерингу)
Tasks []      ← LabTask записи
```

### LabTask — завдання до лаби
```
Id, LabDefId, Number, Title
Brief         ← повний markdown-вміст між заголовками завдань (не обрізається)
Difficulty    ← 1-4 (кількість ⭐)
```

### StudentRecord — студент
```
Id, FirstName, LastName, Group, Initials
Email         ← заповнюється студентом у профілі
Github        ← URL репозиторію (https://github.com/user/repo)
GithubToken   ← особистий токен (необов'язково, для вищого rate limit GitHub API)
```

### TeacherRecord — викладач
```
Id, FirstName, LastName, Initials, Email, Title, Course
```

### AppUser — Identity-акаунт (таблиця AspNetUsers)
```
IdentityUser + FirstName, LastName
```
Ролі teacher/student — стандартні Identity ролі (AspNetRoles/AspNetUserRoles).
Google-логіни — AspNetUserLogins.

### UserLink — зв'язок AppUser ↔ StudentRecord/TeacherRecord
```
Id, UserId (UNIQUE, → AspNetUsers.Id), Email, Role (student|teacher)
StudentId?   ← UNIQUE (один студент — один link)
TeacherId?   ← UNIQUE (один викладач — один link)
```
Якщо запис студента/викладача вже має UserLink зі старим UserId,
`EnsureLinkedAsync` **оновлює** UserId — не створює новий запис.

### Submission — здача лаби студентом
```
Id, StudentId, LabDefId
Status           ← int (enum LabStatus: Done=0, Review=1, Rejected=2, Locked=3)
AutoScore        ← оцінка авто-перевірки 0-100
DefenseScore     ← оцінка захисту від викладача 0-100
FinalScore       ← 0.4*Auto + 0.6*Defense
AttemptsUsed, AttemptsMax (default 3)
IsCurrent        ← чи це поточна активна лаба студента
SubmittedAt      ← DateTime? коли здано
BranchOverride   ← гілка обрана студентом (якщо відрізняється від LabDef.BranchName)
CommitMappingJson← JSON маппінгу sha→taskNumber обраного студентом
```

### TaskResult — результат перевірки одного завдання
```
Id, SubmissionId, LabTaskId
State         ← "pass" | "warn" | "fail"
Score         ← 0-100
TestsPassed, TestsTotal
Feedback      ← текст від перевіряючої системи (markdown)
DiffLines []  ← рядки diff коду (ctx/add/del)
```

### GroupRecord — академічна група
```
Id, Name, Description, OrderIndex
```

### LabComment — коментар до здачі
```
Id, SubmissionId, TaskResultId?
AuthorRole (teacher|student), AuthorName, Text, CreatedAt
```

### Notification — сповіщення студенту
```
Id, StudentId, Title, Body, IsRead, CreatedAt
Type (grade|grading|info)
```

## UI моделі (Models/Models.cs)

DTO для передачі з сервісів в компоненти. **НЕ сутності БД**.

```
CommitInfo    ← Sha, Short(7), Message, Author, Date, Parents[]  (для git граф)
CommitTaskMap ← Sha, TaskNumber  (маппінг коміту до завдання)

Student       ← FirstName, LastName, Group, Email, Github, GithubToken, Initials
Teacher       ← FirstName, LastName, Initials, Title, Course

Lab           ← Id, Title, Status, Auto?, Defense?, Final?, Current, DeadlineAt?
LabDetail     ← повна деталізація: Id, Title, Branch, Tasks[], Auto, AttemptsUsed...
TaskItem      ← завдання з результатом (State, Score, Brief, Feedback, Diff[])

RosterStudent ← студент у журналі (Id, Last, First, Group, Labs[Cell])
Cell          ← оцінка однієї лаби (Status, Auto?, Defense?, Final?)
ReviewItem    ← елемент черги на перевірку
LabStat       ← статистика лаби для сторінки моніторингу
```

## Enum LabStatus
```csharp
Done     = 0  // зараховано
Review   = 1  // на перевірці
Rejected = 2  // відхилено
Locked   = 3  // не здавалось (початковий стан)
```

## Індекси та обмеження (AppDbContext.cs)

```
LabDef:     Slug UNIQUE, Number UNIQUE
UserLink:   UserId UNIQUE, StudentId UNIQUE, TeacherId UNIQUE
Submission: (StudentId, LabDefId) UNIQUE
```
