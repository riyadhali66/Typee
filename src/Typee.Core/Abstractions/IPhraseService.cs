using Typee.Core.Models;

namespace Typee.Core.Abstractions;

public interface IPhraseService
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    IReadOnlyList<Phrase> Match(string textBeforeCaret, int maxResults = 5);
    Task<IReadOnlyList<Phrase>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Phrase> AddManualAsync(string text, CancellationToken cancellationToken = default);
    Task UpdateAsync(Phrase phrase, CancellationToken cancellationToken = default);
    Task DeleteAsync(long id, CancellationToken cancellationToken = default);
    Task RecordAcceptAsync(string suggestionText, CancellationToken cancellationToken = default);
    Task ReloadAsync(CancellationToken cancellationToken = default);
    Task ImportAsync(IEnumerable<Phrase> phrases, CancellationToken cancellationToken = default);
}
