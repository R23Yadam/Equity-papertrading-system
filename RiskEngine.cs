namespace TradingSystem;

/// <summary>
/// Configuration for RiskEngine limits.
/// </summary>
public sealed class RiskConfig
{
    // Maximum quantity per single order.
    public int MaxOrderQty { get; init; } = 100;

    // Maximum absolute position size per symbol.
    public int MaxPositionQty { get; init; } = 500;
}

/// <summary>
/// Enforces risk limits on orders before execution.
/// Tracks positions via FILL events to enforce position limits.
/// </summary>
public sealed class RiskEngine
{
    private readonly EventBus _bus;
    private readonly RiskConfig _config;

    // Track current position per symbol (positive = long, negative = short).
    private readonly Dictionary<string, int> _positions = new();

    public RiskEngine(EventBus bus, RiskConfig? config = null)
    {
        _bus = bus;
        _config = config ?? new RiskConfig();

        // Subscribe to both ORDER (to validate) and FILL (to track positions).
        bus.Subscribe(HandleEvent);
    }

    private void HandleEvent(Event evt)
    {
        switch (evt.Type)
        {
            case EventTypes.ORDER:
                HandleOrder(evt);
                break;
            case EventTypes.FILL:
                HandleFill(evt);
                break;
        }
    }

    private void HandleOrder(Event evt)
    {
        var orderId = EventDataReader.GetString(evt.Data, "orderId");
        var side = EventDataReader.GetString(evt.Data, "side");
        var qty = EventDataReader.GetInt(evt.Data, "qty");
        var symbol = evt.Symbol;

        // Check order size limit.
        if (qty > _config.MaxOrderQty)
        {
            EmitReject(orderId, symbol, evt.Ts,
                $"Order qty {qty} exceeds max {_config.MaxOrderQty}");
            return;
        }

        // Calculate resulting position after this order.
        var currentPos = _positions.GetValueOrDefault(symbol, 0);
        var delta = side == "BUY" ? qty : -qty;
        var newPos = currentPos + delta;

        // Check position limit.
        if (Math.Abs(newPos) > _config.MaxPositionQty)
        {
            EmitReject(orderId, symbol, evt.Ts,
                $"Position would be {newPos}, exceeds max {_config.MaxPositionQty}");
            return;
        }

        // Order passes all checks.
        EmitAccepted(evt);
    }

    private void HandleFill(Event evt)
    {
        // Update position tracking from fills.
        var side = EventDataReader.GetString(evt.Data, "side");
        var qty = EventDataReader.GetInt(evt.Data, "qty");
        var symbol = evt.Symbol;

        var delta = side == "BUY" ? qty : -qty;
        _positions[symbol] = _positions.GetValueOrDefault(symbol, 0) + delta;
    }

    private void EmitAccepted(Event originalOrder)
    {
        // Pass through original order data with ORDER_ACCEPTED type.
        var acceptedData = new Dictionary<string, object>
        {
            ["orderId"] = EventDataReader.GetString(originalOrder.Data, "orderId"),
            ["side"] = EventDataReader.GetString(originalOrder.Data, "side"),
            ["qty"] = EventDataReader.GetInt(originalOrder.Data, "qty"),
            ["symbol"] = originalOrder.Symbol,
            ["ts"] = originalOrder.Ts
        };

        var evt = Event.Create(EventTypes.ORDER_ACCEPTED, originalOrder.Symbol, acceptedData, ts: originalOrder.Ts);
        _bus.Emit(evt);
    }

    private void EmitReject(string orderId, string symbol, long ts, string reason)
    {
        var rejectData = new Dictionary<string, object>
        {
            ["orderId"] = orderId,
            ["reason"] = reason
        };

        var evt = Event.Create(EventTypes.REJECT, symbol, rejectData, ts: ts);
        _bus.Emit(evt);

        Console.WriteLine($"[RiskEngine] REJECT: {reason}");
    }
}
