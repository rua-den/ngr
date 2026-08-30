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

    private readonly object _gate = new();
    private readonly StreamWriter _writer;
    private readonly long _maxPhysicalBytes;
    private readonly int _markerBytes;
    private long _physicalBytes;
    private bool _isTruncated;
    private bool _isDisposed;

    public CappedCommandLog(Stream stream, long maxContentBytes)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var markerBytes = Encoding.UTF8.GetByteCount(TruncationMarker + Environment.NewLine);
        if (maxContentBytes < markerBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxContentBytes),
                maxContentBytes,
                "The log cap must have room for the complete truncation marker and newline.");
        }

        _maxPhysicalBytes = maxContentBytes;
        _markerBytes = markerBytes;
        _physicalBytes = stream.CanSeek ? stream.Length : 0;
        _writer = new StreamWriter(
            stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            bufferSize: 1024,
            leaveOpen: true);
    }

    public void Write(string line)
    {
        ArgumentNullException.ThrowIfNull(line);

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);

            if (_isTruncated)
            {
                return;
            }

            var lineBytes = Encoding.UTF8.GetByteCount(line + _writer.NewLine);
            var contentLimit = _maxPhysicalBytes - _markerBytes;
            if (_physicalBytes + lineBytes <= contentLimit)
            {
                _writer.WriteLine(line);
                _writer.Flush();
                _physicalBytes += lineBytes;
                return;
            }

            _writer.WriteLine(TruncationMarker);
            _writer.Flush();
            _physicalBytes += _markerBytes;
            _isTruncated = true;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_isDisposed)
            {
                return;
            }

            _writer.Dispose();
            _isDisposed = true;
        }
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
