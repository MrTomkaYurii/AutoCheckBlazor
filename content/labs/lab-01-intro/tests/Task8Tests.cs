namespace SandboxIntro.Tests;

/// <summary>
/// Task 8 — All logic extracted into static methods.
/// Input: weight height price count discount birthYear systolic diastolic (8 lines).
/// Checks that all 4 results appear in output.
/// </summary>
public class Task8Tests
{
    private static string Go(string input) => TaskTestBase.Run(Task8.Run, input);

    // Example 1 from instructions
    private const string Input1 = "82.0\n1.78\n500\n3\n10\n1990\n120\n80";
    // Example 2
    private const string Input2 = "55.0\n1.62\n200\n2\n15\n2010\n155\n100";

    [Fact]
    public void Task8_Ex1_BMI()
        => TaskTestBase.AssertContainsNumber(Go(Input1), 25.88,
            "BMI for 82kg / 1.78m should be 25.88");

    [Fact]
    public void Task8_Ex1_Cost()
        => TaskTestBase.AssertContainsNumber(Go(Input1), 1350.00,
            "Cost for 500*3*(1-10%) should be 1350.00");

    [Fact]
    public void Task8_Ex1_Age()
        => TaskTestBase.AssertContainsText(Go(Input1), "36",
            "Age for birth year 1990 should be 36");

    [Fact]
    public void Task8_Ex1_Adult()
        => TaskTestBase.AssertContainsText(Go(Input1), "дорослий");

    [Fact]
    public void Task8_Ex2_BMI()
        => TaskTestBase.AssertContainsNumber(Go(Input2), 20.95);

    [Fact]
    public void Task8_Ex2_Cost()
        => TaskTestBase.AssertContainsNumber(Go(Input2), 340.00);

    [Fact]
    public void Task8_Ex2_Child()
        => TaskTestBase.AssertContainsText(Go(Input2), "дитин");

    [Fact]
    public void Task8_Ex2_Hypertension2()
    {
        var output = Go(Input2);
        bool ok = output.Contains("2 ступеня", StringComparison.OrdinalIgnoreCase)
               || output.Contains("гіпертонія 2", StringComparison.OrdinalIgnoreCase);
        Assert.True(ok, $"Expected hypertension stage 2.\nOutput:\n{output}");
    }

    [Fact]
    public void Task8_ProducesFourLines()
    {
        var output = Go(Input1).Trim();
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.True(lines.Length >= 4,
            $"Expected at least 4 output lines, got {lines.Length}.\nOutput:\n{output}");
    }
}
