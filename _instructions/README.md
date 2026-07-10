# _instructions

Документація для розробки AutoCheck. Читати перед початком сесії.

| Файл | Що містить |
|------|-----------|
| [01-project-overview](01-project-overview.md) | Що це, стек, ролі, запуск, ключові факти |
| [02-architecture](02-architecture.md) | Структура проекту, потік даних, Blazor Server, DbContextFactory, фонові сервіси, БД |
| [03-data-models](03-data-models.md) | Всі сутності БД (Infrastructure/Data), UI моделі, індекси |
| [04-services](04-services.md) | Що робить кожен сервіс (grading, plagiarism, черга, квота, нотифікації, бекап) |
| [05-grading-pipeline](05-grading-pipeline.md) | Реальний флоу: git clone → витяг коду → plagiarism-гейт → Gemini → score; checks.json (requirements) |
| [06-lab-structure](06-lab-structure.md) | Формати заголовків завдань, LabMdParser, checks.json |
| [07-ui-pages](07-ui-pages.md) | Всі сторінки, Lab.razor діалог здачі, git граф, кастомний dropdown |
| [08-auth](08-auth.md) | ASP.NET Core Identity, Google OAuth, ендпоінти, EnsureLinkedAsync |
| [09-decisions](09-decisions.md) | Чому так а не інакше, відомі обмеження, TODO |
