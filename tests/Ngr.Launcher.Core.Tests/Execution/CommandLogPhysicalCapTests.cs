using System.Text;
using Ngr.Launcher.Core.Execution;
using Xunit;

namespace Ngr.Launcher.Core.Tests.Execution;

public sealed class CommandLogPhysicalCapTests
{
    [Fact]
    public void Cap_includes_utf8_newlines_and_the_single_truncation_marker()
    {
        const int maxBytes = 64;
        using var stream = new MemoryStream();
        using var log = new CappedCommandLog(stream, maxBytes);

        log.Write(new string('x', 50));
        log.Write("later");
        log.Write("again");

        var output = Encoding.UTF8.GetString(stream.ToArray());
        Assert.True(stream.Length <= maxBytes, $"Physical log length was {stream.Length} bytes.");
        Assert.Equal(1, Count(output, CappedCommandLog.TruncationMarker));
        Assert.DoesNotContain("later", output, StringComparison.Ordinal);
        Assert.DoesNotContain("again", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Cap_must_have_room_for_a_complete_marker_and_newline()
    {
        var minimum = Encoding.UTF8.GetByteCount(
            CappedCommandLog.TruncationMarker + Environment.NewLine);

        using var stream = new MemoryStream();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CappedCommandLog(stream, minimum - 1));
    }

    [Fact]
    public async Task Concurrent_writers_remain_bounded_and_emit_at_most_one_marker()
    {
        const int maxBytes = 512;
        using var stream = new MemoryStream();
        using var log = new CappedCommandLog(stream, maxBytes);

        await Task.WhenAll(Enumerable.Range(0, 100).Select(index =>
            Task.Run(() => log.Write($"line-{index:D3}-{new string('z', 24)}"))));

        var output = Encoding.UTF8.GetString(stream.ToArray());
        Assert.True(stream.Length <= maxBytes, $"Physical log length was {stream.Length} bytes.");
        Assert.InRange(Count(output, CappedCommandLog.TruncationMarker), 0, 1);
    }

    private static int Count(string text, string value) =>
        text.Split(value, StringSplitOptions.None).Length - 1;
}
