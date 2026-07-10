namespace AutoCheck.Services;

/// <summary>
/// Pure git-diff parsing used by the grading pipeline: normalise a repo URL, filter a
/// commit diff down to one task's file, and turn a diff (or plain file text) into typed
/// lines (hdr / add / del / ctx). Extracted from <see cref="GradingService"/> so this
/// core logic — which drives the stored DiffEntry rows and the plagiarism corpus — can
/// be unit-tested in isolation.
/// </summary>
public static class GitDiff
{
    /// <summary>Trims, forces an https:// scheme and a .git suffix.</summary>
    public static string NormalizeUrl(string raw)
    {
        raw = raw.Trim().TrimEnd('/');
        if (!raw.StartsWith("http")) raw = "https://" + raw;
        if (!raw.EndsWith(".git"))   raw += ".git";
        return raw;
    }

    /// <summary>
    /// Keeps only the hunk(s) whose file path contains "task{N}" (case-insensitive).
    /// Returns the input unchanged when it isn't a git diff, or falls back to the full
    /// diff when no task-specific file is found.
    /// </summary>
    public static string FilterToTask(string diffOutput, int taskNumber)
    {
        if (string.IsNullOrWhiteSpace(diffOutput)) return diffOutput;

        var trimmed = diffOutput.TrimStart();
        if (!trimmed.StartsWith("commit ") && !trimmed.StartsWith("diff --git"))
            return diffOutput; // plain file content, not a git diff

        var pattern = $"task{taskNumber}";
        var lines   = diffOutput.Split('\n');
        var header  = new List<string>(); // commit / Author / Date / message lines
        var taskSec = new List<string>(); // lines for this task's file
        bool inDiff = false;
        bool inTask = false;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');
            if (line.StartsWith("diff --git"))
            {
                inDiff = true;
                inTask = line.Contains(pattern, StringComparison.OrdinalIgnoreCase);
                if (inTask) taskSec.Add(line);
            }
            else if (!inDiff)
            {
                header.Add(line);
            }
            else if (inTask)
            {
                taskSec.Add(line);
            }
        }

        return taskSec.Count > 0
            ? string.Join('\n', header.Concat(taskSec))
            : diffOutput; // fallback: show full diff if no task-specific file found
    }

    /// <summary>
    /// Parses a git diff into ordered typed lines. Plain (non-diff) text is returned as
    /// "ctx" lines. Diff lines are "hdr" (metadata/hunk headers), "add" (+), "del" (-)
    /// or "ctx" (unchanged); N1/N2 track old/new line numbers within a hunk.
    /// </summary>
    public static List<(string Type, int? N1, int? N2, string Text)> Parse(string raw)
    {
        var result = new List<(string, int?, int?, string)>();
        if (string.IsNullOrWhiteSpace(raw)) return result;

        bool isDiff = raw.TrimStart().StartsWith("commit ") ||
                      raw.TrimStart().StartsWith("diff --git");

        if (!isDiff)
        {
            int n = 1;
            foreach (var line in raw.Split('\n').Take(600))
                result.Add(("ctx", n, n++, line.TrimEnd('\r')));
            return result;
        }

        int oldLine = 0, newLine = 0;
        bool inHunk = false;

        foreach (var rawLine in raw.Split('\n').Take(1200))
        {
            var line = rawLine.TrimEnd('\r');

            if (line.StartsWith("diff --git") || line.StartsWith("index ") ||
                line.StartsWith("--- ") || line.StartsWith("+++ ") ||
                line.StartsWith("Binary "))
            {
                result.Add(("hdr", null, null, line));
                inHunk = false;
                continue;
            }
            if (line.StartsWith("commit ") || line.StartsWith("Author:") ||
                line.StartsWith("Date:") || line.StartsWith("Merge:") ||
                (line.StartsWith("    ") && !inHunk))
            {
                result.Add(("hdr", null, null, line));
                continue;
            }
            if (line.StartsWith("@@"))
            {
                var parts = line.Split(' ');
                foreach (var p in parts)
                {
                    if (p.StartsWith("-") && p.Length > 1 && !p.StartsWith("---"))
                    { int.TryParse(p[1..].Split(',')[0], out oldLine); }
                    else if (p.StartsWith("+") && p.Length > 1 && !p.StartsWith("+++"))
                    { int.TryParse(p[1..].Split(',')[0], out newLine); }
                }
                inHunk = true;
                result.Add(("hdr", null, null, line));
                continue;
            }
            if (!inHunk) continue;

            if (line.StartsWith("+"))
            {
                result.Add(("add", null, newLine, line[1..]));
                newLine++;
            }
            else if (line.StartsWith("-"))
            {
                result.Add(("del", oldLine, null, line[1..]));
                oldLine++;
            }
            else if (line.StartsWith(" ") || line.Length == 0)
            {
                result.Add(("ctx", oldLine, newLine, line.Length > 0 ? line[1..] : ""));
                oldLine++;
                newLine++;
            }
        }

        return result;
    }
}
