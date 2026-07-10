using AutoCheck.Data;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.EntityFrameworkCore;

namespace AutoCheck.Services;

// ── DTOs (teacher-only; never surfaced to students) ─────────────────────────────

public record SimilarStudent(int StudentId, string Name, string Group, int Percent, int SharedFragments);

public record CodeLine(string Text, bool Matched);

public record CodeOverlap(
    int StudentId, string StudentName, string Group, int Percent,
    List<CodeLine> Target,   // the opened student's code, matching lines flagged
    List<CodeLine> Other);   // the compared student's code, matching lines flagged

/// <summary>
/// Structural code-similarity detector (teacher-only). Unlike the exact-line plagiarism
/// gate, this is robust to renamed variables/methods and reformatting: it tokenises C#
/// with Roslyn, replaces every identifier/literal with a placeholder (so only the code
/// STRUCTURE remains), then compares MOSS-style k-gram winnowing fingerprints.
/// Same structure with different names ⇒ high similarity.
/// </summary>
public class CodeSimilarityService(IDbContextFactory<AppDbContext> dbf)
{
    private const int K = 5;             // k-gram size in tokens
    private const int Window = 4;        // winnowing window
    private const int MinLineLen = 10;   // ignore trivial lines when highlighting

    // ── Public API ──────────────────────────────────────────────────────────────

    /// <summary>Top-N structurally most-similar students for one student's lab submission.</summary>
    public async Task<List<SimilarStudent>> FindSimilarAsync(int labNumber, int studentId, int topN = 7)
    {
        var subs = await LoadLabCodeAsync(labNumber);
        if (!subs.TryGetValue(studentId, out var target)) return [];

        var targetSig = Analyze(target.Code);
        if (targetSig.Fingerprints.Count == 0) return [];

        var results = new List<SimilarStudent>();
        foreach (var (sid, info) in subs)
        {
            if (sid == studentId) continue;
            var sig = Analyze(info.Code);
            if (sig.Fingerprints.Count == 0) continue;

            var shared = targetSig.Fingerprints.Count(f => sig.Fingerprints.Contains(f));
            if (shared == 0) continue;

            var pct = Dice(targetSig.Fingerprints.Count, sig.Fingerprints.Count, shared);
            results.Add(new SimilarStudent(sid, info.Name, info.Group, pct, shared));
        }

        return results
            .OrderByDescending(r => r.Percent).ThenByDescending(r => r.SharedFragments)
            .Take(topN).ToList();
    }

    /// <summary>Line-level overlap between the target student and one other, for highlighting.</summary>
    public async Task<CodeOverlap?> GetOverlapAsync(int labNumber, int studentId, int otherStudentId)
    {
        var subs = await LoadLabCodeAsync(labNumber);
        if (!subs.TryGetValue(studentId, out var target)) return null;
        if (!subs.TryGetValue(otherStudentId, out var other)) return null;

        var a = Analyze(target.Code);
        var b = Analyze(other.Code);
        var pct = Dice(a.Fingerprints.Count, b.Fingerprints.Count,
                       a.Fingerprints.Count(f => b.Fingerprints.Contains(f)));

        // A line "matches" when its structural (identifier-normalised) signature also
        // appears in the other student's code and is non-trivial.
        var aSet = a.LineNorms.Where(n => n.Length >= MinLineLen).ToHashSet();
        var bSet = b.LineNorms.Where(n => n.Length >= MinLineLen).ToHashSet();

        var targetLines = a.Lines.Zip(a.LineNorms,
            (t, n) => new CodeLine(t, n.Length >= MinLineLen && bSet.Contains(n))).ToList();
        var otherLines = b.Lines.Zip(b.LineNorms,
            (t, n) => new CodeLine(t, n.Length >= MinLineLen && aSet.Contains(n))).ToList();

        return new CodeOverlap(otherStudentId, other.Name, other.Group, pct, targetLines, otherLines);
    }

    // ── Data loading ──────────────────────────────────────────────────────────────

    private record SubInfo(string Name, string Group, string Code);

