# UI — Сторінки і компоненти

## Студент

| URL | Компонент | Що показує |
|-----|-----------|-----------|
| `/student` | StudentDashboard | Дашборд: прогрес, картки лаб, фільтри статусів |
| `/student/lab/{id}` | Lab | Деталі лаби: завдання, результати, diff, здача |
| `/student/grades` | Grades | Таблиця всіх оцінок |
| `/student/profile` | Profile | Профіль: ім'я, прізвище, email, GitHub URL, GitHub token |

## Викладач

| URL | Компонент | Що показує |
|-----|-----------|-----------|
| `/teacher` | TeacherOverview | Черга перевірки (авто-оновлення), активність, сигнали плагіату |
| `/teacher/students` | Students | Список студентів з пошуком |
| `/teacher/journal` | Journal | Журнал оцінок (сітка студенти × лаби, sticky-колонки) |
| `/teacher/results/{studentId}/{labId}` | TeacherLabResults | Детальні результати здачі: завдання, diff, схожість коду, коментарі |
| `/teacher/labs` | LabsAdmin | CRUD лаб і завдань, імпорт з MD |
| `/teacher/groups` | Groups | CRUD академічних груп |
| `/teacher/monitoring` | Monitoring | Аналітика: розподіл оцінок, топ студентів |
| `/teacher/profile` | TeacherProfile | Профіль викладача + системні налаштування (Gemini, WorkRoot, квота) |

## Ключові компоненти

### Profile.razor
Форма редагування профілю студента.
- Поля: FirstName, LastName, Email, GitHub репо, GitHub токен
- Збереження через `ExecuteUpdateAsync` (уникає EF change tracker конфліктів)
- Після збереження → `App.PreloadAsync()` → UI оновлюється

### Lab.razor — діалог здачі (модалка)
Фази: `branch` → `map` → `running` → `done`

**Фаза `branch` (вибір гілки):**
- Якщо нема GitHub URL → попередження з посиланням на профіль
- Якщо є URL → автоматично тягне гілки через GitHub API
- Режим "Список комітів": вибір будь-якої гілки
- Режим "Граф гіта": автоматично ставить `main`, поле заблоковане
- Кнопка "Далі" → `LoadCommits()` → GitHub API завантажує коміти

**Фаза `map` (маппінг комітів до завдань):**
- Список або граф комітів (перемикач)
- **Кастомний dropdown** для кожного коміту: темний фон, CSS hover, скрол (max-height 260px)
- Summary chips внизу показують скільки завдань покрито
- Граф: CSS-based (без SVG), підтримує merge commits та fork arcs

**Git граф деталі:**
- `BuildGraph()` будує `GraphRow[]` з lane, InLanes, OutLanes, Converges, Forks
- Converges — arc ліворуч (гілка зливається в основну)
- Forks — arc праворуч (merge commit відкриває нову лану)
- Кольори лан: green, blue, yellow, red, purple, cyan (циклічно)

**Фаза `running`:** прогрес-повідомлення grading pipeline (`IProgress<string>`:
черга, клонування, витяг коду, перевірка плагіату, аналіз через Gemini)

**Фаза `done`:** нова авто-оцінка, дельта до попередньої спроби

### GradeDialog (Dialogs/GradeDialog.razor)
MudBlazor діалог оцінювання для викладача.
- Показує результати авто-перевірки по задачах (pass/warn/fail)
- Slider + quick-buttons для оцінки захисту (60/70/80/90/100)
- Live розрахунок фінальної оцінки
- Кнопки: "Зберегти оцінку" та "Відхилити"

### LabCard (Shared/LabCard.razor)
Картка лаби на дашборді студента.
- Статус, авто-оцінка, прогрес-бар завдань
- Дедлайн: зелений / жовтий (<24год) / червоний (прострочено)
- Кнопка "Здати" неактивна після дедлайну
- Всі лаби клікабельні незалежно від статусу

## Sidebar (Layout/Sidebar.razor)
Різні пункти меню залежно від ролі.
- **Студент:** Дашборд, Оцінки, Профіль
- **Викладач:** Огляд, Студенти, Журнал, Лаби, Групи, Моніторинг, Профіль

## CSS та стилі

- `wwwroot/css/app.css` — дизайн-токени + компоненти (dark glassmorphism)
- `.dd-option` — кастомний dropdown (hover `:hover`, активний `.active`)
- `.select { color-scheme: dark }` — підказка браузеру для нативних елементів
- Іконки — inline SVG через `Icon.razor` + `IconData.cs`
- Шрифти: Inter + JetBrains Mono (Google Fonts)
