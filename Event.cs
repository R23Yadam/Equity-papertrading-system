using System.Text.Json;
using System.Text.Json.Serialization;

namespace TradingSystem;

public sealed class Event
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; }

    [JsonPropertyName("ts")]
    public long Ts { get; init; } // Unix ms for ordering/replay.

    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("symbol")]
    public string Symbol { get; init; } = string.Empty;

    [JsonPropertyName("data")]
    public JsonElement Data { get; init; }

    // Centralized factory to ensure required fields are set.
    public static Event Create(string type, string symbol, object data, long? ts = null, Guid? id = null)
    {
        return new Event
        {
            Id = id ?? Guid.NewGuid(),
            Ts = ts ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Type = type,
            Symbol = symbol,
            Data = JsonSerializer.SerializeToElement(data)
        };
    }
}
