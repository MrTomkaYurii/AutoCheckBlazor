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
public class PlagiarismService(AppDbContext db)
{
    public async Task<List<PlagPair>> CheckLabAsync(int labNumber, double threshold = 0.5)
    {
        var subs = await db.Submissions.AsNoTracking()
            .Where(s => s.LabDef.Number == labNumber && s.TaskResults.Any())
            .Select(s => new
            {
                s.StudentId,
                Name  = s.Student.LastName + " " + s.Student.FirstName,
                s.Student.Group,
                Lines = s.TaskResults
                    .SelectMany(tr => tr.DiffLines
                        .Where(d => d.Type == "add")
                        .Select(d => d.Text))
                    .ToList(),
            })
            .ToListAsync();

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

        var others = await db.Submissions.AsNoTracking()
            .Where(s => s.LabDefId == labDefId && s.StudentId != studentId && s.TaskResults.Any())
            .Select(s => new
            {
                Name  = s.Student.LastName + " " + s.Student.FirstName,
                s.Student.Group,
                Lines = s.TaskResults
                    .SelectMany(tr => tr.DiffLines
                        .Where(d => d.Type == "add")
                        .Select(d => d.Text))
                    .ToList(),
            })
            .ToListAsync();

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
