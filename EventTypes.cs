namespace TradingSystem;

/// <summary>
/// Centralized event type constants.
/// Using constants avoids typos and enables IDE auto-complete.
/// </summary>
public static class EventTypes
{
    // Market data events
    public const string QUOTE = "QUOTE";
    public const string BAR = "BAR";

    // Trading signal (strategy output)
    public const string SIGNAL = "SIGNAL";

    // Order lifecycle events
    public const string ORDER = "ORDER";
    public const string ORDER_ACCEPTED = "ORDER_ACCEPTED";
    public const string REJECT = "REJECT";
    public const string FILL = "FILL";

    // Portfolio state snapshot
    public const string STATE = "STATE";
}
