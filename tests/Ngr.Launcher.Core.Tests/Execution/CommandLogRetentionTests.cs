using Ngr.Launcher.Core.Execution;
using Xunit;

namespace Ngr.Launcher.Core.Tests.Execution;

public sealed class CommandLogRetentionTests
{
    [Fact]
    public void Hidden_output_lines_have_timestamp_and_stream_marker()
    {
        var line = CommandLogFormatter.Format(DateTimeOffset.UtcNow, CommandOutputStream.StdOut, "hello");
        Assert.Contains("STDOUT", line);
        Assert.Matches(@"^\[[^\]]+\] STDOUT hello$", line);
    }

    [Fact]
    public void Capped_log_writes_one_marker_and_ignores_later_bytes()
    {
        using var stream = new MemoryStream();
        var log = new CappedCommandLog(stream, 10);
        log.Write("1234567890");
        log.Write("later");
        log.Write("again");

        var output = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        Assert.Equal(1, Count(output, CappedCommandLog.TruncationMarker));
        Assert.DoesNotContain("later", output);
        Assert.DoesNotContain("again", output);
    }

    [Fact]
    public void Retention_keeps_latest_N_files_and_deletes_only_inside_tool_directory()
    {
        var root = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var tool = Path.Combine(root, "tool"); Directory.CreateDirectory(tool);
            File.WriteAllText(Path.Combine(tool, "001.log"), "");
            File.WriteAllText(Path.Combine(tool, "002.log"), "");
            File.WriteAllText(Path.Combine(tool, "003.log"), "");
            var outside = Path.Combine(root, "outside.log"); File.WriteAllText(outside, "");

            CommandLogRetention.RetainLatest(tool, 2);

            Assert.False(File.Exists(Path.Combine(tool, "001.log")));
            Assert.True(File.Exists(Path.Combine(tool, "002.log")));
            Assert.True(File.Exists(Path.Combine(tool, "003.log")));
            Assert.True(File.Exists(outside));
        }
        finally { Directory.Delete(root, true); }
    }

    private static int Count(string text, string value) => text.Split(value, StringSplitOptions.None).Length - 1;
}
