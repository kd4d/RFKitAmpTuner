using RFKitAmpTuner.MyModel;
using Xunit;

namespace RFKitAmpTuner.Tests;

public sealed class RFKitAmpTunerConfigurationTests
{
    [Fact]
    public void RfkitStartupCaptureSeconds_DefaultsTo60()
    {
        var c = new RFKitAmpTunerConfiguration();
        Assert.Equal(60, c.RfkitStartupCaptureSeconds);
    }
}
