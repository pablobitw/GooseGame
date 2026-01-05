using GameServer.DTOs;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GameServer;

namespace GameServer.Repositories
{
    public interface IFriendshipRepository : IDisposable
    {
        void AddFriendship(Friendship friendship);

        List<Friendship> GetAcceptedFriendships(int playerId);
        Friendship GetFriendship(int userId1, int userId2);
        List<Friendship> GetIncomingPendingRequests(int playerId);
        List<Friendship> GetOutgoingPendingRequests(int playerId);
        Friendship GetPendingRequest(int requesterId, int responderId);

        Player GetPlayerById(int id);
        Task<Player> GetPlayerByUsernameAsync(string username);

        void RemoveFriendship(Friendship friendship);
        Task SaveChangesAsync();
    }
}
