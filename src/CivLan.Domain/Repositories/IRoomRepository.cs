using CivLan.Domain.Entities;
using CivLan.Domain.ValueObjects;

namespace CivLan.Domain.Repositories;

public interface IRoomRepository
{
    Task<Room?> GetByCodeAsync(RoomCode code, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Room>> ListOpenRoomsAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(Room room, CancellationToken cancellationToken = default);
    Task DeleteAsync(RoomCode code, CancellationToken cancellationToken = default);
}
