using AutoCheck.Data;
using FluentAssertions;
using Xunit;

namespace AutoCheck.Tests.Services;

public class LabMdParserTests
{
    // ── Title extraction ────────────────────────────────────────────────────

    [Fact]
    public void Parse_LabaBranchFormat_ExtractsTitleCorrectly()
    {
        var md = "# Лаба 01 — Основи C#\n\n## Мета\nМета лаби.";
        var result = LabMdParser.Parse(md);
        result.Title.Should().Be("Основи C#");
    }

    [Fact]
    public void Parse_LabRobotaFormat_ExtractsTitleCorrectly()
    {
        var md = "# Лабораторна робота №14. LINQ\n\n## Мета\nМета.";
        var result = LabMdParser.Parse(md);
        result.Title.Should().Be("LINQ");
    }

    // Regression: "Лабораторна робота NN — Title" (labs 17–22) used to leave a
    // stray leading "— " because only the number, not the dash, was stripped.
    [Fact]
    public void Parse_LabRobotaDashFormat_StripsNumberAndDash()
    {
        var md = "# Лабораторна робота 19 — EF Core: TPH глибоко\n\n## Мета\nМета.";
        LabMdParser.Parse(md).Title.Should().Be("EF Core: TPH глибоко");
    }

    [Fact]
    public void Parse_LabRobotaDashFormat_HasNoLeadingDash()
    {
        var title = LabMdParser.Parse("# Лабораторна робота 21 — Async / Await").Title;
        title.Should().Be("Async / Await");
        title.Should().NotStartWith("—");
    }

    [Fact]
    public void Parse_NoH1_ReturnsEmptyTitle()
    {
        var md = "## Мета\nПросто текст без заголовка.";
        var result = LabMdParser.Parse(md);
        result.Title.Should().BeEmpty();
    }

    // ── Goal extraction ─────────────────────────────────────────────────────

    [Fact]
    public void Parse_ExtractsGoalSection()
    {
        var md = "# Лаба 03 — Класи\n\n## Мета\nНавчитися писати класи.\n\n## Гілка\n```\nfeature/catalog\n```";
        var result = LabMdParser.Parse(md);
        result.Goal.Should().Be("Навчитися писати класи.");
    }

    [Fact]
    public void Parse_NoMeta_GoalIsNull()
    {
        var md = "# Лаба 01 — Intro\n\nПросто текст без ## Мета.";
        var result = LabMdParser.Parse(md);
        result.Goal.Should().BeNull();
    }

    // ── Branch extraction ────────────────────────────────────────────────────

    [Fact]
    public void Parse_ExtractsBranchName()
    {
        var md = "# Лаба 03 — Класи\n\n```bash\ngit checkout -b feature/catalog\n```";
        var result = LabMdParser.Parse(md);
        result.BranchName.Should().Be("feature/catalog");
    }

    [Fact]
    public void Parse_NoBranch_BranchNameIsNull()
    {
        var md = "# Лаба 01 — Intro\n\nПросто текст.";
        var result = LabMdParser.Parse(md);
        result.BranchName.Should().BeNull();
    }

    // ── MergesMain detection ─────────────────────────────────────────────────

    [Fact]
    public void Parse_MergesMain_WhenTextSaysSoWithoutNegation()
    {
        var md = "# Лаба 03\n\nГілка **зливається в `main`** після виконання.";
        var result = LabMdParser.Parse(md);
        result.MergesMain.Should().BeTrue();
    }

    [Fact]
    public void Parse_DoesNotMergeMain_WhenTextContainsNegation()
    {
        var md = "# Лаба 01\n\nГілка **не зливається в `main`**. Це пісочниця.";
        var result = LabMdParser.Parse(md);
        result.MergesMain.Should().BeFalse();
    }

    // ── Task extraction ──────────────────────────────────────────────────────

