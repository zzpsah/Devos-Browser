using Devos.Capabilities.Abstractions;

namespace Devos.Capabilities.Router;

public sealed class CapabilityRouter
{
    private readonly IReadOnlyList<ICapabilityProvider> _providers;

    public CapabilityRouter(IEnumerable<ICapabilityProvider> providers)
    {
        _providers = providers.ToList();
    }

    public ICapabilityProvider Select(CapabilityRequest request)
    {
        return _providers.FirstOrDefault(provider => provider.IsAvailable && provider.Capabilities.Contains(request.Capability))
            ?? throw new InvalidOperationException($"No available provider supports capability '{request.Capability}'.");
    }
}
