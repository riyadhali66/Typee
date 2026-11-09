using Typee.Core.Models;

namespace Typee.Core.Abstractions;

public interface IUserDictionaryRepository
{
    Task<IReadOnlyList<DictionaryWord>> GetAllAsync(CancellationToken cancellationToken = default);
    Task UpsertAsync(string word, DateTimeOffset addedUtc, CancellationToken cancellationToken = default);
    Task DeleteAsync(string word, CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
}
