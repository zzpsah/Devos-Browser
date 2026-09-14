using Devos.Capabilities.Abstractions;

namespace Devos.Capabilities.Router;

public sealed class CapabilityRouter
{
    private readonly IReadOnlyList<ICapabilityProvider> _providers;
    private readonly CapabilityRoutingPolicy _policy;

    public CapabilityRouter(IEnumerable<ICapabilityProvider> providers, CapabilityRoutingPolicy? policy = null)
    {
        _providers = providers.ToList();
        _policy = policy ?? CapabilityRoutingPolicy.Default;
    }

    public ICapabilityProvider Select(CapabilityRequest request)
    {
        return SelectRoute(request).Provider;
    }

    public CapabilityRouteResult SelectRoute(CapabilityRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Capability))
        {
            throw new ArgumentException("Capability must be provided.", nameof(request));
        }

        var candidates = _providers
            .Select((provider, index) => Score(provider, index, request))
            .ToArray();

        var eligible = candidates
            .Where(candidate => candidate.IsEligible)
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.ProviderOrder)
            .ToArray();

        var best = eligible.FirstOrDefault();
        if (best is null)
        {
            var reason = candidates.Length == 0
                ? "No providers are registered."
                : string.Join("; ", candidates.Select(candidate => $"{candidate.ProviderId}: {candidate.Reason}"));

            throw new InvalidOperationException($"No available provider supports capability '{request.Capability}'. {reason}");
        }

        var provider = _providers[best.ProviderOrder];
        var confidence = CalculateConfidence(best, eligible);
        if (confidence < _policy.MinimumConfidence)
        {
            throw new InvalidOperationException($"Best provider '{best.ProviderId}' for capability '{request.Capability}' did not meet the minimum confidence threshold.");
        }

        return new CapabilityRouteResult(
            provider,
            confidence,
            candidates,
            $"Selected {best.ProviderId} for {request.Capability} with confidence {confidence:0.00}.");
    }

    private CapabilityRouteCandidate Score(ICapabilityProvider provider, int providerOrder, CapabilityRequest request)
    {
        var profile = _policy.ProfileFor(provider.ProviderId);
        var supportsCapability = provider.Capabilities.Any(capability => string.Equals(capability, request.Capability, StringComparison.OrdinalIgnoreCase));
        var providerConstraint = Constraint(request, "provider");
        var preferredProvider = Constraint(request, "preferredProvider");
        var exactProviderMismatch = !string.IsNullOrWhiteSpace(providerConstraint) &&
            !string.Equals(provider.ProviderId, providerConstraint, StringComparison.OrdinalIgnoreCase);
        var experimentalBlocked = profile.Experimental && !_policy.AllowExperimentalProviders;
        var eligible = provider.IsAvailable && supportsCapability && !exactProviderMismatch && !experimentalBlocked;

        var score = 0.0;
        var reason = "eligible";

        if (!provider.IsAvailable)
        {
            reason = "provider unavailable";
        }
        else if (!supportsCapability)
        {
            reason = "capability not supported";
        }
        else if (exactProviderMismatch)
        {
            reason = $"provider constraint requested '{providerConstraint}'";
        }
        else if (experimentalBlocked)
        {
            reason = "experimental provider blocked by policy";
        }
        else
        {
            score = 100.0 + profile.Priority + (profile.NormalizedReliability * 20.0);
            score += ConstraintBonus(provider.ProviderId, profile.Kind, request);

            if (!string.IsNullOrWhiteSpace(preferredProvider) && string.Equals(provider.ProviderId, preferredProvider, StringComparison.OrdinalIgnoreCase))
            {
                score += 40.0;
            }

            if (!string.IsNullOrWhiteSpace(providerConstraint) && string.Equals(provider.ProviderId, providerConstraint, StringComparison.OrdinalIgnoreCase))
            {
                score += 80.0;
            }

            reason = $"eligible score {score:0.00}";
        }

        return new CapabilityRouteCandidate(
            provider.ProviderId,
            profile.Kind,
            request.Capability,
            provider.IsAvailable,
            supportsCapability,
            eligible,
            providerOrder,
            profile.Priority,
            profile.NormalizedReliability,
            score,
            reason);
    }

    private static double ConstraintBonus(string providerId, CapabilityProviderKind kind, CapabilityRequest request)
    {
        var id = providerId.ToLowerInvariant();
        var mode = Constraint(request, "mode")?.ToLowerInvariant();
        var workload = Constraint(request, "workload")?.ToLowerInvariant();
        var requiresExistingSession = IsTrue(Constraint(request, "requiresExistingSession"));
        var requiresIsolation = IsTrue(Constraint(request, "requiresIsolation")) || IsTrue(Constraint(request, "isolation"));
        var score = 0.0;

        if (requiresExistingSession)
        {
            score += ContainsAny(id, "chrome", "cdp", "edge") ? 35.0 : -20.0;
        }

        if (requiresIsolation)
        {
            score += id.Contains("playwright", StringComparison.Ordinal) ? 35.0 : -10.0;
        }

        if (string.Equals(mode, "local", StringComparison.Ordinal))
        {
            score += ContainsAny(id, "chrome", "cdp", "edge", "browser.synthetic") ? 15.0 : 0.0;
        }

        if (string.Equals(mode, "cloud", StringComparison.Ordinal))
        {
            score += ContainsAny(id, "playwright", "firecrawl") ? 20.0 : -5.0;
        }

        if (workload is "crawl" or "scrape" or "extract" or "extraction")
        {
            score += kind == CapabilityProviderKind.Extraction || id.Contains("firecrawl", StringComparison.Ordinal) ? 35.0 : -10.0;
        }

        return score;
    }

    private static double CalculateConfidence(CapabilityRouteCandidate best, IReadOnlyList<CapabilityRouteCandidate> eligible)
    {
        if (eligible.Count <= 1)
        {
            return 0.95;
        }

        var second = eligible[1];
        var margin = Math.Max(0.0, best.Score - second.Score);
        return Math.Clamp(0.55 + (margin / 100.0), 0.10, 0.99);
    }

    private static string? Constraint(CapabilityRequest request, string key)
    {
        if (request.Constraints is null)
        {
            return null;
        }

        foreach (var pair in request.Constraints)
        {
            if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                return pair.Value;
            }
        }

        return null;
    }

    private static bool IsTrue(string? value) =>
        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "1", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsAny(string value, params string[] tokens) =>
        tokens.Any(token => value.Contains(token, StringComparison.Ordinal));
}
