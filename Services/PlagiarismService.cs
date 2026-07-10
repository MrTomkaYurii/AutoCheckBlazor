using AutoCheck.Data;
using Microsoft.EntityFrameworkCore;

namespace AutoCheck.Services;

public record PlagPair(
    int StudentAId, string StudentA, string GroupA,
    int StudentBId, string StudentB, string GroupB,
    double Similarity, int SharedLines);

/// <summary>
/// Cross-checks students' submissions of one lab for copied code:
/// Jaccard similarity over normalized added-code lines from stored diffs.
/// </summary>
public class PlagiarismService(IDbContextFactory<AppDbContext> dbf)
{
    public async Task<List<PlagPair>> CheckLabAsync(int labNumber, double threshold = 0.5)
    {
        await using var db = await dbf.CreateDbContextAsync();
        var rawSubs = await db.Submissions.AsNoTracking()
            .Where(s => s.LabDef.Number == labNumber && s.TaskResults.Any())
            .Select(s => new
            {
                s.StudentId,
                Name  = s.Student.LastName + " " + s.Student.FirstName,
                s.Student.Group,
                TaskResults = s.TaskResults.Select(tr => new
                {
                    tr.AttemptNo,
                    Add = tr.DiffLines.Where(d => d.Type == "add").Select(d => d.Text).ToList(),
                    Ctx = tr.DiffLines.Where(d => d.Type == "ctx").Select(d => d.Text).ToList(),
                }).ToList(),
            })
            .ToListAsync();

        // Compare only each submission's LATEST attempt — mixing lines from earlier,
        // superseded attempts in with the current one dilutes the similarity ratio.
        var subs = rawSubs.Select(s =>
        {
            var latest = s.TaskResults.Count > 0 ? s.TaskResults.Max(tr => tr.AttemptNo) : 0;
            var results = s.TaskResults.Where(tr => tr.AttemptNo == latest).ToList();
            var add = results.SelectMany(tr => tr.Add).ToList();
            // Commit-mapped submissions expose their code as diff "add" lines; a
            // submission graded WITHOUT a commit map stores it as "ctx" (context) lines.
            // Fall back to ctx so those full-file copies stay comparable — otherwise a
            // student can dodge the check simply by not mapping commits.
            var lines = add.Count > 0 ? add : results.SelectMany(tr => tr.Ctx).ToList();
            return new { s.StudentId, s.Name, s.Group, Lines = lines };
        }).ToList();

        // normalize: drop whitespace; skip short/trivial lines (braces, usings…)
        var sets = subs
            .Select(s => new
            {
                s.StudentId, s.Name, s.Group,
                Set = s.Lines
                    .Select(Normalize)
                    .Where(l => l.Length >= 12 && !l.StartsWith("using"))
                    .ToHashSet(),
            })
            .Where(s => s.Set.Count >= 5)   // too little code → comparison is noise
            .ToList();

        var pairs = new List<PlagPair>();
        for (int i = 0; i < sets.Count; i++)
        for (int j = i + 1; j < sets.Count; j++)
        {
            var a = sets[i]; var b = sets[j];
            var shared = a.Set.Count(l => b.Set.Contains(l));
            if (shared == 0) continue;
            var union = a.Set.Count + b.Set.Count - shared;
            var sim = (double)shared / union;
            if (sim >= threshold)
                pairs.Add(new PlagPair(
                    a.StudentId, a.Name, a.Group,
                    b.StudentId, b.Name, b.Group,
                    sim, shared));
        }

        return pairs.OrderByDescending(p => p.Similarity).ToList();
    }

    private static string Normalize(string line) =>
        new(line.Where(c => !char.IsWhiteSpace(c)).ToArray());

    public record RepoConflict(string StudentName, string Group);

