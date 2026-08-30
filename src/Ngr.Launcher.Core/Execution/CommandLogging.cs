using System.Globalization;
using System.Text;

namespace Ngr.Launcher.Core.Execution;

public sealed record CommandOutputStream(DateTimeOffset Timestamp, string Stream, string Text);

public static class CommandLogTimestamp
{
    public static string Format(DateTimeOffset timestamp) => timestamp.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
}

public sealed class CappedCommandLog : IDisposable
{
    private readonly StreamWriter _writer;
    private readonly long _maxBytes;
    private long _bytes;
    private bool _truncated;
    private const string Marker = "[output truncated]";

    public CappedCommandLog(Stream stream, long maxContentBytes)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (maxContentBytes < 0) throw new ArgumentOutOfRangeException(nameof(maxContentBytes));
        _maxBytes = maxContentBytes;
        _writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, leaveOpen: true);
    }

    public void Write(CommandOutputStream output)
    {
        if (_truncated) return;
        var line = $"{CommandLogTimestamp.Format(output.Timestamp)} [{output.Stream}] {output.Text}{Environment.NewLine}";
        var bytes = Encoding.UTF8.GetBytes(line);
        if (_bytes + bytes.Length <= _maxBytes) { _writer.Write(line); _writer.Flush(); _bytes += bytes.Length; return; }
        var marker = Encoding.UTF8.GetBytes(Marker + Environment.NewLine);
        var remaining = Math.Max(0, _maxBytes - _bytes);
        if (remaining >= marker.Length) { _writer.Write(Marker); _writer.Write(Environment.NewLine); _writer.Flush(); _bytes += marker.Length; }
        _truncated = true;
    }

    public void Dispose() => _writer.Flush();
}

public static class CommandLogRetention
{
    public static void RetainLatest(string directory, int count)
    {
        ArgumentNullException.ThrowIfNull(directory);
        if (count < 1) throw new ArgumentOutOfRangeException(nameof(count));
        var files = Directory.EnumerateFiles(directory, "*.log", SearchOption.TopDirectoryOnly)
            .OrderByDescending(File.GetLastWriteTimeUtc).ThenByDescending(Path.GetFileName, StringComparer.OrdinalIgnoreCase).ToArray();
        foreach (var file in files.Skip(count)) File.Delete(file);
    }
}
