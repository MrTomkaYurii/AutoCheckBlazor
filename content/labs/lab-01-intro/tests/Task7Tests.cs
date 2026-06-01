namespace SandboxIntro.Tests;

/// <summary>
/// Task 7 — Statistics: total, average, min/max, count above average, first > 1000.
/// </summary>
public class Task7Tests
{
    private static string Go(string input) => TaskTestBase.Run(Task7.Run, input);

    // 5 values: 350 800 200 1200 450 → total=3000, avg=600, min=200, max=1200, above=2, first>1000 at #4
    private const string Input1 = "5\n350\n800\n200\n1200\n450";

    [Fact]
    public void Task7_Total_Correct()
        => TaskTestBase.AssertContainsNumber(Go(Input1), 3000.00);

    [Fact]
    public void Task7_Average_Correct()
        => TaskTestBase.AssertContainsNumber(Go(Input1), 600.00);

    [Fact]
    public void Task7_Min_Correct()
        => TaskTestBase.AssertContainsNumber(Go(Input1), 200.00);

    [Fact]
    public void Task7_Max_Correct()
        => TaskTestBase.AssertContainsNumber(Go(Input1), 1200.00);

    [Fact]
    public void Task7_AboveAverage_Correct()
    {
        var output = Go(Input1);
        // 800 and 1200 are above 600 → "2 з 5" or just "2"
        Assert.Contains("2", output);
    }

    [Fact]
    public void Task7_FirstOver1000_Found()
    {
        var output = Go(Input1);
        // Should mention #4 and 1200
        Assert.Contains("4", output);
        TaskTestBase.AssertContainsNumber(output, 1200.00);
    }

    [Fact]
    public void Task7_NoOver1000_ShowsNone()
    {
        // 3 values: 300 450 350 → no value > 1000
        var output = Go("3\n300\n450\n350");
        TaskTestBase.AssertContainsText(output, "немає",
            "Expected 'немає' when no value exceeds 1000");
    }
}
