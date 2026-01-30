namespace TradingSystem;

/// <summary>
/// Abstraction for market data sources.
/// Enables swapping between fake/live/replay data sources without changing consumers.
/// </summary>
public interface IMarketDataSource
{
    /// <summary>
    /// Starts emitting market data events into the bus.
    /// Runs until cancellation is requested.
    /// </summary>
    Task Start(EventBus bus, CancellationToken ct);
}
