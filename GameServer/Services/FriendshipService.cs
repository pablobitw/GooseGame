using System;
using System.Collections.Generic;
using System.Data.Entity.Core;
using System.Data.SqlClient;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;
using GameServer.Contracts;
using log4net;

namespace GameServer.Services
{
    [ServiceBehavior(ConcurrencyMode = ConcurrencyMode.Multiple, InstanceContextMode = InstanceContextMode.Single)]
    public class FriendshipService : IFriendshipService
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(FriendshipService));
        private static readonly Dictionary<string, IFriendshipServiceCallback> _connectedClients = new Dictionary<string, IFriendshipServiceCallback>();
        private static readonly object _locker = new object();

        public void Connect(string username)
        {
            try
            {
                var callback = OperationContext.Current.GetCallbackChannel<IFriendshipServiceCallback>();
                string key = username.ToLower(); 

                lock (_locker)
                {
                    if (!_connectedClients.ContainsKey(key))
                    {
                        _connectedClients.Add(key, callback);
                    }
                    else
                    {
                        _connectedClients[key] = callback;
                    }
                }
                NotifyFriendsOfStatusChange(username);
            }
            catch (Exception ex)
            {
                Log.Error($"Error registering client {username}", ex);
            }
        }

        public void Disconnect(string username)
        {
            string key = username.ToLower();
            lock (_locker)
            {
                if (_connectedClients.ContainsKey(key))
                {
                    _connectedClients.Remove(key);
                }
            }
            NotifyFriendsOfStatusChange(username);
        }

        public async Task<bool> SendFriendRequest(string senderUsername, string receiverUsername)
        {
            try
            {
                using (var context = new GameDatabase_Container())
                {
                    if (string.IsNullOrEmpty(senderUsername) || string.IsNullOrEmpty(receiverUsername)) return false;
                    if (senderUsername.Equals(receiverUsername)) return false;

                    var sender = context.Players.FirstOrDefault(p => p.Username == senderUsername);
                    var receiver = context.Players.FirstOrDefault(p => p.Username == receiverUsername);

                    if (sender == null || receiver == null) return false;

                    var existingRequest = context.Friendships.FirstOrDefault(f =>
                        ((f.PlayerIdPlayer == sender.IdPlayer && f.Player1_IdPlayer == receiver.IdPlayer) ||
                         (f.PlayerIdPlayer == receiver.IdPlayer && f.Player1_IdPlayer == sender.IdPlayer))
                        && (f.FriendshipStatus == (int)FriendshipStatus.Pending || f.FriendshipStatus == (int)FriendshipStatus.Accepted));

                    if (existingRequest != null) return false;

                    var newFriendship = new Friendship
                    {
                        PlayerIdPlayer = sender.IdPlayer,
                        Player1_IdPlayer = receiver.IdPlayer,
                        FriendshipStatus = (int)FriendshipStatus.Pending,
                        RequestDate = DateTime.Now
                    };

                    context.Friendships.Add(newFriendship);
                    await context.SaveChangesAsync();

                    NotifyUserRequestReceived(receiverUsername);

                    return true;
                }
            }
            catch (Exception ex) { Log.Error("Error", ex); return false; }
        }

        public async Task<bool> RespondToFriendRequest(string respondingUsername, string requesterUsername, bool isAccepted)
        {
            try
            {
                using (var context = new GameDatabase_Container())
                {
                    var responder = context.Players.FirstOrDefault(p => p.Username == respondingUsername);
                    var requester = context.Players.FirstOrDefault(p => p.Username == requesterUsername);

                    if (responder == null || requester == null) return false;

                    var friendship = context.Friendships.FirstOrDefault(f =>
                        f.PlayerIdPlayer == requester.IdPlayer &&
                        f.Player1_IdPlayer == responder.IdPlayer &&
                        f.FriendshipStatus == (int)FriendshipStatus.Pending);

                    if (friendship == null) return false;

                    if (isAccepted) friendship.FriendshipStatus = (int)FriendshipStatus.Accepted;
                    else context.Friendships.Remove(friendship);

                    await context.SaveChangesAsync();

                    NotifyUserListUpdated(requesterUsername);
                    NotifyUserListUpdated(respondingUsername);

                    return true;
                }
            }
            catch (Exception ex) { Log.Error("Error", ex); return false; }
        }

        public async Task<bool> RemoveFriend(string username, string friendUsername)
        {
            try
            {
                using (var context = new GameDatabase_Container())
                {
                    var user1 = context.Players.FirstOrDefault(p => p.Username == username);
                    var user2 = context.Players.FirstOrDefault(p => p.Username == friendUsername);
                    if (user1 == null || user2 == null) return false;

                    var friendship = context.Friendships.FirstOrDefault(f =>
                        ((f.PlayerIdPlayer == user1.IdPlayer && f.Player1_IdPlayer == user2.IdPlayer) ||
                         (f.PlayerIdPlayer == user2.IdPlayer && f.Player1_IdPlayer == user1.IdPlayer))
                        && f.FriendshipStatus == (int)FriendshipStatus.Accepted);

                    if (friendship != null)
                    {
                        context.Friendships.Remove(friendship);
                        await context.SaveChangesAsync();

                        NotifyUserListUpdated(username);
                        NotifyUserListUpdated(friendUsername);
                        return true;
                    }
                    return false;
                }
            }
            catch (Exception ex) { Log.Error("Error", ex); return false; }
        }

        public async Task<List<FriendDto>> GetFriendList(string username)
        {
            try
            {
                using (var context = new GameDatabase_Container())
                {
                    var player = context.Players.FirstOrDefault(p => p.Username == username);
                    if (player == null) return new List<FriendDto>();

                    var accepted = (int)FriendshipStatus.Accepted;
                    var friendsQuery = from f in context.Friendships
                                       where (f.PlayerIdPlayer == player.IdPlayer || f.Player1_IdPlayer == player.IdPlayer)
                                             && f.FriendshipStatus == accepted
                                       select f;

                    var list = new List<FriendDto>();
                    foreach (var f in friendsQuery)
                    {
                        int fid = (f.PlayerIdPlayer == player.IdPlayer) ? f.Player1_IdPlayer : f.PlayerIdPlayer;
                        var friend = context.Players.FirstOrDefault(p => p.IdPlayer == fid);
                        if (friend != null)
                        {
                            bool isOnline = false;
                            lock (_locker) { isOnline = _connectedClients.ContainsKey(friend.Username.ToLower()); }

                            list.Add(new FriendDto { Username = friend.Username, AvatarPath = friend.Avatar, IsOnline = isOnline });
                        }
                    }
                    return list;
                }
            }
            catch (Exception) { return new List<FriendDto>(); }
        }

        public async Task<List<FriendDto>> GetPendingRequests(string username)
        {
            try
            {
                using (var context = new GameDatabase_Container())
                {
                    var player = context.Players.FirstOrDefault(p => p.Username == username);
                    if (player == null) return new List<FriendDto>();

                    var pending = (int)FriendshipStatus.Pending;
                    var requests = from f in context.Friendships
                                   where f.Player1_IdPlayer == player.IdPlayer && f.FriendshipStatus == pending
                                   select f;

                    var list = new List<FriendDto>();
                    foreach (var r in requests)
                    {
                        var sender = context.Players.FirstOrDefault(p => p.IdPlayer == r.PlayerIdPlayer);
                        if (sender != null) list.Add(new FriendDto { Username = sender.Username, AvatarPath = sender.Avatar });
                    }
                    return list;
                }
            }
            catch (Exception) { return new List<FriendDto>(); }
        }

        private void NotifyUserRequestReceived(string username)
        {
            string key = username.ToLower();
            lock (_locker)
            {
                if (_connectedClients.ContainsKey(key))
                {
                    try { _connectedClients[key].OnFriendRequestReceived(); }
                    catch (CommunicationException) { _connectedClients.Remove(key); }
                }
            }
        }

        private void NotifyUserListUpdated(string username)
        {
            string key = username.ToLower();
            lock (_locker)
            {
                if (_connectedClients.ContainsKey(key))
                {
                    try { _connectedClients[key].OnFriendListUpdated(); }
                    catch (CommunicationException) { _connectedClients.Remove(key); }
                }
            }
        }

        public void SendGameInvitation(string senderUsername, string targetUsername, string lobbyCode)
        {
            string key = targetUsername.ToLower();
            lock (_locker)
            {
                if (_connectedClients.ContainsKey(key))
                {
                    try
                    {
                        _connectedClients[key].OnGameInvitationReceived(senderUsername, lobbyCode);
                    }
                    catch (CommunicationException)
                    {
                        _connectedClients.Remove(key);
                    }
                }
            }
        }

        private void NotifyFriendsOfStatusChange(string username)
        {
            Task.Run(() =>
            {
                try
                {
                    List<string> friendsToNotify = new List<string>();
                    using (var context = new GameDatabase_Container())
                    {
                        var player = context.Players.FirstOrDefault(p => p.Username == username);
                        if (player != null)
                        {
                            var accepted = (int)FriendshipStatus.Accepted;
                            var friendships = context.Friendships.Where(f => (f.PlayerIdPlayer == player.IdPlayer || f.Player1_IdPlayer == player.IdPlayer) && f.FriendshipStatus == accepted).ToList();

                            foreach (var f in friendships)
                            {
                                int fid = (f.PlayerIdPlayer == player.IdPlayer) ? f.Player1_IdPlayer : f.PlayerIdPlayer;
                                var friend = context.Players.FirstOrDefault(p => p.IdPlayer == fid);
                                if (friend != null) friendsToNotify.Add(friend.Username);
                            }
                        }
                    }
                    foreach (var f in friendsToNotify) NotifyUserListUpdated(f);
                }
                catch (Exception ex) { Log.Error("Status notify error", ex); }
            });
        }
    }
}