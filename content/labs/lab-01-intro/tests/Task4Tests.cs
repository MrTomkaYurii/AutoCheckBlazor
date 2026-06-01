namespace SandboxIntro.Tests;

/// <summary>
/// Task 4 — Blood pressure classification (AHA standard).
/// Both values must appear in output; category keyword must be present.
/// </summary>
public class Task4Tests
{
    private static string Go(string input) => TaskTestBase.Run(Task4.Run, input);

    [Fact]
    public void Task4_Normal()
    {
        var out4 = Go("115\n75");
        TaskTestBase.AssertContainsText(out4, "115");
        TaskTestBase.AssertContainsText(out4, "75");
        TaskTestBase.AssertContainsText(out4, "норма");
    }

    [Fact]
    public void Task4_Elevated()
    {
        var out4 = Go("125\n78");
        TaskTestBase.AssertContainsText(out4, "підвищений");
    }

    [Fact]
    public void Task4_Hypertension1()
    {
        var out4 = Go("135\n88");
        bool isH1 = out4.Contains("гіпертонія 1", StringComparison.OrdinalIgnoreCase)
                 || out4.Contains("1 ступеня",    StringComparison.OrdinalIgnoreCase);
        Assert.True(isH1, $"Expected hypertension stage 1.\nOutput:\n{out4}");
    }

    [Fact]
    public void Task4_Hypertension2()
    {
        var out4 = Go("155\n100");
        bool isH2 = out4.Contains("гіпертонія 2", StringComparison.OrdinalIgnoreCase)
                 || out4.Contains("2 ступеня",    StringComparison.OrdinalIgnoreCase);
        Assert.True(isH2, $"Expected hypertension stage 2.\nOutput:\n{out4}");
    }
}
