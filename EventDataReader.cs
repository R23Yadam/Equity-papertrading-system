using System.Text.Json;

namespace TradingSystem;

/// <summary>
/// Safe helpers to read typed fields from Event.Data.
/// Handles JSON's tendency to deserialize numbers as various types.
/// </summary>
public static class EventDataReader
{
    /// <summary>
    /// Reads a double from Event.Data, handling int/long/decimal/double storage.
    /// </summary>
    public static double GetDouble(JsonElement data, string field)
    {
        if (!data.TryGetProperty(field, out var prop))
        {
            throw new KeyNotFoundException($"Field '{field}' not found in event data");
        }

        return prop.ValueKind switch
        {
            JsonValueKind.Number => prop.GetDouble(),
            JsonValueKind.String => double.Parse(prop.GetString()!),
            _ => throw new InvalidOperationException(
                $"Field '{field}' has unexpected type {prop.ValueKind}, expected number")
        };
    }

    /// <summary>
    /// Reads an int from Event.Data, handling various numeric storage types.
    /// </summary>
    public static int GetInt(JsonElement data, string field)
    {
        if (!data.TryGetProperty(field, out var prop))
        {
            throw new KeyNotFoundException($"Field '{field}' not found in event data");
        }

        return prop.ValueKind switch
        {
            JsonValueKind.Number => prop.GetInt32(),
            JsonValueKind.String => int.Parse(prop.GetString()!),
            _ => throw new InvalidOperationException(
                $"Field '{field}' has unexpected type {prop.ValueKind}, expected number")
        };
    }

    /// <summary>
    /// Reads a long from Event.Data (useful for timestamps, order IDs).
    /// </summary>
    public static long GetLong(JsonElement data, string field)
    {
        if (!data.TryGetProperty(field, out var prop))
        {
            throw new KeyNotFoundException($"Field '{field}' not found in event data");
        }

        return prop.ValueKind switch
        {
            JsonValueKind.Number => prop.GetInt64(),
            JsonValueKind.String => long.Parse(prop.GetString()!),
            _ => throw new InvalidOperationException(
                $"Field '{field}' has unexpected type {prop.ValueKind}, expected number")
        };
    }

    /// <summary>
    /// Reads a string from Event.Data.
    /// </summary>
    public static string GetString(JsonElement data, string field)
    {
        if (!data.TryGetProperty(field, out var prop))
        {
            throw new KeyNotFoundException($"Field '{field}' not found in event data");
        }

        return prop.ValueKind switch
        {
            JsonValueKind.String => prop.GetString()!,
            // Allow numbers to be read as strings for flexibility
            JsonValueKind.Number => prop.GetRawText(),
            _ => throw new InvalidOperationException(
                $"Field '{field}' has unexpected type {prop.ValueKind}, expected string")
        };
    }

    /// <summary>
    /// Reads a Guid from Event.Data (stored as string).
    /// </summary>
    public static Guid GetGuid(JsonElement data, string field)
    {
        var str = GetString(data, field);
        return Guid.Parse(str);
    }

    /// <summary>
    /// Tries to read a double, returns default if field is missing.
    /// </summary>
    public static double GetDoubleOrDefault(JsonElement data, string field, double defaultValue = 0.0)
    {
        if (!data.TryGetProperty(field, out _))
        {
            return defaultValue;
        }
        return GetDouble(data, field);
    }

    /// <summary>
    /// Tries to read an int, returns default if field is missing.
    /// </summary>
    public static int GetIntOrDefault(JsonElement data, string field, int defaultValue = 0)
    {
        if (!data.TryGetProperty(field, out _))
        {
            return defaultValue;
        }
        return GetInt(data, field);
    }
}
