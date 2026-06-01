# AutoCheck — Blazor Server (заготовка)

Система здачі лабораторних робіт з ООП C#. **Blazor Server (.NET 8) + MudBlazor.**
Це UI/UX-заготовка: усі екрани з прототипу, дизайн 1-в-1, дані — мокові (в пам'яті).
Бекенд-логіки (БД, реальна авто-перевірка, автентифікація) свідомо немає.

## Запуск

```bash
cd AutoCheckBlazor
dotnet restore      # підтягне MudBlazor 7.15.0
dotnet run
```
Відкрий адресу з консолі (напр. `https://localhost:7186`).

> Потрібен **.NET 8 SDK**. Перевірити: `dotnet --version` → має бути `8.x`.
> (Якщо стоїть лише .NET 10 — постав 8 LTS поряд, або зміни `TargetFramework` у `AutoCheck.csproj` на `net10.0`; код сумісний.)

## Маршрути

| URL | Екран |
|-----|-------|
| `/` | Вхід / Реєстрація (перемикач Студент / Викладач) |
| `/student` | Дашборд студента |
| `/student/lab/3` | Сторінка здачі лаби (таски, фідбек, diff, модалка) |
| `/student/grades` | Журнал оцінок студента |
| `/student/profile` | Профіль |
| `/teacher` | Огляд курсу + черга на перевірку |
| `/teacher/journal` | Матриця оцінок (фільтри, sticky-колонки) |
| `/teacher/students` | Картки студентів |
| `/teacher/labs` | Лаби (статистика, дедлайни) |

Клік по жовтій клітинці «на перевірці» в журналі (або «Оцінити» в огляді) → **MudDialog** з виставленням оцінки захисту і live-розрахунком фінальної (40% авто + 60% захист).

## Структура

```
AutoCheckBlazor/
├─ Program.cs                  # DI: RazorComponents + MudServices + AppState
├─ AutoCheck.csproj            # net8.0, MudBlazor 7.15.0
├─ Components/
│  ├─ App.razor                # html-документ (заміна index.html)
│  ├─ Routes.razor             # роутер
│  ├─ _Imports.razor
│  ├─ Layout/                  # MainLayout(+Mud providers), LoginLayout, Sidebar, TopBar
│  ├─ Pages/                   # усі 9 екранів (@page)
│  ├─ Dialogs/GradeDialog.razor
│  └─ Shared/                  # Icon, Badge, Progress, ScorePill, Ring, LabCard, GradeCell, …
├─ Services/
│  ├─ MockData.cs              # уся мок-дата (студент, ростер, лаби, статистика)
│  └─ AppState.cs              # роль (student/teacher) — scoped на circuit
├─ Models/Models.cs            # C# моделі (Lab, RosterStudent, Cell, TaskItem, …)
└─ wwwroot/css/
   ├─ app.css                  # дизайн-токени + компоненти, перенесено 1-в-1 з прототипу
   └─ blazor.css               # дрібні Blazor-специфічні правки
```

## Як підключати бекенд далі

- `Services/MockData.cs` → заміни на сервіси з EF Core / репозиторіями.
- `AppState` → реальна автентифікація (`AuthenticationStateProvider`), ролі.
- Форми (`Login.razor`) → `EditForm` + DataAnnotations валідація + identity.
- Авто-перевірка (модалка «Здати повторно») → виклик реального ранера тестів через SignalR/чергу.
- `Lab.razor` приймає `{Id:int}`, але показує мок Lab03 — підв'яжи до сервісу за Id.

## Примітки

- Дизайн тримається на `wwwroot/css/app.css` (як у прототипі) + inline-стилях у розмітці. MudBlazor використано точково — для модалки та теми.
- Іконки — інлайн-SVG (`Components/Shared/IconData.cs`), без зовнішніх залежностей.
- Шрифти Inter / JetBrains Mono тягнуться з Google Fonts (потрібен інтернет). Для офлайну — поклади їх у `wwwroot` і онови `App.razor`.
