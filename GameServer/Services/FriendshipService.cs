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
                lock (_locker)
                {
                    if (!_connectedClients.ContainsKey(username))
                    {
                        _connectedClients.Add(username, callback);
                    }
                    else
                    {
                        _connectedClients[username] = callback;
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
            lock (_locker)
            {
                if (_connectedClients.ContainsKey(username))
                {
                    _connectedClients.Remove(username);
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
                    if (string.IsNullOrEmpty(senderUsername) || string.IsNullOrEmpty(receiverUsername))
                    {
                        return false;
                    }

                    if (senderUsername.Equals(receiverUsername))
                    {
                        return false;
                    }

                    var sender = context.Players.FirstOrDefault(p => p.Username == senderUsername);
                    var receiver = context.Players.FirstOrDefault(p => p.Username == receiverUsername);

                    if (sender == null || receiver == null)
                    {
                        Log.Warn($"Friend request failed. User not found: {senderUsername} or {receiverUsername}");
                        return false;
                    }

                    var existingRequest = context.Friendships.FirstOrDefault(f =>
                        ((f.PlayerIdPlayer == sender.IdPlayer && f.Player1_IdPlayer == receiver.IdPlayer) ||
                         (f.PlayerIdPlayer == receiver.IdPlayer && f.Player1_IdPlayer == sender.IdPlayer))
                        && (f.FriendshipStatus == (int)FriendshipStatus.Pending || f.FriendshipStatus == (int)FriendshipStatus.Accepted));

                    if (existingRequest != null)
                    {
                        Log.Warn($"Duplicate friend request between {senderUsername} and {receiverUsername}");
                        return false;
                    }

                    var newFriendship = new Friendship
                    {
                        PlayerIdPlayer = sender.IdPlayer,
                        Player1_IdPlayer = receiver.IdPlayer,
                        FriendshipStatus = (int)FriendshipStatus.Pending,
                        RequestDate = DateTime.Now
                    };

                    context.Friendships.Add(newFriendship);
                    await context.SaveChangesAsync();

                    Log.Info($"Friend request sent from {senderUsername} to {receiverUsername}");
                    NotifyUserRequestReceived(receiverUsername);

                    return true;
                }
            }
            catch (SqlException ex)
            {
                Log.Error("Database error in SendFriendRequest", ex);
                return false;
            }
            catch (EntityException ex)
            {
                Log.Error("Entity context error in SendFriendRequest", ex);
                return false;
            }
        }

        public async Task<bool> RespondToFriendRequest(string respondingUsername, string requesterUsername, bool isAccepted)
        {
            try
            {
                using (var context = new GameDatabase_Container())
                {
                    var responder = context.Players.FirstOrDefault(p => p.Username == respondingUsername);
                    var requester = context.Players.FirstOrDefault(p => p.Username == requesterUsername);

                    if (responder == null || requester == null)
                    {
                        return false;
                    }

                    var friendship = context.Friendships.FirstOrDefault(f =>
                        f.PlayerIdPlayer == requester.IdPlayer &&
                        f.Player1_IdPlayer == responder.IdPlayer &&
                        f.FriendshipStatus == (int)FriendshipStatus.Pending);

                    if (friendship == null)
                    {
                        Log.Warn($"Pending request not found between {requesterUsername} and {respondingUsername}");
                        return false;
                    }

                    if (isAccepted)
                    {
                        friendship.FriendshipStatus = (int)FriendshipStatus.Accepted;
                    }
                    else
                    {
                        context.Friendships.Remove(friendship);
                    }

                    await context.SaveChangesAsync();
                    Log.Info($"User {respondingUsername} responded {isAccepted} to request from {requesterUsername}");

                    NotifyUserListUpdated(requesterUsername);
                    NotifyUserListUpdated(respondingUsername);

                    return true;
                }
            }
            catch (SqlException ex)
            {
                Log.Error("Database error in RespondToFriendRequest", ex);
                return false;
            }
            catch (EntityException ex)
            {
                Log.Error("Entity context error in RespondToFriendRequest", ex);
                return false;
            }
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
                        Log.Info($"Amistad eliminada entre {username} y {friendUsername}");

                        NotifyUserListUpdated(username);
                        NotifyUserListUpdated(friendUsername);

                        return true;
                    }
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.Error("Error eliminando amigo", ex);
                return false;
            }
        }

        public async Task<List<FriendDto>> GetFriendList(string username)
        {
            try
            {
                using (var context = new GameDatabase_Container())
                {
                    var player = context.Players.FirstOrDefault(p => p.Username == username);
                    if (player == null)
                    {
                        return new List<FriendDto>();
                    }

                    var acceptedStatus = (int)FriendshipStatus.Accepted;

                    var friendsQuery = from f in context.Friendships
                                       where (f.PlayerIdPlayer == player.IdPlayer || f.Player1_IdPlayer == player.IdPlayer)
                                             && f.FriendshipStatus == acceptedStatus
                                       select f;

                    var friendsList = new List<FriendDto>();

                    foreach (var friendship in friendsQuery)
                    {
                        int friendId = (friendship.PlayerIdPlayer == player.IdPlayer)
                                        ? friendship.Player1_IdPlayer
                                        : friendship.PlayerIdPlayer;

                        var friendEntity = context.Players.FirstOrDefault(p => p.IdPlayer == friendId);
                        if (friendEntity != null)
                        {
                            bool isUserOnline = false;
                            lock (_locker)
                            {
                                isUserOnline = _connectedClients.ContainsKey(friendEntity.Username);
                            }

                            friendsList.Add(new FriendDto
                            {
                                Username = friendEntity.Username,
                                AvatarPath = friendEntity.Avatar,
                                IsOnline = isUserOnline
                            });
                        }
                    }

                    return friendsList;
                }
            }
            catch (SqlException ex)
            {
                Log.Error("Database error in GetFriendList", ex);
                return new List<FriendDto>();
            }
        }

        public async Task<List<FriendDto>> GetPendingRequests(string username)
        {
            try
            {
                using (var context = new GameDatabase_Container())
                {
                    var player = context.Players.FirstOrDefault(p => p.Username == username);
                    if (player == null)
                    {
                        return new List<FriendDto>();
                    }

                    var pendingStatus = (int)FriendshipStatus.Pending;

                    var requests = from f in context.Friendships
                                   where f.Player1_IdPlayer == player.IdPlayer
                                         && f.FriendshipStatus == pendingStatus
                                   select f;

                    var result = new List<FriendDto>();
                    foreach (var req in requests)
                    {
                        var sender = context.Players.FirstOrDefault(p => p.IdPlayer == req.PlayerIdPlayer);
                        if (sender != null)
                        {
                            result.Add(new FriendDto
                            {
                                Username = sender.Username,
                                AvatarPath = sender.Avatar
                            });
                        }
                    }
                    return result;
                }
            }
            catch (SqlException ex)
            {
                Log.Error("Database error in GetPendingRequests", ex);
                return new List<FriendDto>();
            }
        }

        private void NotifyUserRequestReceived(string username)
        {
            lock (_locker)
            {
                if (_connectedClients.ContainsKey(username))
                {
                    try
                    {
                        _connectedClients[username].OnFriendRequestReceived();
                    }
                    catch (CommunicationException)
                    {
                        _connectedClients.Remove(username);
                    }
                }
            }
        }

        private void NotifyUserListUpdated(string username)
        {
            lock (_locker)
            {
                if (_connectedClients.ContainsKey(username))
                {
                    try
                    {
                        _connectedClients[username].OnFriendListUpdated();
                    }
                    catch (CommunicationException)
                    {
                        _connectedClients.Remove(username);
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

                            var friendships = context.Friendships
                                .Where(f => (f.PlayerIdPlayer == player.IdPlayer || f.Player1_IdPlayer == player.IdPlayer)
                                            && f.FriendshipStatus == accepted)
                                .ToList();

                            foreach (var f in friendships)
                            {
                                int friendId = (f.PlayerIdPlayer == player.IdPlayer) ? f.Player1_IdPlayer : f.PlayerIdPlayer;
                                var friendUser = context.Players.FirstOrDefault(p => p.IdPlayer == friendId);
                                if (friendUser != null)
                                {
                                    friendsToNotify.Add(friendUser.Username);
                                }
                            }
                        }
                    }

                    foreach (var friendUsername in friendsToNotify)
                    {
                        NotifyUserListUpdated(friendUsername);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"Error notifying friends of status change for {username}", ex);
                }
            });
        }
    }
}