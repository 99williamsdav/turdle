namespace Turdle.Persistence;

public interface IRoomStateRepository
{
    Task<RoomStateSnapshot?> Get(string roomCode);
    Task Upsert(RoomStateSnapshot snapshot);
}
