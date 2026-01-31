namespace TradingSystem;

/// <summary>
/// Configuration for OrderManager.
/// Separating config enables easy testing with different parameters.
/// </summary>
public sealed class OrderManagerConfig
{
    // Default quantity for orders; strategy-agnostic fixed size.
    public int DefaultQty { get; init; } = 10;
}

/// <summary>
/// Converts SIGNAL events into ORDER events.
/// Acts as the interface between strategy signals and order management.
/// </summary>
public sealed class OrderManager
{
    private readonly EventBus _bus;
    private readonly OrderManagerConfig _config;

    public OrderManager(EventBus bus, OrderManagerConfig? config = null)
    {
        _bus = bus;
        _config = config ?? new OrderManagerConfig();

        // Subscribe to signals from strategies.
        bus.Subscribe(HandleEvent);
    }

    private void HandleEvent(Event evt)
    {
        if (evt.Type != EventTypes.SIGNAL) return;

        var side = EventDataReader.GetString(evt.Data, "side");

        // Validate side is BUY or SELL.
        if (side != "BUY" && side != "SELL")
        {
            Console.WriteLine($"[OrderManager] Invalid signal side: {side}");
            return;
        }

        // Use qty from signal if provided, otherwise use default.
        var qty = EventDataReader.GetIntOrDefault(evt.Data, "qty", _config.DefaultQty);

        var orderData = new Dictionary<string, object>
        {
            ["orderId"] = Guid.NewGuid().ToString(),
            ["side"] = side,
            ["qty"] = qty,
            ["symbol"] = evt.Symbol,
            ["ts"] = evt.Ts
        };

        var order = Event.Create(EventTypes.ORDER, evt.Symbol, orderData, ts: evt.Ts);
        _bus.Emit(order);
    }
}