    /// <summary>
    /// Repository-substitution guard: is another student's profile pointing at the
    /// SAME GitHub repo as this one? Legit students each have their own repo/fork, so a
    /// shared URL means one student put another's repository in their profile.
    /// Returns the first conflicting student, or null.
    /// </summary>
    public async Task<RepoConflict?> FindSharedRepoAsync(int studentId, string repoUrl)
    {
        var mine = NormalizeRepo(repoUrl);
        if (string.IsNullOrEmpty(mine)) return null;

        await using var db = await dbf.CreateDbContextAsync();
        var others = await db.Students.AsNoTracking()
            .Where(s => s.Id != studentId && s.Github != "")
            .Select(s => new { s.LastName, s.FirstName, s.Group, s.Github })
            .ToListAsync();

        var hit = others.FirstOrDefault(o => NormalizeRepo(o.Github) == mine);
        return hit is null ? null : new RepoConflict(hit.LastName + " " + hit.FirstName, hit.Group);
    }

    /// <summary>owner/repo, case-insensitive, without scheme/host/.git — for comparing two URLs.</summary>
    private static string NormalizeRepo(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        var s = raw.Trim().ToLowerInvariant()
            .Replace("https://", "").Replace("http://", "").Replace("www.", "")
            .TrimEnd('/');
        if (s.StartsWith("github.com/")) s = s["github.com/".Length..];
        if (s.EndsWith(".git")) s = s[..^4];
        return s;
    }

    public record ExactMatch(string StudentName, string Group, double Containment);

    /// <summary>
    /// Submission-time gate: does the candidate code fully contain another
    /// student's already-checked work for this lab? Returns the best match with
    /// ≥98% containment (≈ 100% copy), or null.
    /// </summary>
    public async Task<ExactMatch?> FindExactMatchAsync(
        int labDefId, int studentId, IEnumerable<string> candidateLines)
    {
        var candidate = candidateLines
            .Select(Normalize)
            .Where(l => l.Length >= 12 && !l.StartsWith("using"))
            .ToHashSet();
        if (candidate.Count < 10) return null;   // too little code to judge

        await using var db = await dbf.CreateDbContextAsync();
        var rawOthers = await db.Submissions.AsNoTracking()
            .Where(s => s.LabDefId == labDefId && s.StudentId != studentId && s.TaskResults.Any())
            .Select(s => new
            {
                Name  = s.Student.LastName + " " + s.Student.FirstName,
                s.Student.Group,
                TaskResults = s.TaskResults.Select(tr => new
                {
                    tr.AttemptNo,
                    Add = tr.DiffLines.Where(d => d.Type == "add").Select(d => d.Text).ToList(),
                    Ctx = tr.DiffLines.Where(d => d.Type == "ctx").Select(d => d.Text).ToList(),
                }).ToList(),
            })
            .ToListAsync();

        // Compare against each other student's LATEST attempt only — the same
        // "don't dilute with stale attempts" reasoning as CheckLabAsync above.
        var others = rawOthers.Select(s =>
        {
            var latest = s.TaskResults.Count > 0 ? s.TaskResults.Max(tr => tr.AttemptNo) : 0;
            var results = s.TaskResults.Where(tr => tr.AttemptNo == latest).ToList();
            var add = results.SelectMany(tr => tr.Add).ToList();
            var lines = add.Count > 0 ? add : results.SelectMany(tr => tr.Ctx).ToList();
            return new { s.Name, s.Group, Lines = lines };
        }).ToList();

        ExactMatch? best = null;
        foreach (var other in others)
        {
            var set = other.Lines
                .Select(Normalize)
                .Where(l => l.Length >= 12 && !l.StartsWith("using"))
                .ToHashSet();
            if (set.Count < 10) continue;

            // containment: how much of the OTHER student's work is inside the candidate
            var shared = set.Count(l => candidate.Contains(l));
            var containment = (double)shared / set.Count;
            if (containment >= 0.98 && (best is null || containment > best.Containment))
                best = new ExactMatch(other.Name, other.Group, containment);
        }
        return best;
    }
}
