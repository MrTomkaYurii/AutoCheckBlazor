using AutoCheck.Models;

namespace AutoCheck.Services;

public interface IGitBranchService
{
    Task<List<CommitInfo>> GetCommitsAsync(
        string repoUrl, string branch,
        int maxCount = 50, CancellationToken ct = default);
}
