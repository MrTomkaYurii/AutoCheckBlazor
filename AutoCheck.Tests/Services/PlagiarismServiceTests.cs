using AutoCheck.Data;
using AutoCheck.Models;
using AutoCheck.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AutoCheck.Tests.Services;

/// <summary>Anti-plagiarism gates: shared-repo detection, exact-copy containment, Jaccard pairs.</summary>
public class PlagiarismServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly PlagiarismService _svc;
    private LabDef _lab = null!;
    private LabTask _task = null!;
    private int _s1, _s2, _s3;

    // Distinct, non-trivial code lines (>12 chars after whitespace removal, not "using…").
    private static readonly string[] Code =
    [
        "public int CalculatePatientAge(DateTime dob)",
        "var discounted = price - price * discount / 100;",
        "return firstName + \" \" + lastName;",
        "if (age >= 18) category = \"adult\";",
        "public decimal ComputeBmi(double weight, double height)",
        "Console.WriteLine($\"[{Id}] {FullName}\");",
        "private static int _nextId = 1;",
        "public string GetAgeCategory() => Age < 18 ? \"child\" : \"adult\";",
        "appointments.Add(new Appointment(patient, doctor));",
        "throw new ArgumentException(\"invalid input value\");",
        "var sorted = queue.OrderBy(x => x.Priority).ToList();",
        "public bool IsAvailableNow => Hour >= Start && Hour < End;",
        "records.RemoveAll(r => r.PatientId == patientId);",
        "return doctors.Where(d => d.Speciality == speciality);",
        "total += patients.Sum(p => p.VisitCount);",
        "public override string ToString() => BuildSummary();",
    ];

    public PlagiarismServiceTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var factory = new TestDbContextFactory(opts);
        _db = factory.CreateDbContext();
        _svc = new PlagiarismService(factory);
        Seed();
    }

    private void Seed()
    {
        _lab = new LabDef { Number = 1, Slug = "lab-01", Title = "L1", Tasks = { new LabTask { Number = 1, Title = "T1", Difficulty = 1 } } };
        _db.Labs.Add(_lab);

        // s1 and s2 point at the SAME repo (different URL spelling); s3 has its own.
        var s1 = new StudentRecord { FirstName = "A", LastName = "Alpha", Group = "G1", Email = "a@a", Initials = "A", Github = "https://github.com/alpha/lab" };
        var s2 = new StudentRecord { FirstName = "B", LastName = "Beta",  Group = "G2", Email = "b@b", Initials = "B", Github = "http://www.github.com/ALPHA/lab.git" };
        var s3 = new StudentRecord { FirstName = "C", LastName = "Gamma", Group = "G1", Email = "c@c", Initials = "C", Github = "https://github.com/gamma/other" };
        _db.Students.AddRange(s1, s2, s3);
        _db.SaveChanges();

        _task = _lab.Tasks.First();
        _s1 = s1.Id; _s2 = s2.Id; _s3 = s3.Id;
    }

    private void AddSubmission(int studentId, IEnumerable<string> addLines)
    {
        var sub = new Submission { StudentId = studentId, LabDefId = _lab.Id, Status = (int)LabStatus.Done, AutoScore = 90, FinalScore = 85 };
        var tr  = new TaskResult { LabTaskId = _task.Id, AttemptNo = 1, State = "pass", Score = 90 };
        int i = 0;
        foreach (var line in addLines)
            tr.DiffLines.Add(new DiffEntry { OrderIndex = i++, Type = "add", Text = line });
        sub.TaskResults.Add(tr);
        _db.Submissions.Add(sub);
        _db.SaveChanges();
    }

    // ── FindSharedRepoAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task FindSharedRepo_DetectsSameRepoAcrossUrlFormats()
    {
        var hit = await _svc.FindSharedRepoAsync(_s1, "https://github.com/alpha/lab");
        hit.Should().NotBeNull();
        hit!.StudentName.Should().Be("Beta B");
        hit.Group.Should().Be("G2");
    }

    [Fact]
    public async Task FindSharedRepo_ReturnsNull_WhenRepoIsUnique() =>
        (await _svc.FindSharedRepoAsync(_s3, "https://github.com/gamma/other")).Should().BeNull();

    [Fact]
    public async Task FindSharedRepo_ReturnsNull_ForEmptyUrl() =>
        (await _svc.FindSharedRepoAsync(_s1, "")).Should().BeNull();

    // ── FindExactMatchAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task FindExactMatch_IdenticalCode_ReturnsFullContainment()
    {
        AddSubmission(_s2, Code);
        var match = await _svc.FindExactMatchAsync(_lab.Id, _s1, Code);

        match.Should().NotBeNull();
        match!.Containment.Should().BeApproximately(1.0, 0.001);
        match.StudentName.Should().Be("Beta B");
    }

    [Fact]
    public async Task FindExactMatch_PartialOverlap_ReturnsNull()
    {
        AddSubmission(_s2, Code);   // 16 lines
        // candidate: 8 shared + 2 unique = 10 lines → containment 8/16 = 0.5 < 0.98
        var candidate = Code.Take(8)
            .Concat(new[] { "var uniqueLineAlpha = 1234567;", "var uniqueLineBravo = 7654321;" })
            .ToArray();

        (await _svc.FindExactMatchAsync(_lab.Id, _s1, candidate)).Should().BeNull();
    }

    [Fact]
    public async Task FindExactMatch_TooFewCandidateLines_ReturnsNull()
    {
        AddSubmission(_s2, Code);
        (await _svc.FindExactMatchAsync(_lab.Id, _s1, Code.Take(5))).Should().BeNull();
    }

    // ── CheckLabAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task CheckLab_IdenticalSubmissions_FlagsHighSimilarityPair()
    {
        AddSubmission(_s1, Code);
        AddSubmission(_s2, Code);

        var pairs = await _svc.CheckLabAsync(labNumber: 1, threshold: 0.5);

        pairs.Should().ContainSingle();
        pairs[0].Similarity.Should().BeApproximately(1.0, 0.001);
    }

    [Fact]
    public async Task CheckLab_DisjointSubmissions_NoPairs()
    {
        AddSubmission(_s1, Code.Take(8));
        AddSubmission(_s2, Code.Skip(8).Take(8));   // no lines in common

        (await _svc.CheckLabAsync(labNumber: 1, threshold: 0.5)).Should().BeEmpty();
    }

    public void Dispose() => _db.Dispose();
}
