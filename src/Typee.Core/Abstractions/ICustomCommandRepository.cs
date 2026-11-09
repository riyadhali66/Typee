using Typee.Core.Models;

namespace Typee.Core.Abstractions;

public interface ICustomCommandRepository
{
    Task<IReadOnlyList<CustomCommand>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<CustomCommand> UpsertAsync(CustomCommand command, CancellationToken cancellationToken = default);
    Task DeleteAsync(long id, CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
}
