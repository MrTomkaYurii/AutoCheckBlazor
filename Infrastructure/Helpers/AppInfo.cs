namespace AutoCheck;

/// <summary>
/// Single source of truth for product identity: version, academic year, author,
/// and the changelog. Referenced by the About page, the footer and the login screen
/// so these never drift apart. Bump <see cref="Version"/> and prepend a
/// <see cref="Release"/> to <see cref="Changelog"/> on every notable change.
/// </summary>
public static class AppInfo
{
    public const string Name         = "AutoCheck";
    public const string Version      = "1.0.0";
    public const string AcademicYear = "2025–2026";
    public const string Author       = "Tomka Yurii";
    public const string AuthorEmail  = "tomka.yuriy@gmail.com";
    public const string Repo         = "https://github.com/MrTomkaYurii/AutoCheckBlazor";
    public const string University   = "Кафедра комп'ютерних наук · ЧНУ ім. Ю. Федьковича";

    /// <summary>Short description shown on the About page.</summary>
    public const string Tagline =
        "Веб-платформа автоматичної перевірки лабораторних робіт з курсу «Об'єктно-орієнтоване програмування на C#».";

    public record Release(string Version, string Date, string[] Changes);

    /// <summary>Newest first.</summary>
    public static readonly Release[] Changelog =
    [
        new("1.0.0", "Липень 2026", [
            "Перший публічний реліз — запуск у 2025–2026 навчальному році",
            "Авто-перевірка коду через Gemini: git clone → витяг коду завдань → оцінка за вимогами",
            "Виявлення плагіату та підміни репозиторію між студентами",
            "Кабінет викладача: черга перевірки, журнал оцінок, аналітика, керування лабами й групами",
            "Реєстрація email+пароль та вхід через Google (опційно)",
            "Сторінка «Про продукт», версійність і форма зворотного зв'язку для студентів",
            "Щоденні авто-бекапи БД та фонове очищення кешу репозиторіїв",
        ]),
        new("0.9.0", "Бета", [
            "Базовий grading pipeline, ролі студент/викладач, імпорт 22 лаб з Markdown",
            "Інтеграція з GitHub API для вибору гілок і комітів",
        ]),
    ];
}
