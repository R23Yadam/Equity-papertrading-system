using System.Text.Json;

namespace TradingSystem;

public sealed class JsonlLogger : IDisposable
{
    private readonly StreamWriter _writer;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = false };

    public JsonlLogger(string path)
    {
        // Append + FileShare.Read keeps tail -f working while we write.
        var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
        _writer = new StreamWriter(stream) { AutoFlush = true };
    }

    public void Handle(Event evt)
    {
        var line = JsonSerializer.Serialize(evt, _jsonOptions);
        _writer.WriteLine(line);
    }

    public void Dispose()
    {
        _writer.Dispose();
    }
}
