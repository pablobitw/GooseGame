using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;

namespace GameServer.Repositories
{
    public class FriendshipRepository : IDisposable
    {
        private readonly GameDatabase_Container _context;

        public FriendshipRepository()
        {
            _context = new GameDatabase_Container();
            _context.Configuration.LazyLoadingEnabled = false;
            _context.Configuration.ProxyCreationEnabled = false;
        }

        public async Task<Player> GetPlayerByUsernameAsync(string username)
        {
            return await _context.Players.FirstOrDefaultAsync(p => p.Username == username);
        }

        public Player GetPlayerById(int id)
        {
            return _context.Players.FirstOrDefault(p => p.IdPlayer == id);
        }

        public Friendship GetFriendship(int userId1, int userId2)
        {
            return _context.Friendships.FirstOrDefault(f =>
                ((f.PlayerIdPlayer == userId1 && f.Player1_IdPlayer == userId2) ||
                 (f.PlayerIdPlayer == userId2 && f.Player1_IdPlayer == userId1)));
        }

        public Friendship GetPendingRequest(int requesterId, int responderId)
        {
            return _context.Friendships.FirstOrDefault(f =>
                f.PlayerIdPlayer == requesterId &&
                f.Player1_IdPlayer == responderId &&
                f.FriendshipStatus == (int)FriendshipStatus.Pending);
        }

        public List<Friendship> GetAcceptedFriendships(int playerId)
        {
            var accepted = (int)FriendshipStatus.Accepted;
            return _context.Friendships
                .Where(f => (f.PlayerIdPlayer == playerId || f.Player1_IdPlayer == playerId) && f.FriendshipStatus == accepted)
                .ToList();
        }

        public List<Friendship> GetIncomingPendingRequests(int playerId)
        {
            var pending = (int)FriendshipStatus.Pending;
            return _context.Friendships
                .Where(f => f.Player1_IdPlayer == playerId && f.FriendshipStatus == pending)
                .ToList();
        }

        public void AddFriendship(Friendship friendship)
        {
            _context.Friendships.Add(friendship);
        }

        public void RemoveFriendship(Friendship friendship)
        {
            _context.Friendships.Remove(friendship);
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }

        public void Dispose()
        {
            _context?.Dispose();
        }
    }
}