namespace AutoCheck.Models;

/// <summary>Parses the Gemini feedback JSON ({done, issues, analysis}) stored in TaskResult.Feedback.</summary>
public static class FeedbackJson
{
    public static (string[] Done, string[] Issues, string Analysis) Parse(string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return ([], [], "");
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(raw);
            var r = doc.RootElement;
            static string[] Arr(System.Text.Json.JsonElement root, string key) =>
                root.TryGetProperty(key, out var el) && el.ValueKind == System.Text.Json.JsonValueKind.Array
                    ? el.EnumerateArray().Select(x => x.GetString() ?? "").Where(s => s.Length > 0).ToArray()
                    : [];
            var analysis = r.TryGetProperty("analysis", out var a) ? a.GetString() ?? "" : "";
            return (Arr(r, "done"), Arr(r, "issues"), analysis);
        }
        catch { return ([], [], raw); }   // legacy plain-text feedback
    }
}
