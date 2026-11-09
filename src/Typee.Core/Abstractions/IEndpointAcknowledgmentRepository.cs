using Typee.Core.Models;

namespace Typee.Core.Abstractions;

public interface IEndpointAcknowledgmentRepository
{
    Task<bool> IsAcknowledgedAsync(string profileName, string host, CancellationToken cancellationToken = default);
    Task AcknowledgeAsync(string profileName, string host, DateTimeOffset acknowledgedUtc, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EndpointAcknowledgment>> GetAllAsync(CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
}
