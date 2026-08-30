using System.Text;
using Ngr.Launcher.Core.Execution;
using Xunit;

namespace Ngr.Launcher.Core.Tests.Execution;

public sealed class CommandLogBehaviorTests
{
    [Fact]
    public void Hidden_output_lines_have_timestamp_and_stream_marker()
    {
        var line = CommandLogFormatter.Format(
            new DateTimeOffset(2026, 8, 30, 12, 34, 56, TimeSpan.Zero),
            CommandOutputStream.StdOut,
            "hello");

        Assert.Equal("[2026-08-30T12:34:56.0000000+00:00] STDOUT hello", line);
    }

    [Fact]
    public void Capped_log_writes_one_marker_and_ignores_later_output()
    {
        using var stream = new MemoryStream();
        var log = new CappedCommandLog(stream, maxContentBytes: 10);
        log.Write("1234567890");
        log.Write("later");
        log.Write("again");

        var output = Encoding.UTF8.GetString(stream.ToArray());
        Assert.Equal(1, Count(output, CappedCommandLog.TruncationMarker));
        Assert.DoesNotContain("later", output);
        Assert.DoesNotContain("again", output);
    }

    [Fact]
    public void Retention_keeps_latest_files_and_never_deletes_outside_tool_directory()
    {
        var root = Directory.CreateTempSubdirectory("ngr-launcher-log-tests-").FullName;
        try
        {
            var toolDirectory = Directory.CreateDirectory(Path.Combine(root, "tool")).FullName;
            var oldest = CreateLog(toolDirectory, "001.log", new DateTime(2026, 1, 1));
            var middle = CreateLog(toolDirectory, "002.log", new DateTime(2026, 1, 2));
            var newest = CreateLog(toolDirectory, "003.log", new DateTime(2026, 1, 3));
            var outside = CreateLog(root, "outside.log", new DateTime(2025, 1, 1));

            CommandLogRetention.RetainLatest(toolDirectory, count: 2);

            Assert.False(File.Exists(oldest));
            Assert.True(File.Exists(middle));
            Assert.True(File.Exists(newest));
            Assert.True(File.Exists(outside));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateLog(string directory, string name, DateTime lastWriteTimeUtc)
    {
        var path = Path.Combine(directory, name);
        File.WriteAllText(path, string.Empty);
        File.SetLastWriteTimeUtc(path, lastWriteTimeUtc);
        return path;
    }

    private static int Count(string text, string value) =>
        text.Split(value, StringSplitOptions.None).Length - 1;
}
