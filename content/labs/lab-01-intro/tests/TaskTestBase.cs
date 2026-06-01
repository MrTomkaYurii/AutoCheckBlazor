namespace SandboxIntro.Tests;

/// <summary>
/// Runs a student's TaskN.Run() with piped stdin, captures stdout.
/// Domain-agnostic: checks only numeric values, not Ukrainian labels.
/// </summary>
public static class TaskTestBase
{
    public static string Run(Action task, string stdin)
    {
        var oldIn  = Console.In;
        var oldOut = Console.Out;
        try
        {
            Console.SetIn(new StringReader(stdin));
            var sw = new StringWriter();
            Console.SetOut(sw);
            task();
            return sw.ToString();
        }
        finally
        {
            Console.SetIn(oldIn);
            Console.SetOut(oldOut);
        }
    }

    /// <summary>Contains a number formatted to 2 decimal places.</summary>
    public static void AssertContainsNumber(string output, double expected, string hint = "")
    {
        // Try both . and , as decimal separator (InvariantCulture & current culture)
        var dot   = expected.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        var comma = expected.ToString("F2", System.Globalization.CultureInfo.CurrentCulture);
        bool found = output.Contains(dot) || output.Contains(comma);
        Assert.True(found,
            $"Expected number {dot} not found in output.\nOutput:\n{output}\n{hint}");
    }

    public static void AssertContainsText(string output, string expected, string hint = "")
    {
        Assert.True(
            output.Contains(expected, StringComparison.OrdinalIgnoreCase),
            $"Expected '{expected}' not found in output.\nOutput:\n{output}\n{hint}");
    }
}
