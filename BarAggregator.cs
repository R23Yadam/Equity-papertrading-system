namespace TradingSystem;

/// <summary>
/// Aggregates QUOTE events into time-based BAR events.
/// Bars are useful for strategies that operate on OHLC data rather than ticks.
/// </summary>
public sealed class BarAggregator
{
    private readonly EventBus _bus;
    private readonly int _barDurationMs;

    // Track bar state per symbol independently.
    private readonly Dictionary<string, BarState> _bars = new();

    public BarAggregator(EventBus bus, int barDurationMs = 1000)
    {
        _bus = bus;
        _barDurationMs = barDurationMs;

        // Subscribe to QUOTE events to build bars.
        bus.Subscribe(HandleEvent);
    }

    private void HandleEvent(Event evt)
    {
        if (evt.Type != EventTypes.QUOTE) return;

        var bid = EventDataReader.GetDouble(evt.Data, "bid");
        var ask = EventDataReader.GetDouble(evt.Data, "ask");

        // Mid price is a common representation for bar building.
        var mid = (bid + ask) / 2.0;

        if (!_bars.TryGetValue(evt.Symbol, out var state))
        {
            // First quote for this symbol starts a new bar.
            state = new BarState
            {
                StartTs = evt.Ts,
                Open = mid,
                High = mid,
                Low = mid,
                Close = mid,
                Count = 1
            };
            _bars[evt.Symbol] = state;
            return;
        }

        // Check if this quote belongs to a new bar period.
        // Using event timestamp (not wall clock) for deterministic replay.
        var barEndTs = state.StartTs + _barDurationMs;

        if (evt.Ts >= barEndTs)
        {
            // Emit completed bar before starting new one.
            EmitBar(evt.Symbol, state, barEndTs);

            // Start new bar with current quote.
            _bars[evt.Symbol] = new BarState
            {
                StartTs = barEndTs, // Align to bar boundaries
                Open = mid,
                High = mid,
                Low = mid,
                Close = mid,
                Count = 1
            };
        }
        else
        {
            // Update current bar with new quote.
            state.High = Math.Max(state.High, mid);
            state.Low = Math.Min(state.Low, mid);
            state.Close = mid;
            state.Count++;
        }
    }

    private void EmitBar(string symbol, BarState state, long endTs)
    {
        var barData = new Dictionary<string, object>
        {
            ["open"] = Math.Round(state.Open, 4),
            ["high"] = Math.Round(state.High, 4),
            ["low"] = Math.Round(state.Low, 4),
            ["close"] = Math.Round(state.Close, 4),
            ["startTs"] = state.StartTs,
            ["endTs"] = endTs,
            ["count"] = state.Count
        };

        var evt = Event.Create(EventTypes.BAR, symbol, barData, ts: endTs);
        _bus.Emit(evt);
    }

    /// <summary>
    /// Forces emission of any partial bars (call at shutdown).
    /// </summary>
    public void Flush(long currentTs)
    {
        foreach (var (symbol, state) in _bars)
        {
            if (state.Count > 0)
            {
                EmitBar(symbol, state, currentTs);
            }
        }
        _bars.Clear();
    }

    // Internal state for building a single bar.
    private class BarState
    {
        public long StartTs;
        public double Open;
        public double High;
        public double Low;
        public double Close;
        public int Count;
    }
}
