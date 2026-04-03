using RFKitAmpTuner.MyModel.Internal;
using Xunit;

namespace RFKitAmpTuner.Tests;

public sealed class RfkitCommandMapperTests
{
    [Theory]
    [InlineData("$FRQ07039", "$FRQ 07039;")]
    [InlineData("$FRQ07039;", "$FRQ 07039;")]
    [InlineData("$FRQ12", null)]
    [InlineData("$FRQabcde", null)]
    public void BuildFrqEchoLine_FiveDigitKhzSuffix(string token, string? expected)
    {
        Assert.Equal(expected, RfkitCommandMapper.BuildFrqEchoLine(token.TrimEnd(';')));
    }

    [Fact]
    public void ProcessOneCommand_Idn_FetchesInfo()
    {
        var fake = new FakeRfkitRestClient();
        fake.JsonByPath[RfkitRestPaths.Info] = GoldenJson.InfoSample;
        var line = RfkitCommandMapper.ProcessOneCommand("$IDN", fake);
        Assert.Equal("$IDN B26 RF2K-S (emulator);$VER 131;", line);
        Assert.Contains(fake.Calls, c => c is { Method: "GET", Path: "info" });
    }

    [Fact]
    public void ProcessOneCommand_Pwr_FetchesPower()
    {
        var fake = new FakeRfkitRestClient();
        fake.JsonByPath[RfkitRestPaths.Power] = GoldenJson.Power;
        var line = RfkitCommandMapper.ProcessOneCommand("$PWR", fake);
        Assert.Equal("$PWR 10 12;", line);
        Assert.Contains(fake.Calls, c => c is { Method: "GET", Path: "power" });
    }

    [Fact]
    public void ProcessOneCommand_Opr1_PutsOperateAndReturnsEcho()
    {
        var fake = new FakeRfkitRestClient();
        var line = RfkitCommandMapper.ProcessOneCommand("$OPR1", fake);
        Assert.Equal("$OPR 1;", line);
        var put = Assert.Single(fake.Calls, c => c.Method == "PUT");
        Assert.Equal(RfkitRestPaths.OperateMode, put.Path);
        Assert.Equal("""{"operate_mode":"OPERATE"}""", put.Body);
    }

    [Fact]
    public void ProcessOneCommand_Bnd_FetchesData()
    {
        var fake = new FakeRfkitRestClient();
        fake.JsonByPath[RfkitRestPaths.Data] = GoldenJson.Data7039;
        var line = RfkitCommandMapper.ProcessOneCommand("$BND", fake);
        Assert.Equal("$BND 3;", line);
        Assert.Contains(fake.Calls, c => c is { Method: "GET", Path: "data" });
    }

    [Fact]
    public void ProcessOneCommand_Flc_PostsErrorReset()
    {
        var fake = new FakeRfkitRestClient();
        var line = RfkitCommandMapper.ProcessOneCommand("$FLC", fake);
        Assert.Equal("$FLT 0;", line);
        var post = Assert.Single(fake.Calls);
        Assert.Equal("POST", post.Method);
        Assert.Equal(RfkitRestPaths.ErrorReset, post.Path);
    }

    [Fact]
    public void ProcessOneCommand_Ant1_PutsAntennasActive()
    {
        var fake = new FakeRfkitRestClient();
        var line = RfkitCommandMapper.ProcessOneCommand("$ANT 1", fake);
        Assert.Null(line);
        var put = Assert.Single(fake.Calls);
        Assert.Equal("PUT", put.Method);
        Assert.Equal(RfkitRestPaths.AntennasActive, put.Path);
        Assert.Equal("""{"type":"INTERNAL","number":1}""", put.Body);
    }

    [Fact]
    public void ProcessOneCommand_Ver_FetchesInfo()
    {
        var fake = new FakeRfkitRestClient();
        fake.JsonByPath[RfkitRestPaths.Info] = GoldenJson.InfoSample;
        var line = RfkitCommandMapper.ProcessOneCommand("$VER", fake);
        Assert.Equal("$VER 131;", line);
    }

    [Fact]
    public void ProcessOneCommand_Ser_FetchesInfo()
    {
        var fake = new FakeRfkitRestClient();
        fake.JsonByPath[RfkitRestPaths.Info] = GoldenJson.InfoSample;
        var line = RfkitCommandMapper.ProcessOneCommand("$SER", fake);
        Assert.Equal("$SER RfkitEmulator MVP;", line);
    }

    [Fact]
    public void ProcessOneCommand_UnknownToken_ReturnsNull()
    {
        var fake = new FakeRfkitRestClient();
        var line = RfkitCommandMapper.ProcessOneCommand("$ZZZ", fake);
        Assert.Null(line);
    }
}
