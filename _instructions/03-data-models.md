# Моделі даних

## Сутності БД (Data/Entities.cs)

### LabDef — лабораторна робота
```
Id, Number, Slug, Title, Goal
BranchName    ← гілка git (наприклад sandbox/intro, feature/catalog)
SourceDir     ← де шукати проект (sandbox/intro, src)
Deadline      ← DateTime? дедлайн здачі
MergesMain    ← чи зливається в main
FullMarkdown  ← повний текст MD
Tasks []      ← завдання цієї лаби
```

### LabTask — завдання до лаби
```
Id, LabDefId, Number, Title, Brief, Difficulty (1-5)
```

### StudentRecord — студент
```
Id, FirstName, LastName, Group, Email
Github        ← URL репозиторію (https://github.com/user/repo)
GithubToken   ← особистий токен (необов'язково, для rate limit)
Initials
```

### TeacherRecord — викладач
```
Id, FirstName, LastName, Initials, Title, Course
```

### UserLink — зв'язок Keycloak ↔ StudentRecord/TeacherRecord
```
Id, KeycloakSub (unique), Email, Role (student|teacher)
StudentId?, TeacherId?
```

### Submission — здача лаби студентом
```
Id, StudentId, LabDefId
Status        ← int (enum LabStatus: Done=0, Review=1, Rejected=2, Locked=3)
AutoScore     ← оцінка авто-перевірки (0-100)
DefenseScore  ← оцінка захисту від викладача (0-100)
FinalScore    ← 0.4*Auto + 0.6*Defense
AttemptsUsed, AttemptsMax
SubmittedAt   ← DateTime? коли здано
IsCurrent     ← чи це поточна активна лаба студента
Deadline      ← string? (legacy, не використовується)
```

### TaskResult — результат перевірки одного завдання
```
Id, SubmissionId, LabTaskId
State         ← "pass" | "warn" | "fail"
Score         ← 0-100
TestsPassed, TestsTotal
Feedback      ← текст від перевіряючої системи
DiffLines []  ← рядки diff коду
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

Це DTO для передачі даних з сервісів в компоненти. НЕ сутності БД.

```
Student       ← FirstName, LastName, Group, Email, Github, GithubToken, Initials
Teacher       ← FirstName, LastName, Initials, Title, Course
Lab           ← Id, Title, Status, Auto?, Defense?, Final?, Current, DeadlineAt?
LabDetail     ← повна деталізація лаби з завданнями та результатами
TaskItem      ← одне завдання з результатом (для Lab.razor)
RosterStudent ← студент у журналі викладача
ReviewItem    ← елемент черги на перевірку
```

## Enum LabStatus
```csharp
Done     = 0  // зараховано
Review   = 1  // на перевірці у викладача
Rejected = 2  // відхилено
Locked   = 3  // не здавалось (початковий стан)
```
