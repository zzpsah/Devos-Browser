using Devos.Browser.ChromeCdp;
using Xunit;

namespace Devos.UnitTests;

public sealed class ChromeCdpBridgeMessageTests
{
    [Fact]
    public void ValidHelloMessagePassesValidation()
    {
        var message = new ChromeCdpBridgeMessage(
            ProtocolVersion: "1.0",
            MessageId: "msg-1",
            Kind: ChromeCdpBridgeMessageKinds.Hello,
            Timestamp: DateTimeOffset.UtcNow);

        var result = ChromeCdpBridgeMessageValidator.Validate(message);

        Assert.True(result.IsValid);
        Assert.Null(result.Error);
    }

    [Fact]
    public void ExecuteActionRequiresTabId()
    {
        var message = new ChromeCdpBridgeMessage(
            ProtocolVersion: "1.0",
            MessageId: "msg-2",
            Kind: ChromeCdpBridgeMessageKinds.ExecuteAction,
            Timestamp: DateTimeOffset.UtcNow);

        var result = ChromeCdpBridgeMessageValidator.Validate(message);

        Assert.False(result.IsValid);
        Assert.Contains("tab id", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UnsupportedKindFailsClosed()
    {
        var message = new ChromeCdpBridgeMessage(
            ProtocolVersion: "1.0",
            MessageId: "msg-3",
            Kind: "executeArbitraryCode",
            Timestamp: DateTimeOffset.UtcNow);

        var result = ChromeCdpBridgeMessageValidator.Validate(message);

        Assert.False(result.IsValid);
        Assert.Contains("unsupported", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InvalidProtocolVersionIsRejected()
    {
        var message = new ChromeCdpBridgeMessage(
            ProtocolVersion: "latest",
            MessageId: "msg-4",
            Kind: ChromeCdpBridgeMessageKinds.Ping,
            Timestamp: DateTimeOffset.UtcNow);

        var result = ChromeCdpBridgeMessageValidator.Validate(message);

        Assert.False(result.IsValid);
        Assert.Contains("protocol version", result.Error, StringComparison.OrdinalIgnoreCase);
    }
}
