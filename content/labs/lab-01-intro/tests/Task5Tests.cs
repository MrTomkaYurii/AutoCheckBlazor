namespace SandboxIntro.Tests;

/// <summary>
/// Task 5 — Schedule via switch expression.
/// Checks day names and hours appear in output.
/// </summary>
public class Task5Tests
{
    private static string Go(string input) => TaskTestBase.Run(Task5.Run, input);

    [Theory]
    [InlineData("1", "Понеділок", "08:00")]
    [InlineData("3", "Середа",    "09:00")]
    [InlineData("5", "П'ятниця",  "08:00")]
    [InlineData("6", "Субота",    "09:00")]
    public void Task5_WeekdayNameAndHours(string input, string dayName, string startTime)
    {
        var output = Go(input);
        TaskTestBase.AssertContainsText(output, dayName);
        TaskTestBase.AssertContainsText(output, startTime);
    }

    [Fact]
    public void Task5_Sunday_IsHoliday()
    {
        var output = Go("7");
        bool isHoliday = output.Contains("вихідний", StringComparison.OrdinalIgnoreCase)
                      || output.Contains("Неділя",   StringComparison.OrdinalIgnoreCase);
        Assert.True(isHoliday, $"Expected Sunday to be a day off.\nOutput:\n{output}");
    }

    [Fact]
    public void Task5_InvalidDay_Handled()
    {
        // Should not throw; output can be anything
        var output = Go("9");
        Assert.NotNull(output);
    }
}
