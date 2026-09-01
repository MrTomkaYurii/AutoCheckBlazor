using AutoCheck.Data;
using AutoCheck.Models;
using AutoCheck.Services;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AutoCheck.Tests.Data;

/// <summary>
/// Перевіряє, що лаба з <see cref="LabDef.IsActive"/> == false повністю
/// виключається зі студентських і викладацьких вибірок DbDataService,
/// але дані про неї лишаються в БД.
/// </summary>
public class DbDataServiceInactiveLabTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly DbDataService _svc;
    private int _activeLabId;
    private int _inactiveLabId;
    private int _studentId;

    public DbDataServiceInactiveLabTests()
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
        var active = new LabDef { Number = 1, Slug = "lab-01", Title = "Активна", OrderIndex = 0, IsActive = true };
        var inactive = new LabDef { Number = 2, Slug = "lab-02", Title = "Вимкнена", OrderIndex = 1, IsActive = false };
        _db.Labs.AddRange(active, inactive);
        _db.Labs.Add(new LabDef { Number = 3, Slug = "lab-03", Title = "Активна 2", OrderIndex = 2, IsActive = true });

        var st = new StudentRecord { FirstName = "Іван", LastName = "Тест", Group = "КН-31", Email = "i@t.com", Initials = "ІТ" };
        _db.Students.Add(st);
        _db.Teachers.Add(new TeacherRecord { FirstName = "Т", LastName = "В", Initials = "ТВ", Title = "Доцент", Course = "ООП" });
        _db.SaveChanges();

        // студент має здачі в обох — і в активній, і у вимкненій лабі
        _db.Submissions.AddRange(
            new Submission { StudentId = st.Id, LabDefId = active.Id,   Status = (int)LabStatus.Done,   AutoScore = 90, FinalScore = 88, AttemptsMax = 3 },
            new Submission { StudentId = st.Id, LabDefId = inactive.Id, Status = (int)LabStatus.Review, AutoScore = 70, AttemptsMax = 3 }
        );
        _db.SaveChanges();

        _activeLabId   = active.Id;
        _inactiveLabId = inactive.Id;
        _studentId     = st.Id;
    }

    [Fact]
    public async Task GetStudentLabsAsync_SkipsInactiveLab()
    {
        var labs = await _svc.GetStudentLabsAsync(_studentId);

        labs.Should().OnlyContain(l => l.Title != "Вимкнена");
        labs.Should().ContainSingle(l => l.Title == "Активна");
    }

    [Fact]
    public async Task GetLabDetailAsync_ReturnsNullForInactiveLab()
    {
        (await _svc.GetLabDetailAsync(2, _studentId)).Should().BeNull();
        (await _svc.GetLabDetailAsync(1, _studentId)).Should().NotBeNull();
    }

    [Fact]
    public async Task GetLabColumnsAsync_ExcludesInactiveLab()
    {
        var cols = await _svc.GetLabColumnsAsync();

        cols.Select(c => c.Id).Should().BeEquivalentTo(new[] { 1, 3 });
    }

    [Fact]
    public async Task GetLabStatsAsync_ExcludesInactiveLab()
    {
        var stats = await _svc.GetLabStatsAsync();

        stats.Should().NotContain(s => s.Id == 2);
    }

    [Fact]
    public async Task GetPointsBreakdownAsync_WeightsOnlyActiveLabs()
    {
        var bd = await _svc.GetPointsBreakdownAsync(_studentId);

        bd.Labs.Should().OnlyContain(l => l.Number == 1 || l.Number == 3);
        bd.Labs.Sum(l => l.MaxPoints).Should().BeApproximately(100.0, 0.01);
    }

    [Fact]
    public async Task GetRosterAsync_MatrixHasNoInactiveColumn()
    {
        var roster = await _svc.GetRosterAsync();

        // 2 активні лаби → 2 клітинки в рядку кожного студента
        roster.Should().OnlyContain(r => r.Labs.Count == 2);
    }

    [Fact]
    public async Task GetReviewQueueAsync_DropsInactiveLabSubmissions()
    {
        // єдина здача у статусі Review належить вимкненій лабі
        (await _svc.GetReviewQueueAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task InactiveLab_StillExistsInDatabase()
    {
        (await _db.Labs.FindAsync(_inactiveLabId)).Should().NotBeNull();
        (await _db.Submissions.CountAsync(s => s.LabDefId == _inactiveLabId)).Should().Be(1);
    }

    public void Dispose() => _db.Dispose();
}
