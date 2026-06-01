using AutoCheck.Models;
using FluentAssertions;
using Xunit;

namespace AutoCheck.Tests.Models;

public class LabStatusMetaTests
{
    [Theory]
    [InlineData(LabStatus.Done,     "Зараховано",   "green")]
    [InlineData(LabStatus.Review,   "На перевірці", "yellow")]
    [InlineData(LabStatus.Rejected, "Відхилено",    "red")]
    [InlineData(LabStatus.Locked,   "Не здано",     "grey")]
    public void StatusMeta_ReturnsCorrectLabelAndColor(LabStatus status, string expectedLabel, string expectedColor)
    {
        var (label, color, _) = StatusMeta.Of(status);
        label.Should().Be(expectedLabel);
        color.Should().Be(expectedColor);
    }

    [Fact]
    public void RosterStudent_Recompute_CalculatesAverageCorrectly()
    {
        var student = new RosterStudent
        {
            Id = 1, Last = "Тест", First = "Студент", Group = "КН-31", Initials = "ТС",
            Labs = new List<Cell>
            {
                new() { Status = LabStatus.Done, Final = 90 },
                new() { Status = LabStatus.Done, Final = 80 },
                new() { Status = LabStatus.Done, Final = 70 },
                new() { Status = LabStatus.Locked },
            }
        };

        student.Recompute();

        student.Avg.Should().Be(80);
        student.DoneCount.Should().Be(3);
        student.ReviewCount.Should().Be(0);
    }

    [Fact]
    public void RosterStudent_Recompute_AvgIsNull_WhenNoFinalScores()
    {
        var student = new RosterStudent
        {
            Labs = new List<Cell>
            {
                new() { Status = LabStatus.Review },
                new() { Status = LabStatus.Locked },
            }
        };

        student.Recompute();

        student.Avg.Should().BeNull();
    }

    [Fact]
    public void TaskItem_View_ReturnsGreenForPass()
    {
        var task = new TaskItem { State = "pass" };
        var (tone, _, icon, label) = task.View();
        icon.Should().Be("check");
        label.Should().Be("Пройдено");
        tone.Should().Contain("green");
    }

    [Fact]
    public void TaskItem_View_ReturnsYellowForWarn()
    {
        var task = new TaskItem { State = "warn" };
        var (tone, _, icon, label) = task.View();
        icon.Should().Be("warn");
        label.Should().Be("Зауваження");
        tone.Should().Contain("yellow");
    }

    [Fact]
    public void TaskItem_View_ReturnsRedForFail()
    {
        var task = new TaskItem { State = "fail" };
        var (_, _, icon, label) = task.View();
        icon.Should().Be("x");
        label.Should().Be("Помилка");
    }

    [Fact]
    public void TaskItem_ColorClass_CorrectForEachState()
    {
        new TaskItem { State = "pass" }.ColorClass.Should().Be("green");
        new TaskItem { State = "warn" }.ColorClass.Should().Be("yellow");
        new TaskItem { State = "fail" }.ColorClass.Should().Be("red");
    }
}
