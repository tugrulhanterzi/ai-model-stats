using StackExchange.Redis;
using System.Text.Json;
using AiModelStats.Api.Models;

namespace AiModelStats.Api.Services;

public class CacheService(IConnectionMultiplexer redis)
{
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(12);
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly IDatabase _db = redis.GetDatabase();

    private static string Key(string username) => $"model-stats:{username.ToLowerInvariant()}";

    public async Task<AggregationResult?> GetAsync(string username)
    {
        var value = await _db.StringGetAsync(Key(username));
        if (value.IsNullOrEmpty) return null;
        return JsonSerializer.Deserialize<AggregationResult>(value!, JsonOpts);
    }

    public async Task SetAsync(string username, AggregationResult result)
    {
        var json = JsonSerializer.Serialize(result, JsonOpts);
        await _db.StringSetAsync(Key(username), json, Ttl);
    }
}