    private async Task<Dictionary<int, SubInfo>> LoadLabCodeAsync(int labNumber)
    {
        await using var db = await dbf.CreateDbContextAsync();
        var raw = await db.Submissions.AsNoTracking()
            .Where(s => s.LabDef.Number == labNumber && s.TaskResults.Any())
            .Select(s => new
            {
                s.StudentId,
                Name = s.Student.LastName + " " + s.Student.FirstName,
                s.Student.Group,
                Results = s.TaskResults.Select(tr => new
                {
                    tr.AttemptNo,
                    Lines = tr.DiffLines
                        .Where(d => d.Type == "add" || d.Type == "ctx")
                        .OrderBy(d => d.OrderIndex)
                        .Select(d => d.Text).ToList(),
                }).ToList(),
            })
            .ToListAsync();

        var dict = new Dictionary<int, SubInfo>();
        foreach (var s in raw)
        {
            // Reconstruct the code from the LATEST attempt's stored diff/context lines.
            var latest = s.Results.Count > 0 ? s.Results.Max(r => r.AttemptNo) : 0;
            var code = string.Join('\n',
                s.Results.Where(r => r.AttemptNo == latest).SelectMany(r => r.Lines));
            if (!string.IsNullOrWhiteSpace(code))
                dict[s.StudentId] = new SubInfo(s.Name, s.Group, code);
        }
        return dict;
    }

    // ── Roslyn analysis ───────────────────────────────────────────────────────────

    private record Signature(HashSet<long> Fingerprints, List<string> Lines, List<string> LineNorms);

    private static Signature Analyze(string code)
    {
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var text = tree.GetText();

        var tokens = new List<string>();
        var byLine = new Dictionary<int, List<string>>();

        foreach (var tok in root.DescendantTokens())
        {
            if (tok.IsKind(SyntaxKind.EndOfFileToken)) continue;
            var norm = NormalizeToken(tok);
            if (norm.Length == 0) continue;

            tokens.Add(norm);
            var line = text.Lines.GetLineFromPosition(tok.SpanStart).LineNumber;
            if (!byLine.TryGetValue(line, out var lt)) byLine[line] = lt = [];
            lt.Add(norm);
        }

        // Per-source-line normalized signature, aligned to the original text lines.
        var srcLines = code.Replace("\r", "").Split('\n');
        var lineNorms = new List<string>(srcLines.Length);
        for (int i = 0; i < srcLines.Length; i++)
            lineNorms.Add(byLine.TryGetValue(i, out var lt) ? string.Concat(lt) : "");

        return new Signature(Winnow(tokens), [.. srcLines], lineNorms);
    }

    /// <summary>Identifiers and literals become placeholders; keywords/operators/punctuation stay.</summary>
    private static string NormalizeToken(SyntaxToken tok)
    {
        if (tok.IsKind(SyntaxKind.IdentifierToken)) return "ID";
        if (tok.IsKind(SyntaxKind.NumericLiteralToken)) return "NUM";
        if (tok.IsKind(SyntaxKind.StringLiteralToken)
            || tok.IsKind(SyntaxKind.CharacterLiteralToken)
            || tok.IsKind(SyntaxKind.InterpolatedStringTextToken)) return "STR";

        var t = tok.Text;
        return string.IsNullOrWhiteSpace(t) ? "" : t;
    }

    // ── Winnowing fingerprints (MOSS algorithm) ───────────────────────────────────

    private static HashSet<long> Winnow(List<string> tokens)
    {
        var fps = new HashSet<long>();
        if (tokens.Count == 0) return fps;
        if (tokens.Count < K) { fps.Add(Hash(tokens, 0, tokens.Count)); return fps; }

        var hashes = new long[tokens.Count - K + 1];
        for (int i = 0; i < hashes.Length; i++) hashes[i] = Hash(tokens, i, K);

        var w = Math.Max(1, Window);
        if (hashes.Length < w) { foreach (var h in hashes) fps.Add(h); return fps; }

        for (int i = 0; i + w <= hashes.Length; i++)
        {
            var min = long.MaxValue;
            for (int j = i; j < i + w; j++) if (hashes[j] < min) min = hashes[j];
            fps.Add(min);
        }
        return fps;
    }

    private static long Hash(List<string> tokens, int start, int len)
    {
        unchecked
        {
            long h = 1469598103934665603L;   // FNV-1a
            for (int i = start; i < start + len; i++)
            {
                foreach (var c in tokens[i]) { h ^= c; h *= 1099511628211L; }
                h ^= '|'; h *= 1099511628211L;
            }
            return h;
        }
    }

    private static int Dice(int a, int b, int shared) =>
        a + b == 0 ? 0 : Math.Min(100, (int)Math.Round(200.0 * shared / (a + b)));
}
