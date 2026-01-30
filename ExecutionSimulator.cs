namespace TradingSystem;

/// <summary>
/// Configuration for execution simulation.
/// </summary>
public sealed class ExecutionConfig
{
    // Fee per share (e.g., $0.005 per share).
    public double FeesPerShare { get; init; } = 0.005;

    // Slippage in basis points (1 bp = 0.01%).
    public double SlippageBps { get; init; } = 5.0;
}

/// <summary>
/// Simulates order execution against market data.
/// Fills BUY orders at ask, SELL orders at bid, with fees and slippage.
/// </summary>
public sealed class ExecutionSimulator
{
    private readonly EventBus _bus;
    private readonly ExecutionConfig _config;

    // Cache last quote per symbol for fill price.
    private readonly Dictionary<string, (double Bid, double Ask)> _lastQuotes = new();

    public ExecutionSimulator(EventBus bus, ExecutionConfig? config = null)
    {
        _bus = bus;
        _config = config ?? new ExecutionConfig();

        // Need both QUOTE (for prices) and ORDER_ACCEPTED (to fill).
        bus.Subscribe(HandleEvent);
    }

    private void HandleEvent(Event evt)
    {
        switch (evt.Type)
        {
            case EventTypes.QUOTE:
                HandleQuote(evt);
                break;
            case EventTypes.ORDER_ACCEPTED:
                HandleOrderAccepted(evt);
                break;
        }
    }

    private void HandleQuote(Event evt)
    {
        // Cache quote for fill pricing.
        var bid = EventDataReader.GetDouble(evt.Data, "bid");
        var ask = EventDataReader.GetDouble(evt.Data, "ask");
        _lastQuotes[evt.Symbol] = (bid, ask);
    }

    private void HandleOrderAccepted(Event evt)
    {
        var orderId = EventDataReader.GetString(evt.Data, "orderId");
        var side = EventDataReader.GetString(evt.Data, "side");
        var qty = EventDataReader.GetInt(evt.Data, "qty");
        var symbol = evt.Symbol;

        // Need a quote to fill against.
        if (!_lastQuotes.TryGetValue(symbol, out var quote))
        {
            Console.WriteLine($"[ExecutionSimulator] No quote for {symbol}, cannot fill order {orderId}");
            return;
        }

        // Fill BUY at ask (buying from sellers), SELL at bid (selling to buyers).
        var basePrice = side == "BUY" ? quote.Ask : quote.Bid;

        // Apply slippage: adverse for the trader.
        // BUY: price goes up (worse), SELL: price goes down (worse).
        var slippageMultiplier = _config.SlippageBps / 10000.0;
        var slippageAmount = basePrice * slippageMultiplier;
        var fillPrice = side == "BUY"
            ? basePrice + slippageAmount
            : basePrice - slippageAmount;

        // Calculate fees.
        var fee = _config.FeesPerShare * qty;

        var fillData = new Dictionary<string, object>
        {
            ["orderId"] = orderId,
            ["side"] = side,
            ["qty"] = qty,
            ["price"] = Math.Round(fillPrice, 4),
            ["fee"] = Math.Round(fee, 4),
            ["slippage"] = Math.Round(slippageAmount * qty, 4),
            ["ts"] = evt.Ts
        };

        var fill = Event.Create(EventTypes.FILL, symbol, fillData, ts: evt.Ts);
        _bus.Emit(fill);
    }
}
