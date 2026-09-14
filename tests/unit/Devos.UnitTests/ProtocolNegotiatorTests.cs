using Devos.Protocol;
using Xunit;

namespace Devos.UnitTests;

public sealed class ProtocolNegotiatorTests
{
    [Fact]
    public void CompatibleMinorVersionConnects()
    {
        var result = ProtocolNegotiator.Negotiate("1.0", "1.2");

        Assert.True(result.IsCompatible);
        Assert.Equal("CONNECTED", result.State);
    }

    [Fact]
    public void OlderProviderRequiresBridgeUpdate()
    {
        var result = ProtocolNegotiator.Negotiate("1.2", "1.0");

        Assert.False(result.IsCompatible);
        Assert.Equal("BRIDGE_UPDATE_REQUIRED", result.State);
    }

    [Fact]
    public void MajorMismatchRequiresBridgeUpdate()
    {
        var result = ProtocolNegotiator.Negotiate("2.0", "1.9");

        Assert.False(result.IsCompatible);
        Assert.Equal("BRIDGE_UPDATE_REQUIRED", result.State);
    }
}
