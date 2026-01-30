using System.Text.Json;

namespace TradingSystem;

public sealed class ReplayRunner
{
    private readonly EventBus _bus;

    public ReplayRunner(EventBus bus)
    {
        _bus = bus;
    }

    public void Replay(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        // Stream line-by-line to preserve order without loading the whole log.
        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var evt = JsonSerializer.Deserialize<Event>(line);
            if (evt is not null)
            {
                _bus.Emit(evt);
            }
        }
    }
}
