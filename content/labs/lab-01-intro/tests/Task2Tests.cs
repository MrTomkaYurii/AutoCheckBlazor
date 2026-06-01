namespace SandboxIntro.Tests;

/// <summary>
/// Task 2 — Cost with discount: price * count * (1 - discount/100).
/// </summary>
public class Task2Tests
{
    private static string Go(string input) => TaskTestBase.Run(Task2.Run, input);

    [Theory]
    [InlineData("500\n3\n10",  1350.00)]
    [InlineData("200\n5\n0",   1000.00)]
    [InlineData("750\n2\n15",  1275.00)]
    [InlineData("1000\n1\n0",  1000.00)]
    public void Task2_CorrectCostWithDiscount(string input, double expected)
        => TaskTestBase.AssertContainsNumber(Go(input), expected);

    [Fact]
    public void Task2_ZeroDiscount_FullPrice()
    {
        var output = Go("100\n1\n0");
        TaskTestBase.AssertContainsNumber(output, 100.00);
    }
}
