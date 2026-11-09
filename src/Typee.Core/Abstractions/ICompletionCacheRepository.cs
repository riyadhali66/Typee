using Typee.Core.Models;

namespace Typee.Core.Abstractions;

public interface ICompletionCacheRepository
{
    Task<CompletionCacheEntry?> TryGetAsync(string contextHash, CancellationToken cancellationToken = default);
    Task UpsertAsync(CompletionCacheEntry entry, CancellationToken cancellationToken = default);
    Task DeleteExpiredAsync(TimeSpan ttl, DateTimeOffset utcNow, CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
}
