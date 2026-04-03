namespace Turdle.Persistence;

public interface IRoomStateRepository
{
    Task<RoomStateSnapshot?> Get(string roomCode);
    Task<IReadOnlyCollection<RoomStateSnapshot>> GetBuffered();
    Task Upsert(RoomStateSnapshot snapshot);
}
