using System.Collections.Generic;
using System.Threading.Tasks;

namespace GameServer.Repositories
{
    public interface IFriendshipRepository
    {
        void AddFriendship(Friendship friendship);
        void Dispose();
        List<Friendship> GetAcceptedFriendships(int playerId);
        Friendship GetFriendship(int userId1, int userId2);
        List<Friendship> GetIncomingPendingRequests(int playerId);
        Friendship GetPendingRequest(int requesterId, int responderId);
        Player GetPlayerById(int id);
        Task<Player> GetPlayerByUsernameAsync(string username);
        void RemoveFriendship(Friendship friendship);
        Task SaveChangesAsync();
    }
}