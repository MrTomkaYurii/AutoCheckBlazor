using AutoCheck.Data;
using AutoCheck.Models;
using AutoCheck.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace AutoCheck.Tests.Services;

public class LabManagementServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly LabManagementService _svc;

    public LabManagementServiceTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var factory = new TestDbContextFactory(opts);
        _db = factory.CreateDbContext();

        var envMock = new Mock<IWebHostEnvironment>();
        envMock.Setup(e => e.ContentRootPath).Returns(Path.GetTempPath());
        _svc = new LabManagementService(factory, envMock.Object);
    }

    // ── Lab CRUD ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_AddsLabToDatabase()
    {
        var deadline = new DateTime(2025, 6, 5, 0, 0, 0, DateTimeKind.Utc);
        var dto = new CreateLabDto(1, "Основи C#", "Мета лаби", "sandbox/intro", false, deadline);
        await _svc.CreateAsync(dto);

        var labs = await _db.Labs.ToListAsync();
        labs.Should().HaveCount(1);
        labs[0].Title.Should().Be("Основи C#");
        labs[0].Number.Should().Be(1);
        labs[0].Deadline.Should().Be(deadline);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesExistingLab()
    {
        _db.Labs.Add(new LabDef { Number = 1, Slug = "lab-01", Title = "Старий", OrderIndex = 0 });
        await _db.SaveChangesAsync();
        var lab = _db.Labs.First();

        await _svc.UpdateAsync(lab.Id, new CreateLabDto(1, "Новий заголовок", null, null, false, null));

        _db.ChangeTracker.Clear();   // service saved via its own context — re-read from store
        var updated = await _db.Labs.FindAsync(lab.Id);
        updated!.Title.Should().Be("Новий заголовок");
    }

    [Fact]
    public async Task DeleteAsync_RemovesLab()
    {
        _db.Labs.Add(new LabDef { Number = 1, Slug = "lab-01", Title = "To Delete", OrderIndex = 0 });
        await _db.SaveChangesAsync();
        var id = _db.Labs.First().Id;

        await _svc.DeleteAsync(id);

        _db.Labs.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllLabsOrderedByNumber()
    {
        _db.Labs.AddRange(
            new LabDef { Number = 3, Slug = "lab-03", Title = "Три",  OrderIndex = 2 },
            new LabDef { Number = 1, Slug = "lab-01", Title = "Один", OrderIndex = 0 },
            new LabDef { Number = 2, Slug = "lab-02", Title = "Два",  OrderIndex = 1 }
        );
        await _db.SaveChangesAsync();

        var labs = await _svc.GetAllAsync();

        labs.Should().HaveCount(3);
        labs[0].Number.Should().Be(1);
        labs[1].Number.Should().Be(2);
        labs[2].Number.Should().Be(3);
    }

    [Fact]
    public async Task SetActiveAsync_TogglesFlagButKeepsLab()
    {
        _db.Labs.Add(new LabDef { Number = 1, Slug = "lab-01", Title = "Lab", OrderIndex = 0 });
        await _db.SaveChangesAsync();
        var id = _db.Labs.First().Id;

        await _svc.SetActiveAsync(id, false);

        _db.ChangeTracker.Clear();   // service saved via its own context — re-read from store
        var lab = await _db.Labs.FindAsync(id);
        lab.Should().NotBeNull();            // не видалено
        lab!.IsActive.Should().BeFalse();

        await _svc.SetActiveAsync(id, true);
        _db.ChangeTracker.Clear();
        (await _db.Labs.FindAsync(id))!.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GetAllAsync_IncludesInactiveLabs()
    {
        _db.Labs.AddRange(
            new LabDef { Number = 1, Slug = "lab-01", Title = "Активна",  OrderIndex = 0, IsActive = true },
            new LabDef { Number = 2, Slug = "lab-02", Title = "Вимкнена", OrderIndex = 1, IsActive = false }
        );
        await _db.SaveChangesAsync();

        var labs = await _svc.GetAllAsync();

        labs.Should().HaveCount(2);   // сторінка керування бачить усі
    }

    // ── Task CRUD ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateTaskAsync_AddsTaskToLab()
    {
        _db.Labs.Add(new LabDef { Number = 1, Slug = "lab-01", Title = "Test", OrderIndex = 0 });
        await _db.SaveChangesAsync();
        var labId = _db.Labs.First().Id;

        var dto = new CreateTaskDto(1, "Клас Patient", "Напишіть клас Patient.", 2);
        await _svc.CreateTaskAsync(labId, dto);

        var tasks = await _db.LabTasks.ToListAsync();
        tasks.Should().HaveCount(1);
        tasks[0].Title.Should().Be("Клас Patient");
        tasks[0].Difficulty.Should().Be(2);
        tasks[0].LabDefId.Should().Be(labId);
    }

    [Fact]
    public async Task UpdateTaskAsync_UpdatesTaskFields()
    {
        _db.Labs.Add(new LabDef { Number = 1, Slug = "lab-01", Title = "Lab", OrderIndex = 0 });
        await _db.SaveChangesAsync();
        var labId = _db.Labs.First().Id;
        _db.LabTasks.Add(new LabTask { LabDefId = labId, Number = 1, Title = "Старе", Difficulty = 1 });
        await _db.SaveChangesAsync();
        var taskId = _db.LabTasks.First().Id;

        await _svc.UpdateTaskAsync(taskId, new CreateTaskDto(1, "Нове", "Новий brief", 3));

        _db.ChangeTracker.Clear();   // service saved via its own context — re-read from store
        var t = await _db.LabTasks.FindAsync(taskId);
        t!.Title.Should().Be("Нове");
        t.Difficulty.Should().Be(3);
        t.Brief.Should().Be("Новий brief");
    }

    [Fact]
    public async Task DeleteTaskAsync_RemovesTask()
    {
        _db.Labs.Add(new LabDef { Number = 1, Slug = "lab-01", Title = "Lab", OrderIndex = 0 });
        await _db.SaveChangesAsync();
        var labId = _db.Labs.First().Id;
        _db.LabTasks.Add(new LabTask { LabDefId = labId, Number = 1, Title = "Task", Difficulty = 1 });
        await _db.SaveChangesAsync();
        var taskId = _db.LabTasks.First().Id;

        await _svc.DeleteTaskAsync(taskId);

        _db.LabTasks.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTasksAsync_ReturnsTasksOrderedByNumber()
    {
        _db.Labs.Add(new LabDef { Number = 1, Slug = "lab-01", Title = "Lab", OrderIndex = 0 });
        await _db.SaveChangesAsync();
        var labId = _db.Labs.First().Id;
        _db.LabTasks.AddRange(
            new LabTask { LabDefId = labId, Number = 3, Title = "Task 3", Difficulty = 1 },
            new LabTask { LabDefId = labId, Number = 1, Title = "Task 1", Difficulty = 1 },
            new LabTask { LabDefId = labId, Number = 2, Title = "Task 2", Difficulty = 2 }
        );
        await _db.SaveChangesAsync();

        var tasks = await _svc.GetTasksAsync(labId);

        tasks[0].Number.Should().Be(1);
        tasks[1].Number.Should().Be(2);
        tasks[2].Number.Should().Be(3);
    }

    public void Dispose() => _db.Dispose();
}
