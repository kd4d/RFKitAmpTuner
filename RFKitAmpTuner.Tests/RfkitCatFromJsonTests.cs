using System.Text.Json;
using RFKitAmpTuner.MyModel.Internal;
using Xunit;

namespace RFKitAmpTuner.Tests;

public sealed class RfkitCatFromJsonTests
{
    [Fact]
    public void PowerLine_FromGoldenPower_MatchesExpected()
    {
        using var doc = JsonDocument.Parse(GoldenJson.Power);
        Assert.Equal("$PWR 10 12;", RfkitCatFromJson.PowerLine(doc.RootElement));
    }

    [Fact]
    public void TmpFromPower_RoundsTemperature()
    {
        using var doc = JsonDocument.Parse(GoldenJson.Power);
        Assert.Equal("$TMP 24;", RfkitCatFromJson.TmpFromPower(doc.RootElement));
    }

    [Fact]
    public void OprLineFromOperateMode_Operate_ReturnsOne()
    {
        using var doc = JsonDocument.Parse(GoldenJson.OperateModeOperate);
        Assert.Equal("$OPR 1;", RfkitCatFromJson.OprLineFromOperateMode(doc.RootElement));
    }

    [Fact]
    public void IdentifyLines_IncludesIdnAndVer()
    {
        using var doc = JsonDocument.Parse(GoldenJson.InfoSample);
        Assert.Equal("$IDN B26 RF2K-S (emulator);$VER 131;", RfkitCatFromJson.IdentifyLines(doc.RootElement));
    }

    [Fact]
    public void VerPollLine_ReturnsControllerVersion()
    {
        using var doc = JsonDocument.Parse(GoldenJson.InfoSample);
        Assert.Equal("$VER 131;", RfkitCatFromJson.VerPollLine(doc.RootElement));
    }

    [Fact]
    public void SerPollLine_PrefersCustomDeviceName()
    {
        using var doc = JsonDocument.Parse(GoldenJson.InfoSample);
        Assert.Equal("$SER RfkitEmulator MVP;", RfkitCatFromJson.SerPollLine(doc.RootElement));
    }

    [Fact]
    public void BndLineFromData_7039kHz_IsBand3()
    {
        using var doc = JsonDocument.Parse(GoldenJson.Data7039);
        Assert.Equal("$BND 3;", RfkitCatFromJson.BndLineFromData(doc.RootElement));
    }

    [Fact]
    public void VltLineFromPower_ScalesTenths()
    {
        using var doc = JsonDocument.Parse(GoldenJson.Power);
        Assert.Equal("$VLT 533 0;", RfkitCatFromJson.VltLineFromPower(doc.RootElement));
    }

    [Fact]
    public void BypTplIndCap_FromGoldenTuner()
    {
        using var doc = JsonDocument.Parse(GoldenJson.TunerModeBypassWithLc);
        Assert.Equal("$BYP B;", RfkitCatFromJson.BypLineFromTuner(doc.RootElement));
        Assert.Equal("$TPL 0;", RfkitCatFromJson.TplLineFromTuner(doc.RootElement));
        Assert.Equal("$IND FF;", RfkitCatFromJson.IndLineFromTuner(doc.RootElement));
        Assert.Equal("$CAP 10;", RfkitCatFromJson.CapLineFromTuner(doc.RootElement));
    }

    [Fact]
    public void TplLine_AutoTuning_IsOne()
    {
        using var doc = JsonDocument.Parse(GoldenJson.TunerAutoTuning);
        Assert.Equal("$TPL 1;", RfkitCatFromJson.TplLineFromTuner(doc.RootElement));
    }

    [Fact]
    public void SwrFpw_FromPower()
    {
        using var doc = JsonDocument.Parse(GoldenJson.Power);
        Assert.Equal("$SWR 1.15;", RfkitCatFromJson.SwrLineFromPower(doc.RootElement));
        Assert.Equal("$FPW 10;", RfkitCatFromJson.FpwLineFromPower(doc.RootElement));
    }

    [Fact]
    public void FltLineFromData_EmptyStatus_IsZero()
    {
        using var doc = JsonDocument.Parse(GoldenJson.Data7039);
        Assert.Equal("$FLT 0;", RfkitCatFromJson.FltLineFromData(doc.RootElement));
    }

    [Fact]
    public void FltLineFromData_NumericStatus_Parsed()
    {
        const string json = """{"status":"42","frequency":{"value":0,"unit":"kHz"}}""";
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("$FLT 42;", RfkitCatFromJson.FltLineFromData(doc.RootElement));
    }

    [Theory]
    [InlineData(1800, 0)]
    [InlineData(3500, 1)]
    [InlineData(7039, 3)]
    [InlineData(14000, 5)]
    [InlineData(50000, 10)]
    public void FrequencyKhzToBandIndex_Buckets(int kHz, int band)
    {
        Assert.Equal(band, RfkitCatFromJson.FrequencyKhzToBandIndex(kHz));
    }
}
