using System.Text.Json;
using TradingSystem;

var bus = new EventBus();
using var logger = new JsonlLogger("events.log");

// Wire the logger so every emit is persisted.
bus.Subscribe(logger.Handle);

var quotes = new[]
{
    new Dictionary<string, object> { ["price"] = 100.25m, ["size"] = 10 },
    new Dictionary<string, object> { ["price"] = 100.30m, ["size"] = 12 },
    new Dictionary<string, object> { ["price"] = 100.10m, ["size"] = 8 },
};

foreach (var quote in quotes)
{
    var evt = Event.Create("QUOTE", "AAPL", quote);
    bus.Emit(evt);
}

// Stub: replays persisted events through the same bus.
static void Replay(EventBus bus, string path)
{
    if (!File.Exists(path))
    {
        return;
    }

    foreach (var line in File.ReadLines(path))
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            continue;
        }

        var evt = JsonSerializer.Deserialize<Event>(line);
        if (evt is not null)
        {
            bus.Emit(evt);
        }
    }
}
