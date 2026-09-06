# Моделі даних

Сутності БД розкидані по `Infrastructure/Data/*Entities.cs`.

## LabEntities.cs

### LabDef — лабораторна робота
```
Id, Number, Slug, Title, Goal
BranchName    ← гілка git (лаби 01–04: Lab-01…Lab-04; далі feature/…)
SourceDir     ← де шукати проект відносно кореня репо
MergesMain    ← чи зливається в main
OrderIndex    ← порядок сортування
FullMarkdown  ← повний текст MD (для рендерингу)
Deadline      ← DateTime? дедлайн здачі (ставить викладач)
AttemptsMax   ← ліміт спроб (default 3)
Tasks []      ← LabTask
```

### LabTask — завдання до лаби
```
Id, LabDefId, Number, Title
Brief         ← повний markdown між заголовками завдань (не обрізається)
Difficulty    ← 1-4 (кількість ⭐; вага в Scoring.Weighted)
```

### GroupRecord — академічна група
```
Id, Name, Description, OrderIndex
```

## PeopleEntities.cs

### AppUser — Identity-акаунт (таблиця AspNetUsers)
```
IdentityUser + FirstName, LastName
```
Ролі teacher/student — Identity ролі (AspNetRoles/AspNetUserRoles). Google-логіни — AspNetUserLogins.

### StudentRecord
```
Id, FirstName, LastName, Group, Initials, Email
Github        ← URL репозиторію
GithubToken   ← особистий токен, зашифрований (TokenProtector); опційно, для private-репо і вищого rate limit
```

### TeacherRecord
```
Id, FirstName, LastName, Initials, Email, Title, Course
```

### UserLink — зв'язок AppUser ↔ StudentRecord/TeacherRecord
```
Id, UserId (UNIQUE → AspNetUsers.Id), Email, Role (student|teacher)
StudentId? (UNIQUE), TeacherId? (UNIQUE)
```
Якщо запис уже має UserLink зі старим UserId, `EnsureLinkedAsync` **оновлює** UserId (не створює новий).

## SubmissionEntities.cs

### Submission — здача лаби студентом
```
Id, StudentId, LabDefId
Status            ← int (enum LabStatus)
AutoScore?        ← найкраща авто-оцінка 0-100 (null = < 50, відхилено)
DefenseScore?     ← оцінка захисту від викладача
FinalScore?       ← 0.4*Auto + 0.6*Defense
AttemptsUsed, AttemptsMax (default 3)
Attempt1Score?, Attempt2Score?, Attempt3Score?  ← оцінка кожної з перших 3 спроб
IsCurrent         ← поточна активна лаба
SubmittedAt?      ← коли здано
BranchOverride?   ← гілка обрана студентом (якщо ≠ LabDef.BranchName)
CommitMappingJson?← JSON маппінгу sha→taskNumber
PlagiarismFlag    ← спрацював plagiarism-гейт
PlagiarismNote?   ← опис збігу (з ким / %)
PlagiarismApproved← викладач дозволив повторну здачу попри збіг (одноразово)
TaskResults [], Comments []
```

### TaskResult — результат перевірки одного завдання
```
Id, SubmissionId, LabTaskId
AttemptNo     ← номер спроби (результати зберігаються для кожної спроби)
State         ← "pass" | "warn" | "fail"
Score         ← 0-100
Feedback      ← JSON { done[], issues[], analysis }
TestsPassed, TestsTotal  ← наразі 0 (тести не запускаються)
DiffLines []  ← рядки diff коду (ctx/add/del)
```

### DiffEntry
```
Id, TaskResultId, OrderIndex
Type (ctx|add|del|hdr), N1?, N2?, Text
```

### LabComment
```
Id, SubmissionId, TaskResultId? (null = коментар на всю лабу)
AuthorRole (teacher|student), AuthorName, Text, CreatedAt
```

## AuditEntities.cs

### GradeAudit — хто що змінив в оцінюванні
```
Id, SubmissionId, Actor, Action (grade|reject|extra-attempt|plagiarism|repo-conflict…)
OldValue?, NewValue?, At
```

### Notification — сповіщення студенту
```
Id, StudentId, Title, Body, IsRead, CreatedAt
Type (grade|grading|deadline|info)
```

## UI моделі (Models/*.cs) — DTO, НЕ сутності БД

```
CommitModels:  CommitInfo (Sha, Short, Message, Author, Date, Parents[]), CommitTaskMap (Sha, TaskNumber)
PeopleModels:  Student, Teacher
LabModels:     Lab, LabDetail, TaskItem
RosterModels:  RosterStudent, Cell, ReviewItem, LabStat
```

## Enum LabStatus
```csharp
Done     = 0  // зараховано
Review   = 1  // на перевірці (пройшло авто, чекає захист)
Rejected = 2  // відхилено (auto < 50 або plagiarism)
Locked   = 3  // не здавалось (початковий стан)
```

## Індекси та обмеження (AppDbContext.cs)
```
LabDef:     Slug UNIQUE, Number UNIQUE
UserLink:   UserId UNIQUE, StudentId UNIQUE, TeacherId UNIQUE
Submission: (StudentId, LabDefId) UNIQUE
```
