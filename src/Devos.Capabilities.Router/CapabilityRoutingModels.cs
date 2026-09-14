using Devos.Capabilities.Abstractions;

namespace Devos.Capabilities.Router;

public enum CapabilityProviderKind
{
    Unknown,
    InteractiveBrowser,
    Extraction,
    NativeDesktop,
    Ai,
    Storage,
    Verification,
    HumanIntervention
}

public sealed record ProviderRoutingProfile(
    string ProviderId,
    CapabilityProviderKind Kind,
    int Priority = 0,
    double Reliability = 1.0,
    bool Experimental = false)
{
    public double NormalizedReliability => Math.Clamp(Reliability, 0.0, 1.0);

    public static ProviderRoutingProfile Infer(string providerId)
    {
        var id = providerId.ToLowerInvariant();

        if (id.Contains("firecrawl", StringComparison.Ordinal) || id.Contains("extraction", StringComparison.Ordinal))
        {
            return new ProviderRoutingProfile(providerId, CapabilityProviderKind.Extraction, Priority: 30, Reliability: 0.90);
        }

        if (id.Contains("chrome", StringComparison.Ordinal) || id.Contains("cdp", StringComparison.Ordinal) || id.Contains("edge", StringComparison.Ordinal))
        {
            return new ProviderRoutingProfile(providerId, CapabilityProviderKind.InteractiveBrowser, Priority: 25, Reliability: 0.88);
        }

        if (id.Contains("playwright", StringComparison.Ordinal))
        {
            return new ProviderRoutingProfile(providerId, CapabilityProviderKind.InteractiveBrowser, Priority: 22, Reliability: 0.92);
        }

        if (id.Contains("browseract", StringComparison.Ordinal) || id.Contains("open-browser-use", StringComparison.Ordinal) || id.Contains("chrome-use", StringComparison.Ordinal))
        {
            return new ProviderRoutingProfile(providerId, CapabilityProviderKind.InteractiveBrowser, Priority: 10, Reliability: 0.70, Experimental: true);
        }

        if (id.Contains("native", StringComparison.Ordinal) || id.Contains("windows", StringComparison.Ordinal))
        {
            return new ProviderRoutingProfile(providerId, CapabilityProviderKind.NativeDesktop, Priority: 15, Reliability: 0.80);
        }

        if (id.Contains("ai", StringComparison.Ordinal) || id.Contains("ollama", StringComparison.Ordinal) || id.Contains("deterministic", StringComparison.Ordinal))
        {
            return new ProviderRoutingProfile(providerId, CapabilityProviderKind.Ai, Priority: 10, Reliability: 0.75);
        }

        return new ProviderRoutingProfile(providerId, CapabilityProviderKind.Unknown);
    }
}

public sealed record CapabilityRoutingPolicy(
    IReadOnlyDictionary<string, ProviderRoutingProfile>? ProviderProfiles = null,
    bool AllowExperimentalProviders = false,
    double MinimumConfidence = 0.10)
{
    public static CapabilityRoutingPolicy Default { get; } = new();

    public ProviderRoutingProfile ProfileFor(string providerId)
    {
        if (ProviderProfiles is not null && ProviderProfiles.TryGetValue(providerId, out var profile))
        {
            return profile;
        }

        return ProviderRoutingProfile.Infer(providerId);
    }
}

public sealed record CapabilityRouteCandidate(
    string ProviderId,
    CapabilityProviderKind Kind,
    string Capability,
    bool IsAvailable,
    bool SupportsCapability,
    bool IsEligible,
    int ProviderOrder,
    int Priority,
    double Reliability,
    double Score,
    string Reason);

public sealed record CapabilityRouteResult(
    ICapabilityProvider Provider,
    double Confidence,
    IReadOnlyList<CapabilityRouteCandidate> Candidates,
    string Reason);