    [Fact]
    public void Parse_ExtractsTaskTitles()
    {
        var md = """
            # Лаба 03 — Класи

            ## Задача 1. Клас Patient ⭐⭐
            ### Умова
            Створіть клас Patient.

            ## Задача 2. Клас Doctor ⭐⭐
            ### Умова
            Створіть клас Doctor.
            """;

        var result = LabMdParser.Parse(md);
        result.Tasks.Should().HaveCount(2);
        result.Tasks[0].Title.Should().Be("Клас Patient");
        result.Tasks[1].Title.Should().Be("Клас Doctor");
    }

    [Fact]
    public void Parse_ExtractsTaskNumbers()
    {
        var md = """
            # Лаба 03 — Класи

            ## Задача 1. Один ⭐
            ### Умова
            Умова 1.

            ## Задача 2. Два ⭐⭐
            ### Умова
            Умова 2.
            """;
        var result = LabMdParser.Parse(md);
        result.Tasks[0].Number.Should().Be(1);
        result.Tasks[1].Number.Should().Be(2);
    }

    [Fact]
    public void Parse_ExtractsDifficulty_TwoStars()
    {
        var md = "# Лаба\n\n## Задача 1. Тест ⭐⭐\n### Умова\nТекст.";
        var result = LabMdParser.Parse(md);
        result.Tasks[0].Difficulty.Should().Be(2);
    }

    [Fact]
    public void Parse_ExtractsDifficulty_FourStars()
    {
        var md = "# Лаба\n\n## Задача 1. Важка ⭐⭐⭐⭐\n### Умова\nТекст.";
        var result = LabMdParser.Parse(md);
        result.Tasks[0].Difficulty.Should().Be(4);
    }

    [Fact]
    public void Parse_DefaultDifficultyOne_WhenNoStars()
    {
        var md = "# Лаба\n\n## Задача 1. Без зірок\n### Умова\nТекст.";
        var result = LabMdParser.Parse(md);
        result.Tasks[0].Difficulty.Should().Be(1);
    }

    [Fact]
    public void Parse_ExtractsBriefFromUmovaSection()
    {
        var md = """
            # Лаба 03

            ## Задача 1. Клас ⭐
            ### Умова
            Напишіть клас з трьома полями.

            ### Підказки
            Використайте конструктор.
            """;
        var result = LabMdParser.Parse(md);
        result.Tasks[0].Brief.Should().Contain("Напишіть клас з трьома полями.");
        result.Tasks[0].Brief.Should().NotContain("Використайте конструктор.");
    }

    [Fact]
    public void Parse_HandlesAlternativeKeyword_Zavdannia()
    {
        var md = "# Лаба 14\n\n## Завдання 1. Рефакторинг ⭐⭐\n### Умова\nТекст завдання.";
        var result = LabMdParser.Parse(md);
        result.Tasks.Should().HaveCount(1);
        result.Tasks[0].Title.Should().Be("Рефакторинг");
    }

    [Fact]
    public void Parse_RemovesBackticksFromTaskTitle()
    {
        var md = "# Лаба\n\n## Задача 1. Рефакторинг `AnalyticsManager` ⭐⭐\n### Умова\nТекст.";
        var result = LabMdParser.Parse(md);
        result.Tasks[0].Title.Should().Be("Рефакторинг AnalyticsManager");
    }

    [Fact]
    public void Parse_EmptyMarkdown_ReturnsEmptyResult()
    {
        var result = LabMdParser.Parse("");
        result.Title.Should().BeEmpty();
        result.Goal.Should().BeNull();
        result.Tasks.Should().BeEmpty();
    }

    [Theory]
    [InlineData("# Лаба 01 — A\n\n## Задача 1. T1 ⭐\n## Задача 2. T2 ⭐\n## Задача 3. T3 ⭐⭐\n## Задача 4. T4 ⭐⭐⭐", 4)]
    [InlineData("# Лаба 01 — A", 0)]
    [InlineData("# Лаба 01 — A\n\n## Задача 1. T ⭐", 1)]
    public void Parse_CorrectTaskCount(string md, int expectedCount)
    {
        LabMdParser.Parse(md).Tasks.Should().HaveCount(expectedCount);
    }
}
