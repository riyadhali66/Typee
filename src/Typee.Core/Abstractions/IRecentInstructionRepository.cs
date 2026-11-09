using Typee.Core.Models;

namespace Typee.Core.Abstractions;

public interface IRecentInstructionRepository
{
    Task<IReadOnlyList<RecentInstruction>> GetRecentAsync(int limit = 10, CancellationToken cancellationToken = default);
    Task AddAsync(string text, DateTimeOffset usedUtc, CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
}
