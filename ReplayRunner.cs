using System.Text.Json;

namespace TradingSystem;

/// <summary>
/// Replays events from a JSONL log file through the EventBus.
/// Enables backtesting and debugging with recorded market data.
/// </summary>
public sealed class ReplayRunner
{
    private readonly EventBus _bus;
    private readonly string _logPath;

    public ReplayRunner(EventBus bus, string logPath = "events.log")
    {
        _bus = bus;
        _logPath = logPath;
    }

    /// <summary>
    /// Replays all events from the log file.
    /// Events are emitted in file order (should already be time-sorted).
    /// </summary>
    public void Run()
    {
        if (!File.Exists(_logPath))
        {
            Console.WriteLine($"[ReplayRunner] Log file not found: {_logPath}");
            return;
        }

        var count = 0;
        foreach (var line in File.ReadLines(_logPath))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            try
            {
                var evt = JsonSerializer.Deserialize<Event>(line);
                if (evt is not null)
                {
                    _bus.Emit(evt);
                    count++;
                }
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"[ReplayRunner] Failed to parse line: {ex.Message}");
            }
        }

        Console.WriteLine($"[ReplayRunner] Replayed {count} events from {_logPath}");
    }

    /// <summary>
    /// Replays events, optionally filtering by type.
    /// Useful for replaying only market data without signals/orders.
    /// </summary>
    public void Run(HashSet<string> eventTypesToReplay)
    {
        if (!File.Exists(_logPath))
        {
            Console.WriteLine($"[ReplayRunner] Log file not found: {_logPath}");
            return;
        }

        var count = 0;
        foreach (var line in File.ReadLines(_logPath))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            try
            {
                var evt = JsonSerializer.Deserialize<Event>(line);
                if (evt is not null && eventTypesToReplay.Contains(evt.Type))
                {
                    _bus.Emit(evt);
                    count++;
                }
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"[ReplayRunner] Failed to parse line: {ex.Message}");
            }
        }

        Console.WriteLine($"[ReplayRunner] Replayed {count} events from {_logPath}");
    }
}
