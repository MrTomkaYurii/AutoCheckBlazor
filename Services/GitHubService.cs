using System.Net.Http.Headers;
using System.Text.Json;
using AutoCheck.Models;

namespace AutoCheck.Services;

public record GitHubBranch(string Name);
public record GitHubCommit(string Sha, string Message, string Date);
public record GitHubCommitNode(string Sha, string Message, string Date, List<string> Branches);

public class GitHubService
{
    private readonly HttpClient _http;

    public GitHubService(IHttpClientFactory factory)
    {
        _http = factory.CreateClient("github");
    }

    // ── Parse "https://github.com/user/repo" → ("user", "repo") ──────────────
    public static (string Owner, string Repo)? ParseUrl(string url)
    {
        url = url.Trim().TrimEnd('/');
        url = url.Replace("https://", "").Replace("http://", "").Replace("github.com/", "");
        var parts = url.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return null;
        return (parts[0], parts[1].Replace(".git", ""));
    }

    // ── Список гілок (всі, з пагінацією) ─────────────────────────────────────
    public async Task<List<GitHubBranch>> GetBranchesAsync(string repoUrl, string? token = null)
    {
        var parsed = ParseUrl(repoUrl);
        if (parsed is null) return [];

        var (owner, repo) = parsed.Value;
        var all = new List<GitHubBranch>();
        var page = 1;

        while (true)
        {
            var req = new HttpRequestMessage(HttpMethod.Get,
                $"https://api.github.com/repos/{owner}/{repo}/branches?per_page=100&page={page}");
            AddHeaders(req, token);

            var resp = await _http.SendAsync(req);
            if (!resp.IsSuccessStatusCode) break;

            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var batch = doc.RootElement.EnumerateArray()
                .Select(b => new GitHubBranch(b.GetProperty("name").GetString() ?? ""))
                .ToList();

            all.AddRange(batch);
            if (batch.Count < 100) break;   // остання сторінка
            page++;
        }

        // Сортуємо: main/master завжди перші, потім алфавіт
        return all
            .OrderBy(b => b.Name == "main" || b.Name == "master" ? 0 : 1)
            .ThenBy(b => b.Name)
            .ToList();
    }

    // ── Коміти гілки ─────────────────────────────────────────────────────────
    // Спочатку намагається compare (тільки нові коміти vs default branch).
    // Якщо compare порожній (гілка вже злита) — показує останні 50 комітів гілки.
    public async Task<List<GitHubCommit>> GetBranchCommitsAsync(
        string repoUrl, string branch, string? token = null)
    {
        var parsed = ParseUrl(repoUrl);
        if (parsed is null) return [];

        var (owner, repo) = parsed.Value;
        var defaultBranch = await GetDefaultBranchAsync(owner, repo, token);

        // Якщо вибрали default branch — одразу показуємо всі коміти
        if (branch == defaultBranch)
            return await GetCommitsOnBranchAsync(owner, repo, branch, token);

        // Compare: коміти що є в branch але не в default
        var req = new HttpRequestMessage(HttpMethod.Get,
            $"https://api.github.com/repos/{owner}/{repo}/compare/{Uri.EscapeDataString(defaultBranch)}...{Uri.EscapeDataString(branch)}");
        AddHeaders(req, token);

        var resp = await _http.SendAsync(req);
        if (resp.IsSuccessStatusCode)
        {
            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("commits", out var commits))
            {
                var list = commits.EnumerateArray()
                    .Reverse()
                    .Select(MapCommit)
                    .ToList();

                // Якщо compare дав результати — повертаємо їх
                if (list.Count > 0) return list;
            }
        }

