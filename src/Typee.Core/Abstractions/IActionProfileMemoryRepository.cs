using Typee.Core.Models;

namespace Typee.Core.Abstractions;

public interface IActionProfileMemoryRepository
{
    Task<ActionProfileMemory?> GetAsync(string actionId, CancellationToken cancellationToken = default);
    Task UpsertAsync(string actionId, string profileName, DateTimeOffset updatedUtc, CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
}
