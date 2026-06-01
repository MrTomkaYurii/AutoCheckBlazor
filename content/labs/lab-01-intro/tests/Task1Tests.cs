namespace SandboxIntro.Tests;

/// <summary>
/// Task 1 — BMI (or equivalent domain formula): output = value / (value * value).
/// We check only the numeric result — domain label irrelevant.
/// </summary>
public class Task1Tests
{
    private static string Go(string input) => TaskTestBase.Run(Task1.Run, input);

    [Theory]
    [InlineData("70\n1.75", 22.86)]
    [InlineData("90\n1.80", 27.78)]
    [InlineData("55\n1.62", 20.95)]
    [InlineData("100\n1.90", 27.70)]
    public void Task1_CorrectFormulaResult(string input, double expected)
        => TaskTestBase.AssertContainsNumber(Go(input), expected);

    [Fact]
    public void Task1_ProducesOutput()
        => Assert.False(string.IsNullOrWhiteSpace(Go("70\n1.75")),
            "Task1.Run() produced no output");
}
