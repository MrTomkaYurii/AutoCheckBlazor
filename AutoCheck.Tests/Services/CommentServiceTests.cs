using AutoCheck.Data;
using AutoCheck.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AutoCheck.Tests.Services;

public class CommentServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly CommentService _svc;
    private int _submissionId;

    public CommentServiceTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(opts);
        _svc = new CommentService(_db);

        // Seed minimal required data
        var student = new StudentRecord { FirstName = "Test", LastName = "User", Group = "КН-31", Initials = "TU" };
        _db.Students.Add(student);
        var lab = new LabDef { Number = 1, Slug = "lab-01", Title = "Test Lab", OrderIndex = 0 };
        _db.Labs.Add(lab);
        _db.SaveChanges();

        var sub = new Submission { StudentId = student.Id, LabDefId = lab.Id, Status = 1, AttemptsMax = 3 };
        _db.Submissions.Add(sub);
        _db.SaveChanges();
        _submissionId = sub.Id;
    }

    [Fact]
    public async Task AddAsync_CreatesComment()
    {
        await _svc.AddAsync(_submissionId, null, "teacher", "Олена Ковальчук", "Гарна робота!");

        var comments = await _db.Comments.ToListAsync();
        comments.Should().HaveCount(1);
        comments[0].Text.Should().Be("Гарна робота!");
        comments[0].AuthorRole.Should().Be("teacher");
        comments[0].AuthorName.Should().Be("Олена Ковальчук");
    }

    [Fact]
    public async Task GetForSubmissionAsync_ReturnsAllComments()
    {
        await _svc.AddAsync(_submissionId, null, "teacher", "Викладач", "Коментар 1");
        await _svc.AddAsync(_submissionId, null, "student", "Студент",  "Коментар 2");

        var comments = await _svc.GetForSubmissionAsync(_submissionId);
        comments.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetForSubmissionAsync_OrdersByCreatedAt()
    {
        await _svc.AddAsync(_submissionId, null, "teacher", "Перший",  "Перший коментар");
        await Task.Delay(10);
        await _svc.AddAsync(_submissionId, null, "student", "Другий",  "Другий коментар");

        var comments = await _svc.GetForSubmissionAsync(_submissionId);
        comments[0].AuthorName.Should().Be("Перший");
        comments[1].AuthorName.Should().Be("Другий");
    }

    [Fact]
    public async Task DeleteAsync_RemovesComment()
    {
        await _svc.AddAsync(_submissionId, null, "teacher", "Автор", "Текст");
        var id = _db.Comments.First().Id;

        await _svc.DeleteAsync(id);

        _db.Comments.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteAsync_NonExistentId_DoesNotThrow()
    {
        var act = async () => await _svc.DeleteAsync(9999);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetForSubmissionAsync_ReturnsEmpty_WhenNoComments()
    {
        var result = await _svc.GetForSubmissionAsync(_submissionId);
        result.Should().BeEmpty();
    }

    public void Dispose() => _db.Dispose();
}
