using Octokit;
using AiModelStats.Api.Models;

namespace AiModelStats.Api.Services;

public class GitHubScannerService(
    GitHubClient github,
    ModelDetectionService detector,
    ILogger<GitHubScannerService> logger)
{
    private const int MaxRepos = 100;
    private const int MaxFilesPerRepo = 30;
    private const int RateLimitBuffer = 100;

    private static readonly HashSet<string> TargetFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "package.json", "requirements.txt", "pyproject.toml"
    };

    private static readonly HashSet<string> TargetExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".py", ".ts", ".js", ".cs", ".ipynb"
    };

    public async Task<Dictionary<string, int>> ScanAsync(string username)
    {
        IReadOnlyList<Repository> repos;
        try
        {
            repos = await github.Repository.GetAllForUser(username);
        }
        catch (NotFoundException)
        {
            throw;
        }

        var totals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var repo in repos.Take(MaxRepos))
        {
            if (repo.Fork || repo.Archived) continue;

            // Proactively stop if rate limit is running low to preserve remaining calls.
            var rateLimit = github.GetLastApiInfo()?.RateLimit;
            if (rateLimit is { Remaining: < RateLimitBuffer })
            {
                logger.LogWarning("Rate limit low ({Remaining} remaining), stopping scan early", rateLimit.Remaining);
                break;
            }

            try
            {
                var counts = await ScanRepoAsync(repo.Owner.Login, repo.Name, repo.DefaultBranch ?? "main");
                foreach (var (model, count) in counts)
                    totals[model] = totals.GetValueOrDefault(model) + count;
            }
            catch (RateLimitExceededException) { throw; }
            catch (Exception ex)
            {
                logger.LogDebug("Skipping {Owner}/{Repo}: {Message}", repo.Owner.Login, repo.Name, ex.Message);
            }
        }

        return totals;
    }

    private async Task<Dictionary<string, int>> ScanRepoAsync(string owner, string repo, string branch)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        TreeResponse tree;
        try
        {
            // Recursive tree: 1 API call to discover all paths, then fetch only matching files.
            tree = await github.Git.Tree.GetRecursive(owner, repo, branch);
        }
        catch (NotFoundException) { return counts; }

        if (tree.Truncated)
            logger.LogDebug("Tree truncated for {Owner}/{Repo} — partial scan only", owner, repo);

        var filesToScan = tree.Tree
            .Where(item => item.Type == TreeType.Blob && IsTargetFile(item.Path))
            .Take(MaxFilesPerRepo);

        foreach (var item in filesToScan)
        {
            try
            {
                var contents = await github.Repository.Content.GetAllContents(owner, repo, item.Path);
                var text = contents.FirstOrDefault()?.Content ?? string.Empty;

                foreach (var (model, count) in detector.Detect(text))
                    counts[model] = counts.GetValueOrDefault(model) + count;
            }
            catch (RateLimitExceededException) { throw; }
            catch (Exception ex)
            {
                logger.LogDebug("Skipping file {Path}: {Message}", item.Path, ex.Message);
            }
        }

        return counts;
    }

    private static bool IsTargetFile(string path)
    {
        var fileName = Path.GetFileName(path);
        var extension = Path.GetExtension(path);
        return TargetFileNames.Contains(fileName) || TargetExtensions.Contains(extension);
    }
}
