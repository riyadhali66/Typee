using Typee.Core.Models;

namespace Typee.Core.Abstractions;

public interface ISnippetRepository
{
    Task<IReadOnlyList<Snippet>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Snippet> UpsertAsync(Snippet snippet, CancellationToken cancellationToken = default);
    Task DeleteAsync(long id, CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
}
