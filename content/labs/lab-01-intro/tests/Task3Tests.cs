namespace SandboxIntro.Tests;

/// <summary>
/// Task 3 — Age + category (child / adult / senior).
/// Year 2026 is hardcoded in instructions: age = 2026 - birthYear.
/// Categories: 0-17 child, 18-59 adult, 60+ senior (or domain equivalent).
/// </summary>
public class Task3Tests
{
    private static string Go(string input) => TaskTestBase.Run(Task3.Run, input);

    [Theory]
    [InlineData("2010", 16)]  // child
    [InlineData("1990", 36)]  // adult
    [InlineData("1955", 71)]  // senior
    [InlineData("2008", 18)]  // boundary: adult
    public void Task3_CorrectAge(string input, int expectedAge)
        => TaskTestBase.AssertContainsText(Go(input), expectedAge.ToString());

    [Fact]
    public void Task3_Child_Category()
        => TaskTestBase.AssertContainsText(Go("2010"), "дитин",
            "Expected category 'дитина' for year 2010");

    [Fact]
    public void Task3_Adult_Category()
        => TaskTestBase.AssertContainsText(Go("1990"), "дорослий",
            "Expected category 'дорослий' for year 1990");

    [Fact]
    public void Task3_Senior_Category()
    {
        var output = Go("1955");
        bool isSenior = output.Contains("пенсіонер", StringComparison.OrdinalIgnoreCase)
                     || output.Contains("літній",    StringComparison.OrdinalIgnoreCase);
        Assert.True(isSenior, $"Expected senior category for 1955.\nOutput:\n{output}");
    }
}
