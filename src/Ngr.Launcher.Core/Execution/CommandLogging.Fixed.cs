using System.Globalization;
using System.Text;

namespace Ngr.Launcher.Core.Execution;

public enum CommandOutputStream
{
    StdOut,
    StdErr
}

public static class CommandLogFormatter
{
    public static string Format(DateTimeOffset timestamp, CommandOutputStream stream, string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var streamName = stream switch
        {
            CommandOutputStream.StdOut => "STDOUT",
            CommandOutputStream.StdErr => "STDERR",
            _ => throw new ArgumentOutOfRangeException(nameof(stream))
        };

        return $"[{timestamp.ToString("O", CultureInfo.InvariantCulture)}] {streamName} {text}";
    }
}

public sealed class CappedCommandLog : IDisposable
{
    public const string TruncationMarker = "[output truncated]";

    private readonly StreamWriter _writer;
    private readonly long _maxContentBytes;
    private long _contentBytes;
    private bool _isTruncated;
    private bool _isDisposed;

    public CappedCommandLog(Stream stream, long maxContentBytes)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentOutOfRangeException.ThrowIfNegative(maxContentBytes);

        _maxContentBytes = maxContentBytes;
        _writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            bufferSize: 1024, leaveOpen: true);
    }

    public void Write(string line)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        ArgumentNullException.ThrowIfNull(line);

        if (_isTruncated)
        {
            return;
        }

        var contentBytes = Encoding.UTF8.GetByteCount(line);
        if (_contentBytes + contentBytes <= _maxContentBytes)
        {
            _writer.WriteLine(line);
            _writer.Flush();
            _contentBytes += contentBytes;
            return;
        }

        _writer.WriteLine(TruncationMarker);
        _writer.Flush();
        _isTruncated = true;
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _writer.Dispose();
        _isDisposed = true;
    }
}

public static class CommandLogRetention
{
    public static void RetainLatest(string directory, int count)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        if (!Directory.Exists(directory))
        {
            return;
        }

        var obsoleteLogs = Directory
            .EnumerateFiles(directory, "*.log", SearchOption.TopDirectoryOnly)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ThenByDescending(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .Skip(count);

        foreach (var path in obsoleteLogs)
        {
            File.Delete(path);
        }
    }
}
