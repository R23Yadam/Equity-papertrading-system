namespace TradingSystem;

public sealed class EventBus
{
    private readonly List<Action<Event>> _handlers = new();

    // Handlers are invoked in subscription order for determinism.
    public void Subscribe(Action<Event> handler)
    {
        _handlers.Add(handler);
    }

    public void Emit(Event evt)
    {
        foreach (var handler in _handlers)
        {
            handler(evt);
        }
    }
}
