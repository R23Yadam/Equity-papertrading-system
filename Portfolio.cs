namespace TradingSystem;

/// <summary>
/// Configuration for Portfolio state emission.
/// </summary>
public sealed class PortfolioConfig
{
    // Initial cash balance.
    public double InitialCash { get; init; } = 100_000.0;

    // Emit STATE event every N fills (0 = disabled).
    public int EmitStateEveryNFills { get; init; } = 1;
}

/// <summary>
/// Tracks portfolio state: cash, positions, P&L.
/// Emits STATE events for monitoring and strategy decisions.
/// </summary>
public sealed class Portfolio
{
    private readonly EventBus _bus;
    private readonly PortfolioConfig _config;

    private double _cash;
    private double _realizedPnl;
    private int _fillCount;

    // Position tracking per symbol.
    private readonly Dictionary<string, PositionInfo> _positions = new();

    // Cache last quote per symbol for unrealized P&L.
    private readonly Dictionary<string, double> _lastMid = new();

    public Portfolio(EventBus bus, PortfolioConfig? config = null)
    {
        _bus = bus;
        _config = config ?? new PortfolioConfig();
        _cash = _config.InitialCash;

        bus.Subscribe(HandleEvent);
    }

    private void HandleEvent(Event evt)
    {
        switch (evt.Type)
        {
            case EventTypes.QUOTE:
                HandleQuote(evt);
                break;
            case EventTypes.FILL:
                HandleFill(evt);
                break;
            case EventTypes.BAR:
                // Optionally emit state on bars too.
                break;
        }
    }

    private void HandleQuote(Event evt)
    {
        // Cache mid price for unrealized P&L calculation.
        var bid = EventDataReader.GetDouble(evt.Data, "bid");
        var ask = EventDataReader.GetDouble(evt.Data, "ask");
        _lastMid[evt.Symbol] = (bid + ask) / 2.0;
    }

    private void HandleFill(Event evt)
    {
        var side = EventDataReader.GetString(evt.Data, "side");
        var qty = EventDataReader.GetInt(evt.Data, "qty");
        var price = EventDataReader.GetDouble(evt.Data, "price");
        var fee = EventDataReader.GetDouble(evt.Data, "fee");
        var symbol = evt.Symbol;

        // Update cash: subtract for buys, add for sells, always subtract fees.
        var cashDelta = side == "BUY"
            ? -(price * qty) - fee
            : (price * qty) - fee;
        _cash += cashDelta;

        // Update position.
        if (!_positions.TryGetValue(symbol, out var pos))
        {
            pos = new PositionInfo();
            _positions[symbol] = pos;
        }

        if (side == "BUY")
        {
            // Add to position, update average cost.
            var totalCost = pos.AvgCost * pos.Qty + price * qty;
            pos.Qty += qty;
            pos.AvgCost = pos.Qty > 0 ? totalCost / pos.Qty : 0;
        }
        else // SELL
        {
            // Realize P&L on the sold quantity.
            var realizedOnSale = (price - pos.AvgCost) * qty;
            _realizedPnl += realizedOnSale;

            pos.Qty -= qty;

            // If position goes negative (short), reset avg cost to current price.
            if (pos.Qty < 0)
            {
                pos.AvgCost = price;
            }
            else if (pos.Qty == 0)
            {
                pos.AvgCost = 0;
            }
        }

        _fillCount++;

        // Emit state periodically.
        if (_config.EmitStateEveryNFills > 0 && _fillCount % _config.EmitStateEveryNFills == 0)
        {
            EmitState(evt.Ts);
        }
    }

    /// <summary>
    /// Calculates unrealized P&L using last known mid prices.
    /// </summary>
    private double CalculateUnrealizedPnl()
    {
        double unrealized = 0;
        foreach (var (symbol, pos) in _positions)
        {
            if (pos.Qty == 0) continue;

            if (_lastMid.TryGetValue(symbol, out var mid))
            {
                unrealized += (mid - pos.AvgCost) * pos.Qty;
            }
        }
        return unrealized;
    }

    /// <summary>
    /// Calculates total equity = cash + position value.
    /// </summary>
    private double CalculateEquity()
    {
        double positionValue = 0;
        foreach (var (symbol, pos) in _positions)
        {
            if (pos.Qty == 0) continue;

            if (_lastMid.TryGetValue(symbol, out var mid))
            {
                positionValue += mid * pos.Qty;
            }
        }
        return _cash + positionValue;
    }

    private void EmitState(long ts)
    {
        var unrealizedPnl = CalculateUnrealizedPnl();
        var equity = CalculateEquity();

        // Build positions summary.
        var positionsSummary = new Dictionary<string, object>();
        foreach (var (symbol, pos) in _positions)
        {
            if (pos.Qty != 0)
            {
                positionsSummary[symbol] = new Dictionary<string, object>
                {
                    ["qty"] = pos.Qty,
                    ["avgCost"] = Math.Round(pos.AvgCost, 4)
                };
            }
        }

        var stateData = new Dictionary<string, object>
        {
            ["cash"] = Math.Round(_cash, 2),
            ["equity"] = Math.Round(equity, 2),
            ["realizedPnl"] = Math.Round(_realizedPnl, 2),
            ["unrealizedPnl"] = Math.Round(unrealizedPnl, 2),
            ["positions"] = positionsSummary,
            ["fillCount"] = _fillCount
        };

        var evt = Event.Create(EventTypes.STATE, "", stateData, ts: ts);
        _bus.Emit(evt);
    }

    /// <summary>
    /// Force emit current state (e.g., at shutdown).
    /// </summary>
    public void FlushState(long ts)
    {
        EmitState(ts);
    }

    // Internal position tracking.
    private class PositionInfo
    {
        public int Qty;
        public double AvgCost;
    }
}
