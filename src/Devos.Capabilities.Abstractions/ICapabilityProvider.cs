namespace Devos.Capabilities.Abstractions;

public interface ICapabilityProvider
{
    string ProviderId { get; }
    IReadOnlySet<string> Capabilities { get; }
    bool IsAvailable { get; }
}

public sealed record CapabilityRequest(string Capability, IReadOnlyDictionary<string, string>? Constraints = null);
