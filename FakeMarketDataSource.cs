namespace TradingSystem;

/// <summary>
/// Simulates market data for testing and development.
/// Uses deterministic random for reproducible test runs.
/// </summary>
public sealed class FakeMarketDataSource : IMarketDataSource
{
    private readonly string[] _symbols;
    private readonly int _intervalMs;
    private readonly Random _random;

    // Fixed seed ensures reproducible sequences across runs.
    private const int DefaultSeed = 42;

    public FakeMarketDataSource(string[] symbols, int intervalMs = 1000, int? seed = null)
    {
        _symbols = symbols;
        _intervalMs = intervalMs;
        _random = new Random(seed ?? DefaultSeed);
    }

    public async Task Start(EventBus bus, CancellationToken ct)
    {
        // Base prices per symbol for realistic quote generation.
        var basePrices = new Dictionary<string, decimal>();
        foreach (var symbol in _symbols)
        {
            basePrices[symbol] = 100m + _random.Next(0, 400);
        }

        while (!ct.IsCancellationRequested)
        {
            foreach (var symbol in _symbols)
            {
                if (ct.IsCancellationRequested) break;

                var quote = GenerateQuote(symbol, basePrices[symbol]);
                var evt = Event.Create("QUOTE", symbol, quote);
                bus.Emit(evt);

                // Small drift to simulate price movement.
                basePrices[symbol] += (decimal)(_random.NextDouble() - 0.5) * 0.5m;
            }

            try
            {
                await Task.Delay(_intervalMs, ct);
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown, exit gracefully.
                break;
            }
        }
    }

    private object GenerateQuote(string symbol, decimal basePrice)
    {
        // Spread is typically small relative to price.
        var spread = 0.01m + (decimal)(_random.NextDouble() * 0.05);
        var bid = Math.Round(basePrice - spread / 2, 2);
        var ask = Math.Round(basePrice + spread / 2, 2);

        // Sizes vary to simulate market depth.
        var bidSize = _random.Next(1, 100);
        var askSize = _random.Next(1, 100);

        return new Dictionary<string, object>
        {
            ["bid"] = bid,
            ["ask"] = ask,
            ["bidSize"] = bidSize,
            ["askSize"] = askSize,
            // Symbol and timestamp included for completeness in data payload.
            // (Event already has these at top level, but consumers may want them in data too)
            ["symbol"] = symbol,
            ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
    }
}
