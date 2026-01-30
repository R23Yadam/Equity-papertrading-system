using System.Text.Json;
using TradingSystem;

// CancellationTokenSource enables graceful shutdown via Ctrl+C.
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true; // Prevent immediate termination.
    cts.Cancel();
    Console.WriteLine("Shutting down...");
};

var bus = new EventBus();
using var logger = new JsonlLogger("events.log");

// Wire the logger so every emit is persisted.
bus.Subscribe(logger.Handle);

// Market data source abstraction allows swapping fake/live/replay sources.
var symbols = new[] { "AAPL", "MSFT", "GOOGL" };
IMarketDataSource dataSource = new FakeMarketDataSource(symbols, intervalMs: 500);

Console.WriteLine("Starting market data feed (Ctrl+C to stop)...");

// Run the data source until cancelled.
await dataSource.Start(bus, cts.Token);

Console.WriteLine("Market data feed stopped.");

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
