using System.Text;

namespace CodeModernizer.Core.Models;

/// <summary>
/// Thread-safe append-only text buffer used to expose an AI model's streaming
/// output to polling clients (read from an offset, get back the new tail).
/// </summary>
public sealed class StreamLog
{
    private readonly StringBuilder _buffer = new();
    private readonly Lock _lock = new();

    public void Reset(string? header = null)
    {
        lock (_lock)
        {
            _buffer.Clear();
            if (header is not null) _buffer.Append(header);
        }
    }

    public void Append(string text)
    {
        lock (_lock) _buffer.Append(text);
    }

    /// <summary>Returns the text written after <paramref name="from"/> and the new offset.</summary>
    public (int Next, string Chunk) Read(int from)
    {
        lock (_lock)
        {
            if (from < 0 || from > _buffer.Length) from = 0;
            return (_buffer.Length, _buffer.ToString(from, _buffer.Length - from));
        }
    }
}
