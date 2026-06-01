using System.Text.RegularExpressions;

namespace AutoCheck.Data;

public record TaskParseResult(int Number, string Title, string? Brief, int Difficulty);

public record LabParseResult(
    string Title,
    string? Goal,
    string? BranchName,
    bool MergesMain,
    List<TaskParseResult> Tasks);

public static class LabMdParser
{
    private static readonly Regex TaskHeadingRx = new(
        @"^##\s+(?:Задача|Завдання)\s+(\d+)\.\s+(.+?)(\s+[⭐]+)?\s*$",
        RegexOptions.Multiline | RegexOptions.Compiled);

    public static LabParseResult Parse(string md)
    {
        // H1 → lab title
        string title = "";
        var h1 = Regex.Match(md, @"^#\s+(.+)$", RegexOptions.Multiline);
        if (h1.Success)
        {
            title = h1.Groups[1].Value.Trim();
            title = Regex.Replace(title, @"^Лаба\s+\d+\s+[—–]\s+", "");
            title = Regex.Replace(title, @"^Лабораторна робота №?\s*\d+[.\s]+", "");
            title = title.Trim();
        }

        // ## Мета → goal
        string? goal = null;
        var goalM = Regex.Match(md, @"##\s+Мета\s*\n([\s\S]*?)(?=\n##|\z)");
        if (goalM.Success) goal = goalM.Groups[1].Value.Trim();

        // git branch name
        string? branch = null;
        var brM = Regex.Match(md, @"git checkout -b (\S+)");
        if (brM.Success) branch = brM.Groups[1].Value;
        bool merges = md.Contains("зливається в `main`") && !md.Contains("не зливається в `main`");

        // ## Задача / ## Завдання headings
        var tasks = new List<TaskParseResult>();
        var hits = TaskHeadingRx.Matches(md);
        for (int i = 0; i < hits.Count; i++)
        {
            var m = hits[i];
            int num = int.Parse(m.Groups[1].Value);
            string taskTitle = m.Groups[2].Value.Trim().Replace("`", "");
            int diff = m.Groups[3].Value.Count(c => c == '⭐');
            if (diff == 0) diff = 1;

            // content between this heading and the next ## heading
            int start = m.Index + m.Length;
            int end = i + 1 < hits.Count ? hits[i + 1].Index : md.Length;
            string section = md[start..end].Trim();

            // prefer ### Умова / Що зараз section; fall back to first 800 chars
            string? brief = null;
            var bM = Regex.Match(section,
                @"###\s+(?:Умова|Що зараз|Завдання|Опис)\s*\n([\s\S]*?)(?=\n###|\n##|\z)");
            brief = bM.Success
                ? bM.Groups[1].Value.Trim()
                : (section.Length > 0 ? section[..Math.Min(section.Length, 800)].Trim() : null);

            tasks.Add(new(num, taskTitle, brief, diff));
        }

        return new(title, goal, branch, merges, tasks);
    }
}
