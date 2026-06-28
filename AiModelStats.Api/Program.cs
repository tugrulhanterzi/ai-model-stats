using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using System.Text.Json;
using StackExchange.Redis;
using Octokit;
using AiModelStats.Api.Services;
using AiModelStats.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// GitHub client — single PAT, public-repo read scope only.
var githubToken = builder.Configuration["GITHUB_TOKEN"]
    ?? throw new InvalidOperationException("GITHUB_TOKEN environment variable is required.");

var github = new GitHubClient(new ProductHeaderValue("ai-model-stats"))
{
    Credentials = new Credentials(githubToken)
};
builder.Services.AddSingleton(github);

// Redis — sole persistence layer.
var redisConnection = builder.Configuration["REDIS_CONNECTION"] ?? "localhost:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect(redisConnection));

// Camelcase JSON for HTTP responses — keeps the svg-renderer's field access consistent.
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);

// Application services.
builder.Services.AddSingleton<ModelDetectionService>();
builder.Services.AddSingleton<CacheService>();
builder.Services.AddSingleton<GitHubScannerService>();

// Per-IP rate limiter — partitioned so each client gets its own 30 req/min bucket.
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("ip", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
            }));
    options.RejectionStatusCode = 429;
});

var app = builder.Build();

app.UseRateLimiter();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
AggregateEndpoint.Map(app);

app.Run();
