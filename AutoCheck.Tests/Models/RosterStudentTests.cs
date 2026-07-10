using AutoCheck.Models;
using FluentAssertions;
using Xunit;

namespace AutoCheck.Tests.Models;

/// <summary>Gradebook row aggregation for the teacher journal.</summary>
public class RosterStudentTests
{
    private static Cell C(LabStatus s, int? final = null) => new() { Status = s, Final = final };

    private static RosterStudent Row(params Cell[] cells)
    {
        var rs = new RosterStudent { Labs = cells.ToList() };
        rs.Recompute();
        return rs;
    }

    [Fact]
    public void Recompute_CountsDoneReviewAndSubmissions()
    {
        var rs = Row(
            C(LabStatus.Done, 90),
            C(LabStatus.Done, 80),
            C(LabStatus.Review),
            C(LabStatus.Rejected),
            C(LabStatus.Locked));

        rs.DoneCount.Should().Be(2);
        rs.ReviewCount.Should().Be(1);
        rs.Submissions.Should().Be(4);   // everything except Locked
    }

    [Fact]
    public void Recompute_AverageUsesOnlyDoneFinals()
    {
        var rs = Row(
            C(LabStatus.Done, 90),
            C(LabStatus.Done, 80),
            C(LabStatus.Review, 100));   // review is ignored even with a Final

        rs.Avg.Should().Be(85);
    }

    [Fact]
    public void Recompute_DoneWithoutFinal_ExcludedFromAverage()
    {
        var rs = Row(C(LabStatus.Done, 60), C(LabStatus.Done, null));
        rs.Avg.Should().Be(60);
    }

    [Fact]
    public void Recompute_NoDoneFinals_AverageIsNull()
    {
        var rs = Row(C(LabStatus.Review), C(LabStatus.Locked));
        rs.Avg.Should().BeNull();
    }
}
