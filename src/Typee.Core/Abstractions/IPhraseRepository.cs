using Typee.Core.Models;

namespace Typee.Core.Abstractions;

public interface IPhraseRepository
{
    Task<IReadOnlyList<Phrase>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Phrase?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<Phrase?> FindByTextAsync(string text, CancellationToken cancellationToken = default);
    Task<Phrase> InsertAsync(Phrase phrase, CancellationToken cancellationToken = default);
    Task UpdateAsync(Phrase phrase, CancellationToken cancellationToken = default);
    Task DeleteAsync(long id, CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
}
