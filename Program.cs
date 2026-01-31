using TradingSystem;

// =============================================================================
// Program.cs: Glue code only - wires modules and starts the system.
// No strategy logic belongs here.
// =============================================================================

// Parse command line for mode: "live" (default) or "replay".
var mode = args.Length > 0 ? args[0].ToLower() : "live";

// CancellationTokenSource enables graceful shutdown via Ctrl+C.
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true; // Prevent immediate termination.
    cts.Cancel();
    Console.WriteLine("\nShutting down...");
};

var bus = new EventBus();

// -----------------------------------------------------------------------------
// Wire modules in dependency order:
// 1. Logger (first, so all events are persisted)
// 2. BarAggregator (optional, builds bars from quotes)
// 3. OrderManager (SIGNAL -> ORDER)
// 4. RiskEngine (ORDER -> ORDER_ACCEPTED/REJECT)
// 5. ExecutionSimulator (ORDER_ACCEPTED -> FILL)
// 6. Portfolio (tracks state from FILLs)
// -----------------------------------------------------------------------------

// 1. Logger - persists all events for replay/debugging.
using var logger = new JsonlLogger("events.log");
bus.Subscribe(logger.Handle);

// 2. BarAggregator - builds 1-second bars from QUOTE events.
var barAggregator = new BarAggregator(bus, barDurationMs: 1000);

// 3. OrderManager - converts SIGNAL events to ORDER events.
var orderManager = new OrderManager(bus, new OrderManagerConfig { DefaultQty = 10 });

// 4. RiskEngine - validates orders against risk limits.
var riskEngine = new RiskEngine(bus, new RiskConfig
{
    MaxOrderQty = 100,
    MaxPositionQty = 500
});

// 5. ExecutionSimulator - fills accepted orders against market quotes.
var executionSim = new ExecutionSimulator(bus, new ExecutionConfig
{
    FeesPerShare = 0.005,
    SlippageBps = 5.0
});

// 6. Portfolio - tracks cash, positions, and P&L.
var portfolio = new Portfolio(bus, new PortfolioConfig
{
    InitialCash = 100_000.0,
    EmitStateEveryNFills = 1
});

// -----------------------------------------------------------------------------
// Run in selected mode
// -----------------------------------------------------------------------------

if (mode == "replay")
{
    Console.WriteLine("=== REPLAY MODE ===");
    Console.WriteLine("Replaying events from events.log...\n");

    var runner = new ReplayRunner(bus, "events.log");
    runner.Run();

    Console.WriteLine("\nReplay complete.");
}
else
{
    Console.WriteLine("=== LIVE MODE ===");
    Console.WriteLine("Starting market data feed (Ctrl+C to stop)...\n");

    // Market data source.
    var symbols = new[] { "AAPL", "MSFT", "GOOGL" };
    IMarketDataSource dataSource = new FakeMarketDataSource(symbols, intervalMs: 500);

    // -------------------------------------------------------------------------
    // TEMPORARY TEST HARNESS: Emit a single SIGNAL after a delay to exercise
    // the ORDER -> FILL -> STATE pipeline. Remove when real strategy is added.
    // -------------------------------------------------------------------------
    _ = Task.Run(async () =>
    {
        // Wait for some quotes to arrive first.
        await Task.Delay(2000, cts.Token).ConfigureAwait(false);

        if (!cts.Token.IsCancellationRequested)
        {
            Console.WriteLine("\n[TEST HARNESS] Emitting test BUY signal for AAPL...");
            var testSignal = Event.Create(EventTypes.SIGNAL, "AAPL", new Dictionary<string, object>
            {
                ["side"] = "BUY",
                ["reason"] = "test_harness"
            });
            bus.Emit(testSignal);

            // Also test a sell after another delay.
            await Task.Delay(3000, cts.Token).ConfigureAwait(false);
            if (!cts.Token.IsCancellationRequested)
            {
                Console.WriteLine("[TEST HARNESS] Emitting test SELL signal for AAPL...");
                var sellSignal = Event.Create(EventTypes.SIGNAL, "AAPL", new Dictionary<string, object>
                {
                    ["side"] = "SELL",
                    ["reason"] = "test_harness"
                });
                bus.Emit(sellSignal);
            }
        }
    });
    // -------------------------------------------------------------------------
    // END TEST HARNESS
    // -------------------------------------------------------------------------

    // Run the data source until cancelled.
    await dataSource.Start(bus, cts.Token);

    // Flush any partial bars at shutdown.
    barAggregator.Flush(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    portfolio.FlushState(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

    Console.WriteLine("\nMarket data feed stopped.");
}
