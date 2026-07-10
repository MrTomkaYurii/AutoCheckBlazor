using AutoCheck.Data;
using AutoCheck.Models;
using AutoCheck.Services;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AutoCheck.Tests.Data;

/// <summary>
/// Course-wide 100-point breakdown: distribution by task difficulty, earned points
/// from finalised grades, and the 50-point exam-admission threshold.
/// </summary>
public class PointsBreakdownTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly DbDataService _svc;
    private int _student1, _student2;

    public PointsBreakdownTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var factory = new TestDbContextFactory(opts);
        _db = factory.CreateDbContext();
        _svc = new DbDataService(factory, new TokenProtector(new EphemeralDataProtectionProvider()));
        Seed();
    }

    private void Seed()
    {
        // lab1 weight = 1 + 3 = 4 ; lab2 weight = 2 + 2 = 4 ; total = 8 → each lab = 50 pts
        var lab1 = new LabDef
        {
            Number = 1, Slug = "lab-01", Title = "L1",
            Tasks =
            {
                new LabTask { Number = 1, Title = "T1", Difficulty = 1 },
                new LabTask { Number = 2, Title = "T2", Difficulty = 3 },
            },
        };
        var lab2 = new LabDef
        {
            Number = 2, Slug = "lab-02", Title = "L2",
            Tasks =
            {
                new LabTask { Number = 1, Title = "T1", Difficulty = 2 },
                new LabTask { Number = 2, Title = "T2", Difficulty = 2 },
            },
        };
        _db.Labs.AddRange(lab1, lab2);

        var s1 = new StudentRecord { FirstName = "A", LastName = "A", Group = "G", Email = "a@a", Initials = "A" };
        var s2 = new StudentRecord { FirstName = "B", LastName = "B", Group = "G", Email = "b@b", Initials = "B" };
        _db.Students.AddRange(s1, s2);
        _db.SaveChanges();
        _student1 = s1.Id;
        _student2 = s2.Id;

        _db.Submissions.AddRange(
            // student1: lab1 finalised at 80 → 50 × 0.80 = 40 pts ; lab2 not submitted (locked → 0)
            new Submission { StudentId = s1.Id, LabDefId = lab1.Id, Status = (int)LabStatus.Done, AutoScore = 85, DefenseScore = 75, FinalScore = 80 },
            // student2: both finalised at 100 → 100 pts → admitted
            new Submission { StudentId = s2.Id, LabDefId = lab1.Id, Status = (int)LabStatus.Done, FinalScore = 100 },
            new Submission { StudentId = s2.Id, LabDefId = lab2.Id, Status = (int)LabStatus.Done, FinalScore = 100 }
        );
        _db.SaveChanges();
    }

    [Fact]
    public async Task Breakdown_MaxPointsSumToExactly100()
    {
        var bd = await _svc.GetPointsBreakdownAsync(_student1);
        bd.Labs.Sum(l => l.MaxPoints).Should().BeApproximately(100, 0.001);
        bd.TotalMax.Should().Be(100);
    }

    [Fact]
    public async Task Breakdown_DistributesByDifficultyWeight()
    {
        var bd = await _svc.GetPointsBreakdownAsync(_student1);
        bd.Labs.Single(l => l.Number == 1).MaxPoints.Should().BeApproximately(50, 0.001);
        bd.Labs.Single(l => l.Number == 2).MaxPoints.Should().BeApproximately(50, 0.001);
    }

    [Fact]
    public async Task Breakdown_EarnedComesFromFinalGrade()
    {
        var bd   = await _svc.GetPointsBreakdownAsync(_student1);
        var lab1 = bd.Labs.Single(l => l.Number == 1);
        lab1.Earned.Should().BeApproximately(40, 0.001);   // 50 × 80/100
        lab1.Percent.Should().BeApproximately(80, 0.001);
    }

    [Fact]
    public async Task Breakdown_UnsubmittedLab_EarnsZeroAndIsLocked()
    {
        var bd   = await _svc.GetPointsBreakdownAsync(_student1);
        var lab2 = bd.Labs.Single(l => l.Number == 2);
        lab2.Earned.Should().Be(0);
        lab2.Status.Should().Be(LabStatus.Locked);
    }

    [Fact]
    public async Task Breakdown_TotalsAndThreshold_NotYetAdmitted()
    {
        var bd = await _svc.GetPointsBreakdownAsync(_student1);
        bd.TotalEarned.Should().BeApproximately(40, 0.001);
        bd.Threshold.Should().Be(50);
        bd.Admitted.Should().BeFalse();
        bd.Remaining.Should().BeApproximately(10, 0.001);
    }

    [Fact]
    public async Task Breakdown_AdmittedWhenThresholdReached()
    {
        var bd = await _svc.GetPointsBreakdownAsync(_student2);
        bd.TotalEarned.Should().BeApproximately(100, 0.001);
        bd.Admitted.Should().BeTrue();
        bd.Remaining.Should().Be(0);
    }

    [Fact]
    public async Task Breakdown_PerTaskPointsSplitByDifficultyAndSumToLabMax()
    {
        var bd   = await _svc.GetPointsBreakdownAsync(_student1);
        var lab1 = bd.Labs.Single(l => l.Number == 1);

        lab1.Tasks.Should().HaveCount(2);
        lab1.Tasks.Sum(t => t.MaxPoints).Should().BeApproximately(lab1.MaxPoints, 0.001);
        lab1.Tasks.Single(t => t.Number == 1).MaxPoints.Should().BeApproximately(12.5, 0.001); // 50 × 1/4
        lab1.Tasks.Single(t => t.Number == 2).MaxPoints.Should().BeApproximately(37.5, 0.001); // 50 × 3/4
    }

    public void Dispose() => _db.Dispose();
}
