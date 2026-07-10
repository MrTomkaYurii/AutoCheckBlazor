using AutoCheck.Data;
using AutoCheck.Models;
using AutoCheck.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AutoCheck.Tests.Services;

/// <summary>
/// Verifies the Roslyn structural detector: renamed-but-identical code must score high,
/// genuinely different code must score low.
/// </summary>
public class CodeSimilarityServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly CodeSimilarityService _svc;
    private int _aId, _bId, _cId;

    private const string CodeA = """
        public class Calculator
        {
            public int Add(int a, int b)
            {
                int result = a + b;
                return result;
            }
        }
        """;

    // Same structure as A, every identifier renamed — the whole point of structural detection.
    private const string CodeB = """
        public class MathHelper
        {
            public int Sum(int x, int y)
            {
                int total = x + y;
                return total;
            }
        }
        """;

    // Genuinely different logic.
    private const string CodeC = """
        public class Printer
        {
            public void Show(string message)
            {
                for (int i = 0; i < 10; i++)
                {
                    System.Console.WriteLine(message);
                }
            }
        }
        """;

    public CodeSimilarityServiceTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var factory = new TestDbContextFactory(opts);
        _db = factory.CreateDbContext();
        _svc = new CodeSimilarityService(factory);

        var lab = new LabDef { Number = 5, Slug = "lab-05", Title = "Lab", OrderIndex = 0 };
        _db.Labs.Add(lab);
        _db.SaveChanges();

        _aId = SeedStudent(lab.Id, "Alpha", CodeA);
        _bId = SeedStudent(lab.Id, "Beta", CodeB);
        _cId = SeedStudent(lab.Id, "Gamma", CodeC);
    }

    private int SeedStudent(int labId, string last, string code)
    {
        var s = new StudentRecord { FirstName = "Test", LastName = last, Group = "КН-31", Initials = "T" + last[0] };
        _db.Students.Add(s);
        _db.SaveChanges();

        var sub = new Submission { StudentId = s.Id, LabDefId = labId, Status = (int)LabStatus.Review, AttemptsMax = 3 };
        _db.Submissions.Add(sub);
        _db.SaveChanges();

        var tr = new TaskResult { SubmissionId = sub.Id, LabTaskId = 0, AttemptNo = 1, State = "warn", Score = 60 };
        var lines = code.Replace("\r", "").Split('\n');
        for (int i = 0; i < lines.Length; i++)
            tr.DiffLines.Add(new DiffEntry { OrderIndex = i, Type = "add", N2 = i + 1, Text = lines[i] });
        _db.TaskResults.Add(tr);
        _db.SaveChanges();
        return s.Id;
    }

    [Fact]
    public async Task FindSimilar_RankasRenamedCopyFirstWithHighScore()
    {
        var result = await _svc.FindSimilarAsync(labNumber: 5, studentId: _aId);

        result.Should().NotBeEmpty();
        result[0].StudentId.Should().Be(_bId, "renamed-but-identical code must be the closest match");
        result[0].Percent.Should().BeGreaterThanOrEqualTo(80, "identifier renaming must not hide structural identity");
    }

    [Fact]
    public async Task FindSimilar_DifferentCodeScoresLowerThanRenamedCopy()
    {
        var result = await _svc.FindSimilarAsync(labNumber: 5, studentId: _aId);

        var b = result.First(r => r.StudentId == _bId);
        var c = result.FirstOrDefault(r => r.StudentId == _cId);
        (c?.Percent ?? 0).Should().BeLessThan(b.Percent, "unrelated code must be less similar than a renamed copy");
    }

    [Fact]
    public async Task GetOverlap_FlagsMatchingLines()
    {
        var overlap = await _svc.GetOverlapAsync(labNumber: 5, studentId: _aId, otherStudentId: _bId);

        overlap.Should().NotBeNull();
        overlap!.Target.Count(l => l.Matched).Should().BeGreaterThan(0, "identical structure should light up matching lines");
    }

    public void Dispose() => _db.Dispose();
}
