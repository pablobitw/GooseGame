using GameServer.DTOs.Friendship;
using GameServer.Interfaces;
using GameServer.Repositories;
using log4net;
using System;
using System.Collections.Generic;
using System.Data.Entity.Infrastructure;
using System.Data.SqlClient;
using System.ServiceModel;
using System.Threading.Tasks;

namespace GameServer.Services.Logic
{
    public class FriendshipAppService
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(FriendshipAppService));

        private static readonly Dictionary<string, IFriendshipServiceCallback> _connectedClients = new Dictionary<string, IFriendshipServiceCallback>();
        private static readonly object _locker = new object();

        private readonly FriendshipRepository _repository;

        public FriendshipAppService(FriendshipRepository repository)
        {
            _repository = repository;
        }

        public void ConnectUser(string username, IFriendshipServiceCallback callback)
        {
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

        public void DisconnectUser(string username)
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

        public async Task<bool> SendFriendRequestAsync(string senderUsername, string receiverUsername)
        {
            bool success = false;
            try
            {
                if (!string.IsNullOrEmpty(senderUsername) &&
                    !string.IsNullOrEmpty(receiverUsername) &&
                    !senderUsername.Equals(receiverUsername))
                {
                    var sender = await _repository.GetPlayerByUsernameAsync(senderUsername);
                    var receiver = await _repository.GetPlayerByUsernameAsync(receiverUsername);

                    if (sender != null && receiver != null)
                    {
                        var existing = _repository.GetFriendship(sender.IdPlayer, receiver.IdPlayer);
                        if (existing == null) 
                        {
                            var newFriendship = new Friendship
                            {
                                PlayerIdPlayer = sender.IdPlayer,
                                Player1_IdPlayer = receiver.IdPlayer,
                                FriendshipStatus = (int)FriendshipStatus.Pending,
                                RequestDate = DateTime.Now
                            };

                            _repository.AddFriendship(newFriendship);
                            await _repository.SaveChangesAsync();

                            NotifyUserRequestReceived(receiverUsername);
                            success = true;
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                Log.Error("Error SQL enviando solicitud de amistad.", ex);
            }
            catch (DbUpdateException ex)
            {
                Log.Error("Error DB enviando solicitud de amistad.", ex);
            }

            return success;
        }

        public async Task<bool> RespondToFriendRequestAsync(RespondRequestDto request)
        {
            bool success = false;
            try
            {
                var responder = await _repository.GetPlayerByUsernameAsync(request.RespondingUsername);
                var requester = await _repository.GetPlayerByUsernameAsync(request.RequesterUsername);

                if (responder != null && requester != null)
                {
                    var friendship = _repository.GetPendingRequest(requester.IdPlayer, responder.IdPlayer);

                    if (friendship != null)
                    {
                        if (request.IsAccepted)
                        {
                            friendship.FriendshipStatus = (int)FriendshipStatus.Accepted;
                        }
                        else
                        {
                            _repository.RemoveFriendship(friendship);
                        }

                        await _repository.SaveChangesAsync();

                        NotifyUserListUpdated(request.RequesterUsername);
                        NotifyUserListUpdated(request.RespondingUsername);
                        success = true;
                    }
                }
            }
            catch (SqlException ex)
            {
                Log.Error("Error SQL respondiendo solicitud.", ex);
            }
            catch (DbUpdateException ex)
            {
                Log.Error("Error DB respondiendo solicitud.", ex);
            }

            return success;
        }

        public async Task<bool> RemoveFriendAsync(string username, string friendUsername)
        {
            bool success = false;
            try
            {
                var user1 = await _repository.GetPlayerByUsernameAsync(username);
                var user2 = await _repository.GetPlayerByUsernameAsync(friendUsername);

                if (user1 != null && user2 != null)
                {
                    var friendship = _repository.GetFriendship(user1.IdPlayer, user2.IdPlayer);

                    if (friendship != null && friendship.FriendshipStatus == (int)FriendshipStatus.Accepted)
                    {
                        _repository.RemoveFriendship(friendship);
                        await _repository.SaveChangesAsync();

                        NotifyUserListUpdated(username);
                        NotifyUserListUpdated(friendUsername);
                        success = true;
                    }
                }
            }
            catch (SqlException ex)
            {
                Log.Error("Error SQL eliminando amigo.", ex);
            }
            catch (DbUpdateException ex)
            {
                Log.Error("Error DB eliminando amigo.", ex);
            }

            return success;
        }

        public async Task<List<FriendDto>> GetFriendListAsync(string username)
        {
            var resultList = new List<FriendDto>();
            try
            {
                var player = await _repository.GetPlayerByUsernameAsync(username);
                if (player != null)
                {
                    var friendships = _repository.GetAcceptedFriendships(player.IdPlayer);

                    foreach (var f in friendships)
                    {
                        int friendId = (f.PlayerIdPlayer == player.IdPlayer) ? f.Player1_IdPlayer : f.PlayerIdPlayer;
                        var friend = _repository.GetPlayerById(friendId);

                        if (friend != null)
                        {
                            bool isOnline;
                            lock (_locker) { isOnline = _connectedClients.ContainsKey(friend.Username.ToLower()); }

                            resultList.Add(new FriendDto
                            {
                                Username = friend.Username,
                                AvatarPath = friend.Avatar,
                                IsOnline = isOnline
                            });
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                Log.Error("Error SQL obteniendo lista de amigos.", ex);
            }
            return resultList;
        }

        public async Task<List<FriendDto>> GetPendingRequestsAsync(string username)
        {
            var resultList = new List<FriendDto>();
            try
            {
                var player = await _repository.GetPlayerByUsernameAsync(username);
                if (player != null)
                {
                    var requests = _repository.GetIncomingPendingRequests(player.IdPlayer);

                    foreach (var r in requests)
                    {
                        var sender = _repository.GetPlayerById(r.PlayerIdPlayer);
                        if (sender != null)
                        {
                            resultList.Add(new FriendDto
                            {
                                Username = sender.Username,
                                AvatarPath = sender.Avatar
                            });
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                Log.Error("Error SQL obteniendo solicitudes pendientes.", ex);
            }
            return resultList;
        }

        public void SendGameInvitation(GameInvitationDto invitation)
        {
            string key = invitation.TargetUsername.ToLower();
            lock (_locker)
            {
                if (_connectedClients.ContainsKey(key))
                {
                    try
                    {
                        _connectedClients[key].OnGameInvitationReceived(invitation.SenderUsername, invitation.LobbyCode);
                    }
                    catch (CommunicationException)
                    {
                        _connectedClients.Remove(key);
                    }
                }
            }
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

        private void NotifyFriendsOfStatusChange(string username)
        {
            Task.Run(async () =>
            {
                try
                {
                    using (var repo = new FriendshipRepository())
                    {
                        var player = await repo.GetPlayerByUsernameAsync(username);
                        if (player != null)
                        {
                            var friendships = repo.GetAcceptedFriendships(player.IdPlayer);
                            foreach (var f in friendships)
                            {
                                int fid = (f.PlayerIdPlayer == player.IdPlayer) ? f.Player1_IdPlayer : f.PlayerIdPlayer;
                                var friend = repo.GetPlayerById(fid);
                                if (friend != null)
                                {
                                    NotifyUserListUpdated(friend.Username);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"Error notificando estado de {username}", ex);
                }
            });
        }
    }
}