using Typee.Core.Models;

namespace Typee.Core.Abstractions;

public interface ICaretMethodMemoryRepository
{
    Task<CaretDetectionMethod?> TryGetAsync(string processName, CancellationToken cancellationToken = default);
    CaretDetectionMethod? TryGet(string processName);
    Task UpsertAsync(string processName, CaretDetectionMethod method, DateTimeOffset updatedUtc, CancellationToken cancellationToken = default);
    void Upsert(string processName, CaretDetectionMethod method, DateTimeOffset updatedUtc);
    Task ClearAsync(CancellationToken cancellationToken = default);
}
