using Devos.Capabilities.Abstractions;
using Devos.Protocol;

namespace Devos.Browser.Abstractions;

public interface IBrowserAdapter : ICapabilityProvider
{
    Task<BrowserObservation> GetObservationAsync(CancellationToken cancellationToken = default);
    Task<BrowserActionResult> ExecuteAsync(BrowserAction action, CancellationToken cancellationToken = default);
    Task<string?> GetCurrentUrlAsync(CancellationToken cancellationToken = default);
}
