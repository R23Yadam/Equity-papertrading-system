using TradingSystem;

var bus = new EventBus();
const string logPath = "events.log";

var isReplay = args.Length > 0 &&
               string.Equals(args[0], "replay", StringComparison.OrdinalIgnoreCase);

if (isReplay)
{
    // Avoid writing to the source log while replaying.
    // Reuse the same bus so handlers see the identical event stream.
    var replay = new ReplayRunner(bus);
    replay.Replay(logPath);
    return;
}

using var logger = new JsonlLogger(logPath);
// Persist live events so the log can be replayed deterministically.
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
