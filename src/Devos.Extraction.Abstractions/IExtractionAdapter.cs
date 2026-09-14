using Devos.Capabilities.Abstractions;

namespace Devos.Extraction.Abstractions;

public interface IExtractionAdapter : ICapabilityProvider
{
    Task<ExtractionResult> ExtractAsync(ExtractionRequest request, CancellationToken cancellationToken = default);
}

public sealed record ExtractionRequest(string Url, string Mode, string? Schema = null);
public sealed record ExtractionResult(bool Success, string Provider, string? Content, string? Error = null);
