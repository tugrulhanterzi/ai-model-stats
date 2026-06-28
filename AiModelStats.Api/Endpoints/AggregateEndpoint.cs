using System.Text.RegularExpressions;
using Octokit;
using AiModelStats.Api.Models;
using AiModelStats.Api.Services;

namespace AiModelStats.Api.Endpoints;

public static partial class AggregateEndpoint
{
    // GitHub rules: 1-39 chars, alphanumeric + single hyphens, no leading/trailing/consecutive hyphens.
    [GeneratedRegex(@"^(?!.*--)[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,37}[a-zA-Z0-9])?$")]
    private static partial Regex GitHubUsernameRegex();

    private static readonly Dictionary<string, string> ModelColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["GPT-4"]            = "#74AA9C",
        ["GPT-3.5"]          = "#A8D4C8",
        ["o1 / o3"]          = "#10A37F",
        ["Claude"]           = "#D97757",
        ["Gemini"]           = "#4285F4",
        ["Llama"]            = "#0467DF",
        ["Mistral"]          = "#FF7000",
        ["Whisper"]          = "#412991",
        ["DALL-E"]           = "#BE4B48",
        ["Stable Diffusion"] = "#CF4E2A",
    };
    private const string DefaultColor = "#858585";

    public static void Map(WebApplication app)
    {
        app.MapGet("/api/aggregate/{username}", async (
            string username,
            CacheService cache,
            GitHubScannerService scanner) =>
        {
            if (!GitHubUsernameRegex().IsMatch(username))
                return Results.BadRequest(new { error = $"'{username}' is not a valid GitHub username." });

            var cached = await cache.GetAsync(username);
            if (cached is not null)
                return Results.Ok(cached);

            Dictionary<string, int> rawCounts;
            try
            {
                rawCounts = await scanner.ScanAsync(username);
            }
            catch (NotFoundException)
            {
                return Results.NotFound(new { error = $"GitHub user '{username}' not found." });
            }
            catch (RateLimitExceededException ex)
            {
                return Results.Json(
                    new { error = "GitHub API rate limit exceeded. Try again later.", resetAt = ex.Reset },
                    statusCode: 503);
            }

            if (rawCounts.Count == 0)
            {
                return Results.NotFound(new
                {
                    error = $"No AI model usage detected in '{username}'s public repositories."
                });
            }

            var total = rawCounts.Values.Sum();
            var models = rawCounts
                .Select(kvp => new ModelShare(
                    kvp.Key,
                    Math.Round((double)kvp.Value / total * 100, 1),
                    ModelColors.GetValueOrDefault(kvp.Key, DefaultColor)))
                .OrderByDescending(m => m.Percentage)
                .ToList();

            var result = new AggregationResult(username, models, DateTime.UtcNow);
            await cache.SetAsync(username, result);
            return Results.Ok(result);
        })
        .RequireRateLimiting("ip");
    }
}
