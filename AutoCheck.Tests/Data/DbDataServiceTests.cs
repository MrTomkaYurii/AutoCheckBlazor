using AutoCheck.Data;
using AutoCheck.Models;
using AutoCheck.Services;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AutoCheck.Tests.Data;

/// <summary>Tests for DbDataService — the main read-only data service.</summary>
public class DbDataServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly DbDataService _svc;

    public DbDataServiceTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(opts);
        _svc = new DbDataService(_db, new TokenProtector(new EphemeralDataProtectionProvider()));
        SeedData();
    }

    private void SeedData()
    {
        var lab1 = new LabDef { Number = 1, Slug = "lab-01-intro", Title = "Основи C#",   OrderIndex = 0, Goal = "Навчитися" };
        var lab2 = new LabDef { Number = 2, Slug = "lab-02-arrays", Title = "Масиви",       OrderIndex = 1 };
        _db.Labs.AddRange(lab1, lab2);

        var student1 = new StudentRecord { FirstName = "Петро", LastName = "Іваненко", Group = "КН-31", Email = "p@test.com", Initials = "ПІ" };
        var student2 = new StudentRecord { FirstName = "Олена", LastName = "Коваль",   Group = "КН-32", Email = "o@test.com", Initials = "ОК" };
        _db.Students.AddRange(student1, student2);

        _db.Teachers.Add(new TeacherRecord { FirstName = "Тест", LastName = "Викладач", Initials = "ТВ", Title = "Доцент", Course = "ООП" });
        _db.SaveChanges();

        _db.Submissions.AddRange(
            new Submission { StudentId = student1.Id, LabDefId = lab1.Id, Status = (int)LabStatus.Done,   AutoScore = 90, DefenseScore = 88, FinalScore = 89, AttemptsMax = 3 },
            new Submission { StudentId = student1.Id, LabDefId = lab2.Id, Status = (int)LabStatus.Review, AutoScore = 74, AttemptsMax = 3 },
            new Submission { StudentId = student2.Id, LabDefId = lab1.Id, Status = (int)LabStatus.Done,   AutoScore = 95, DefenseScore = 92, FinalScore = 93, AttemptsMax = 3 },
            new Submission { StudentId = student2.Id, LabDefId = lab2.Id, Status = (int)LabStatus.Locked, AttemptsMax = 3 }
        );
        _db.SaveChanges();
    }

    // ── GetStudentLabsAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task GetStudentLabsAsync_ReturnsTwoLabsForStudent1()
    {
        var student1 = _db.Students.First(s => s.LastName == "Іваненко");
        var labs = await _svc.GetStudentLabsAsync(student1.Id);
        labs.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetStudentLabsAsync_LabsOrderedByNumber()
    {
        var id = _db.Students.First().Id;
        var labs = await _svc.GetStudentLabsAsync(id);
        labs[0].Id.Should().BeLessThan(labs[1].Id);  // Id = lab Number
    }

    [Fact]
    public async Task GetStudentLabsAsync_StatusMappedCorrectly()
    {
        var id = _db.Students.First(s => s.LastName == "Іваненко").Id;
        var labs = await _svc.GetStudentLabsAsync(id);

        labs.First(l => l.Id == 1).Status.Should().Be(LabStatus.Done);
        labs.First(l => l.Id == 2).Status.Should().Be(LabStatus.Review);
    }

    [Fact]
    public async Task GetStudentLabsAsync_ScoresMappedCorrectly()
    {
        var id = _db.Students.First(s => s.LastName == "Іваненко").Id;
        var labs = await _svc.GetStudentLabsAsync(id);
        var doneLab = labs.First(l => l.Status == LabStatus.Done);

        doneLab.Auto.Should().Be(90);
        doneLab.Defense.Should().Be(88);
        doneLab.Final.Should().Be(89);
    }

    // ── GetLabColumnsAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task GetLabColumnsAsync_ReturnsBothLabs()
    {
        var cols = await _svc.GetLabColumnsAsync();
        cols.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetLabColumnsAsync_ShortFormatIsCorrect()
    {
        var cols = await _svc.GetLabColumnsAsync();
        cols[0].Short.Should().Be("L01");
        cols[1].Short.Should().Be("L02");
    }

    // ── GetTeacherGroupsAsync ───────────────────────────────────────────────

    [Fact]
    public async Task GetTeacherGroupsAsync_IncludesAllGroupsOption()
    {
        var groups = await _svc.GetTeacherGroupsAsync();
        groups.Should().Contain("Усі групи");
    }

    [Fact]
    public async Task GetTeacherGroupsAsync_ContainsBothGroups()
    {
        var groups = await _svc.GetTeacherGroupsAsync();
        groups.Should().Contain("КН-31");
        groups.Should().Contain("КН-32");
    }

    // ── GetStatsAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetStatsAsync_StudentsCountIsCorrect()
    {
        var stats = await _svc.GetStatsAsync();
        stats.Students.Should().Be(2);
    }

    [Fact]
    public async Task GetStatsAsync_PendingReviewCountIsCorrect()
    {
        var stats = await _svc.GetStatsAsync();
        stats.PendingReview.Should().Be(1);
    }

    [Fact]
    public async Task GetStatsAsync_AvgGradeIsCalculated()
    {
        var stats = await _svc.GetStatsAsync();
        stats.AvgGrade.Should().BeGreaterThan(0);
    }

    // ── GetRosterAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetRosterAsync_ReturnsBothStudents()
    {
        var roster = await _svc.GetRosterAsync();
        roster.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetRosterAsync_EachStudentHasLabCells()
    {
        var roster = await _svc.GetRosterAsync();
        roster.Should().AllSatisfy(s => s.Labs.Should().HaveCount(2));
    }

    [Fact]
    public async Task GetRosterAsync_ComputesAverageForDoneStudents()
    {
        var roster = await _svc.GetRosterAsync();
        var student1 = roster.First(s => s.Last == "Іваненко");
        student1.Avg.Should().NotBeNull();
    }

    // ── GetReviewQueueAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task GetReviewQueueAsync_ReturnsOnlyReviewItems()
    {
        var queue = await _svc.GetReviewQueueAsync();
        queue.Should().HaveCount(1);
        queue[0].Auto.Should().Be(74);
    }

    // ── GetCurrentStudentAsync ──────────────────────────────────────────────

    [Fact]
    public async Task GetCurrentStudentAsync_ReturnsFirstStudent()
    {
        var student = await _svc.GetCurrentStudentAsync();
        student.Should().NotBeNull();
        student.FirstName.Should().NotBeEmpty();
    }

    // ── GetTeacherAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetTeacherAsync_ReturnsTeacher()
    {
        var teacher = await _svc.GetTeacherAsync();
        teacher.FirstName.Should().Be("Тест");
        teacher.Title.Should().Be("Доцент");
    }

    public void Dispose() => _db.Dispose();
}
