namespace SandboxIntro.Tests;

/// <summary>
/// Task 6 — Card number analysis via modulo operator.
/// Checks that three output lines are produced with correct values.
/// </summary>
public class Task6Tests
{
    private static string Go(string input) => TaskTestBase.Run(Task6.Run, input);

    [Fact]
    public void Task6_Card10240_GeneralTherapy()
    {
        var output = Go("10240");
        // lastDigit = 0 → загальна терапія; even → пільгова: так; %3==0 → огляд: так
        TaskTestBase.AssertContainsText(output, "загальна терапія");
        TaskTestBase.AssertContainsText(output, "так");
    }

    [Fact]
    public void Task6_Card20003_Surgery()
    {
        var output = Go("20003");
        // lastDigit = 3 → хірургія; odd → пільгова: ні; %3==0 → огляд: так
        TaskTestBase.AssertContainsText(output, "хірургія");
    }

    [Fact]
    public void Task6_Card11117_Neurology()
    {
        var output = Go("11117");
        // lastDigit = 7 → неврологія; odd → пільгова: ні; 11117%3≠0 → огляд: ні
        TaskTestBase.AssertContainsText(output, "неврологія");
    }

    [Fact]
    public void Task6_Card10245_Cardiology()
    {
        var output = Go("10245");
        // lastDigit = 5 → кардіологія
        TaskTestBase.AssertContainsText(output, "кардіологія");
    }

    [Fact]
    public void Task6_ProducesThreeLines()
    {
        var output = Go("10240").Trim();
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.True(lines.Length >= 3,
            $"Expected at least 3 output lines, got {lines.Length}.\nOutput:\n{output}");
    }
}
