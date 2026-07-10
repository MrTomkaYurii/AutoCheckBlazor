# Автентифікація — ASP.NET Core Identity

Без зовнішніх сервісів і контейнерів: акаунти, ролі та Google-логіни зберігаються
в тій самій SQLite базі (`autocheck.db`), таблиці `AspNet*` створює `IdentityDbContext`.

## Схема

- `AppUser : IdentityUser` (+ `FirstName`, `LastName`) — Infrastructure/Data/PeopleEntities.cs
- `AppDbContext : IdentityDbContext<AppUser>` — Identity таблиці + доменні
- Ролі: `teacher`, `student` (IdentityRole, сідяться при старті)
- `UserLink.UserId` → `AppUser.Id` — місток до `StudentRecord` / `TeacherRecord`

## Cookie

`ConfigureApplicationCookie`: LoginPath = `/`, 8 годин, sliding. Логін-сторінка — Blazor
сторінка `/` (Login.razor), реєстрація — `/register`.

## Ендпоінти (Program.cs, поза Blazor circuit — вони пишуть cookie)

| Ендпоінт | Метод | Що робить |
|---|---|---|
| `/account/login` | POST (form) | `PasswordSignInAsync`, помилки назад через query (`/?error=…&email=…`) |
| `/account/register` | POST (form) | Валідація → `CreateAsync` → роль student → `LinkStudentAsync(group)` → sign-in |
| `/account/google-login` | GET | Challenge Google |
| `/account/google-callback` | GET | Вхід по external login; новий юзер → створення + `/onboarding` |
| `/account/logout` | POST | `SignOutAsync` |
| `/account/refresh-signin` | GET (auth) | `RefreshSignInAsync` після зміни пароля (security stamp) |

Blazor-сторінки постять на ці ендпоінти звичайними `<form method="post">` —
интерактивний circuit не може встановлювати cookie, тому логін/реєстрація йдуть
повним HTTP-запитом.

## Google OAuth (опційно)

`appsettings.json → Authentication:Google:{ClientId,ClientSecret}`. Якщо ClientId
порожній — `AddGoogle` не реєструється і кнопки Google не рендеряться.
Redirect URI в Google Cloud Console: `https://<host>/signin-google`.

Новий Google-користувач: створюється AppUser без пароля (роль student),
`LinkStudentAsync(user, "")` → редирект на `/onboarding`, де студент обирає групу
(гейт у MainLayout: студент без групи → `/onboarding`). Пізніше може встановити
пароль у профілі (`AddPasswordAsync`) — тоді працюють обидва способи входу.

Якщо email Google-акаунта збігається з існуючим акаунтом — Google просто
підв'язується до нього (`AddLoginAsync`), дублікат не створюється.

## Тестові акаунти (сідер)

| Email | Пароль | Роль |
|---|---|---|
| `teacher@test.com` | `Test1234!` | teacher (лінк до seed TeacherRecord) |
| `student@test.com` | `Test1234!` | student (лінк до Петро Іваненко по email) |

## Зміна пароля

Прямо в профілях через `UserManager.ChangePasswordAsync` (студент без пароля —
`AddPasswordAsync`). Після успіху — редирект на `/account/refresh-signin`,
щоб перевипустити cookie з новим security stamp (інакше сесія злетить за ~30 хв).

## AuthService.EnsureLinkedAsync() — деталі

Викликається в MainLayout при кожному вході (страхувальна сітка — зазвичай лінк
уже створений при реєстрації):
1. Шукає `UserLink` по `UserId`
2. Якщо знайдено → виходить
3. Якщо ні → бере AppUser з БД, шукає студента по email
4. Якщо студент вже має `UserLink` зі старим UserId → **оновлює** UserId
5. Якщо студента немає → `CreateStudentAsync()` + `Submission(Locked)` для всіх лаб
6. Race condition → `catch DbUpdateException` → `ChangeTracker.Clear()`
