namespace AiModelStats.Api.Models;

public record ModelUsage(string Model, int Count, string Color);

public record AggregationResult(string Username, IReadOnlyList<ModelShare> Models, DateTime ScannedAt);

public record ModelShare(string Model, double Percentage, string Color);