        // Fallback: гілка злита або compare порожній —
        // показуємо останні 50 комітів гілки (включно з тими що в main)
        return await GetCommitsOnBranchAsync(owner, repo, branch, token);
    }

    // ── Всі коміти з усіх гілок (для дерева) ────────────────────────────────
    public async Task<List<GitHubCommitNode>> GetCommitTreeAsync(
        string repoUrl, string? token = null, int perBranch = 30)
    {
        var parsed = ParseUrl(repoUrl);
        if (parsed is null) return [];

        var (owner, repo) = parsed.Value;
        var branches = await GetBranchesAsync(repoUrl, token);

        // Для кожної гілки — беремо останні коміти
        var byBranch = new Dictionary<string, List<GitHubCommit>>();
        foreach (var b in branches)
        {
            var commits = await GetCommitsOnBranchAsync(owner, repo, b.Name, token, perBranch);
            byBranch[b.Name] = commits;
        }

        // Збираємо всі SHA → які гілки містять цей коміт
        var shaToCommit = new Dictionary<string, GitHubCommit>();
        var shaToBranches = new Dictionary<string, List<string>>();

        foreach (var (branch, commits) in byBranch)
        {
            foreach (var c in commits)
            {
                shaToCommit[c.Sha] = c;
                if (!shaToBranches.ContainsKey(c.Sha))
                    shaToBranches[c.Sha] = [];
                if (!shaToBranches[c.Sha].Contains(branch))
                    shaToBranches[c.Sha].Add(branch);
            }
        }

        return shaToCommit.Values
            .Select(c => new GitHubCommitNode(
                c.Sha, c.Message, c.Date,
                shaToBranches.GetValueOrDefault(c.Sha, [])))
            .OrderByDescending(c => c.Date)
            .Take(100)
            .ToList();
    }

    // ── Коміти гілки як CommitInfo (для Lab.razor) ───────────────────────────
    // Знаходить гілку від якої `branch` відгалузилась (мінімальна кількість
    // унікальних комітів в `branch` відносно будь-якої іншої гілки) і повертає
    // лише коміти з моменту відгалуження.
    public async Task<List<CommitInfo>> GetBranchCommitInfosAsync(
        string repoUrl, string branch, string? token = null, int count = 50)
    {
        var parsed = ParseUrl(repoUrl);
        if (parsed is null) return [];

        var (owner, repo) = parsed.Value;
        var defaultBranch = await GetDefaultBranchAsync(owner, repo, token);

        if (branch == defaultBranch)
            return await GetCommitInfosOnBranchAsync(owner, repo, branch, token, count);

        // Порівнюємо `branch` з усіма іншими гілками.
        // Батьківська гілка — та, де branch є "ahead" з мінімальною кількістю комітів
        // (тобто відгалузилась найпізніше від неї).
        var allBranches = await GetBranchNamesAsync(owner, repo, token);
        var bestBase = defaultBranch;
        var fewestAhead = int.MaxValue;

        foreach (var other in allBranches.Where(b => b != branch))
        {
            var r = new HttpRequestMessage(HttpMethod.Get,
                $"https://api.github.com/repos/{owner}/{repo}/compare/{Uri.EscapeDataString(other)}...{Uri.EscapeDataString(branch)}");
            AddHeaders(r, token);
            var rs = await _http.SendAsync(r);
            if (!rs.IsSuccessStatusCode) continue;

            using var d = JsonDocument.Parse(await rs.Content.ReadAsStringAsync());
            var root = d.RootElement;
            var status = root.TryGetProperty("status", out var sv) ? sv.GetString() : null;
            if (status is not ("ahead" or "diverged")) continue;

            var ahead = root.TryGetProperty("ahead_by", out var av) ? av.GetInt32() : int.MaxValue;
            if (ahead < fewestAhead) { fewestAhead = ahead; bestBase = other; }
        }

        // Беремо лише коміти унікальні для `branch` відносно батьківської гілки
        var req = new HttpRequestMessage(HttpMethod.Get,
            $"https://api.github.com/repos/{owner}/{repo}/compare/{Uri.EscapeDataString(bestBase)}...{Uri.EscapeDataString(branch)}");
        AddHeaders(req, token);
        var resp = await _http.SendAsync(req);

        if (resp.IsSuccessStatusCode)
        {
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            if (doc.RootElement.TryGetProperty("commits", out var commits))
            {
                var list = commits.EnumerateArray().Reverse().Select(MapCommitInfo).ToList();
                if (list.Count > 0) return list;
            }
        }

        return await GetCommitInfosOnBranchAsync(owner, repo, branch, token, count);
    }

    private async Task<List<string>> GetBranchNamesAsync(string owner, string repo, string? token)
    {
        var all = new List<string>();
        var page = 1;
        while (true)
        {
            var req = new HttpRequestMessage(HttpMethod.Get,
                $"https://api.github.com/repos/{owner}/{repo}/branches?per_page=100&page={page}");
            AddHeaders(req, token);
            var resp = await _http.SendAsync(req);
            if (!resp.IsSuccessStatusCode) break;
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var batch = doc.RootElement.EnumerateArray()
                .Select(b => b.GetProperty("name").GetString() ?? "")
                .Where(s => s.Length > 0).ToList();
            all.AddRange(batch);
            if (batch.Count < 100) break;
            page++;
        }
        return all;
    }

    private async Task<List<CommitInfo>> GetCommitInfosOnBranchAsync(
        string owner, string repo, string branch, string? token, int count = 50)
    {
        var req = new HttpRequestMessage(HttpMethod.Get,
            $"https://api.github.com/repos/{owner}/{repo}/commits?sha={Uri.EscapeDataString(branch)}&per_page={count}");
        AddHeaders(req, token);
        var resp = await _http.SendAsync(req);
        if (!resp.IsSuccessStatusCode) return [];
        var json = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.EnumerateArray().Select(MapCommitInfo).ToList();
    }

    private static CommitInfo MapCommitInfo(JsonElement c)
    {
        var sha    = c.GetProperty("sha").GetString() ?? "";
        var commit = c.GetProperty("commit");
        var msg    = commit.GetProperty("message").GetString() ?? "";
        var date   = commit.GetProperty("author").GetProperty("date").GetString() ?? "";
        var author = commit.GetProperty("author").GetProperty("name").GetString() ?? "";
        var parents = c.TryGetProperty("parents", out var p)
            ? p.EnumerateArray()
               .Select(x => x.GetProperty("sha").GetString() ?? "")
               .Where(s => s.Length > 0)
               .ToArray()
            : Array.Empty<string>();
        return new CommitInfo(sha, sha[..Math.Min(7, sha.Length)],
            msg.Split('\n')[0], author,
            DateTime.TryParse(date, out var dt) ? dt : DateTime.UtcNow,
            parents);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private async Task<string> GetDefaultBranchAsync(string owner, string repo, string? token)
    {
        var req = new HttpRequestMessage(HttpMethod.Get,
            $"https://api.github.com/repos/{owner}/{repo}");
        AddHeaders(req, token);
        var resp = await _http.SendAsync(req);
        if (!resp.IsSuccessStatusCode) return "main";
        var json = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty("default_branch", out var db)
            ? db.GetString() ?? "main" : "main";
    }

    private async Task<List<GitHubCommit>> GetCommitsOnBranchAsync(
        string owner, string repo, string branch, string? token, int count = 50)
    {
        var req = new HttpRequestMessage(HttpMethod.Get,
            $"https://api.github.com/repos/{owner}/{repo}/commits?sha={Uri.EscapeDataString(branch)}&per_page={count}");
        AddHeaders(req, token);
        var resp = await _http.SendAsync(req);
        if (!resp.IsSuccessStatusCode) return [];
        var json = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.EnumerateArray().Select(MapCommit).ToList();
    }

    private static GitHubCommit MapCommit(JsonElement c)
    {
        var sha    = c.GetProperty("sha").GetString() ?? "";
        var commit = c.GetProperty("commit");
        var msg    = commit.GetProperty("message").GetString() ?? "";
        var date   = commit.GetProperty("author").GetProperty("date").GetString() ?? "";
        return new GitHubCommit(sha[..Math.Min(7, sha.Length)], msg.Split('\n')[0], date);
    }

    private static void AddHeaders(HttpRequestMessage req, string? token)
    {
        req.Headers.UserAgent.Add(new ProductInfoHeaderValue("AutoCheck", "1.0"));
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        if (!string.IsNullOrEmpty(token))
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
}
