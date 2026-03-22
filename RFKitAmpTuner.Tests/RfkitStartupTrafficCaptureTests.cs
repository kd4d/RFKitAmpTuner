using System;
using RFKitAmpTuner.MyModel.Internal;
using Xunit;

namespace RFKitAmpTuner.Tests;

public sealed class RfkitStartupTrafficCaptureTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(-1, 0)]
    [InlineData(1, 1)]
    [InlineData(600, 600)]
    [InlineData(3600, 3600)]
    [InlineData(7200, 7200)]
    [InlineData(7201, 7200)]
    [InlineData(999999, 7200)]
    public void NormalizeStartupCaptureSeconds_PositiveSecondsOrClamp(int input, int expected)
    {
        Assert.Equal(expected, RfkitStartupTrafficCapture.NormalizeStartupCaptureSeconds(input));
    }

    [Fact]
    public void TruncateForLog_LeavesShortStrings()
    {
        var s = new string('a', 100);
        Assert.Equal(s, RfkitStartupTrafficCapture.TruncateForLog(s, 8192));
    }

    [Fact]
    public void TruncateForLog_AppendsMarkerWhenLong()
    {
        var s = new string('b', 100);
        var t = RfkitStartupTrafficCapture.TruncateForLog(s, 20);
        Assert.True(t.Length < s.Length);
        Assert.Contains("truncated", t, StringComparison.OrdinalIgnoreCase);
    }
}
