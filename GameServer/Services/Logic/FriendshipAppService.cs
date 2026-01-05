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

        private readonly IFriendshipRepository _repository;

        public FriendshipAppService(IFriendshipRepository repository)
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

        public async Task<FriendRequestResult> SendFriendRequestAsync(string senderUsername, string receiverUsername)
        {
            try
            {
                var validationResult = ValidateFriendRequest(senderUsername, receiverUsername);
                if (validationResult != FriendRequestResult.Success)
                {
                    return validationResult;
                }

                var sender = await _repository.GetPlayerByUsernameAsync(senderUsername);
                var receiver = await _repository.GetPlayerByUsernameAsync(receiverUsername);

                if (sender == null || receiver == null)
                {
                    return FriendRequestResult.TargetNotFound;
                }

                if (sender.IsGuest || receiver.IsGuest)
                {
                    Log.WarnFormat("Intento de amistad inválido con invitado: {0} -> {1}", senderUsername, receiverUsername);
                    return FriendRequestResult.GuestRestriction;
                }

                var existing = _repository.GetFriendship(sender.IdPlayer, receiver.IdPlayer);

                if (existing != null)
                {
                    return await HandleExistingFriendship(existing, sender.IdPlayer, senderUsername, receiverUsername);
                }

                return await CreateNewFriendRequest(sender.IdPlayer, receiver.IdPlayer, senderUsername, receiverUsername);
            }
            catch (SqlException ex)
            {
                Log.Error("Error SQL enviando solicitud de amistad.", ex);
                return FriendRequestResult.Error;
            }
            catch (DbUpdateException ex)
            {
                Log.Error("Error DB enviando solicitud de amistad.", ex);
                return FriendRequestResult.Error;
            }
            catch (TimeoutException ex)
            {
                Log.Error("Timeout enviando solicitud de amistad.", ex);
                return FriendRequestResult.Error;
            }
        }

        private static FriendRequestResult ValidateFriendRequest(string senderUsername, string receiverUsername)
        {
            if (string.IsNullOrEmpty(senderUsername) ||
                string.IsNullOrEmpty(receiverUsername) ||
                senderUsername.Equals(receiverUsername, StringComparison.OrdinalIgnoreCase))
            {
                return FriendRequestResult.Error;
            }

            return FriendRequestResult.Success;
        }

        private async Task<FriendRequestResult> HandleExistingFriendship(Friendship existing, int senderId, string senderUsername, string receiverUsername)
        {
            if (existing.FriendshipStatus == (int)FriendshipStatus.Accepted)
            {
                return FriendRequestResult.AlreadyFriends;
            }

            if (existing.FriendshipStatus == (int)FriendshipStatus.Pending)
            {
                if (existing.PlayerIdPlayer == senderId)
                {
                    return FriendRequestResult.Pending;
                }

                existing.FriendshipStatus = (int)FriendshipStatus.Accepted;
                await _repository.SaveChangesAsync();

                NotifyUserListUpdated(senderUsername);
                NotifyUserListUpdated(receiverUsername);

                return FriendRequestResult.Success;
            }

            return FriendRequestResult.Error;
        }

        private async Task<FriendRequestResult> CreateNewFriendRequest(int senderId, int receiverId, string senderUsername, string receiverUsername)
        {
            var newFriendship = new Friendship
            {
                PlayerIdPlayer = senderId,
                Player1_IdPlayer = receiverId,
                FriendshipStatus = (int)FriendshipStatus.Pending,
                RequestDate = DateTime.Now
            };

            _repository.AddFriendship(newFriendship);
            await _repository.SaveChangesAsync();

            NotifyUserRequestReceived(receiverUsername);
            NotifyUserPopUp(receiverUsername, senderUsername);

            return FriendRequestResult.Success;
        }

        public async Task<List<FriendDto>> GetSentRequestsAsync(string username)
        {
            var resultList = new List<FriendDto>();
            try
            {
                var player = await _repository.GetPlayerByUsernameAsync(username);
                if (player == null || player.IsGuest)
                {
                    return resultList;
                }

                var requests = _repository.GetOutgoingPendingRequests(player.IdPlayer);

                foreach (var r in requests)
                {
                    var receiver = _repository.GetPlayerById(r.Player1_IdPlayer);
                    if (receiver != null)
                    {
                        resultList.Add(new FriendDto
                        {
                            Username = receiver.Username,
                            AvatarPath = receiver.Avatar
                        });
                    }
                }
            }
            catch (SqlException ex)
            {
                Log.Error("Error SQL obteniendo solicitudes enviadas", ex);
            }
            catch (TimeoutException ex)
            {
                Log.Error("Timeout obteniendo solicitudes enviadas", ex);
            }

            return resultList;
        }

        public async Task<bool> RespondToFriendRequestAsync(RespondRequestDto request)
        {
            bool success = false;

            try
            {
                var responder = await _repository.GetPlayerByUsernameAsync(request.RespondingUsername);
                var requester = await _repository.GetPlayerByUsernameAsync(request.RequesterUsername);

                if (responder == null || requester == null)
                {
                    return success;
                }

                if (responder.IsGuest)
                {
                    return success;
                }

                var friendship = _repository.GetPendingRequest(requester.IdPlayer, responder.IdPlayer);

                if (friendship == null)
                {
                    return success;
                }

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
            catch (SqlException ex)
            {
                Log.Error("Error SQL respondiendo solicitud.", ex);
            }
            catch (DbUpdateException ex)
            {
                Log.Error("Error DB respondiendo solicitud.", ex);
            }
            catch (TimeoutException ex)
            {
                Log.Error("Timeout respondiendo solicitud.", ex);
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

                if (user1 == null || user2 == null)
                {
                    return success;
                }

                if (user1.IsGuest)
                {
                    return success;
                }

                var friendship = _repository.GetFriendship(user1.IdPlayer, user2.IdPlayer);

                if (friendship == null)
                {
                    return success;
                }

                _repository.RemoveFriendship(friendship);
                await _repository.SaveChangesAsync();

                NotifyUserListUpdated(username);
                NotifyUserListUpdated(friendUsername);

                success = true;
            }
            catch (SqlException ex)
            {
                Log.Error("Error SQL eliminando amigo.", ex);
            }
            catch (DbUpdateException ex)
            {
                Log.Error("Error DB eliminando amigo.", ex);
            }
            catch (TimeoutException ex)
            {
                Log.Error("Timeout eliminando amigo.", ex);
            }

            return success;
        }

        public async Task<List<FriendDto>> GetFriendListAsync(string username)
        {
            var resultList = new List<FriendDto>();

            try
            {
                var player = await _repository.GetPlayerByUsernameAsync(username);

                if (player == null || player.IsGuest)
                {
                    return resultList;
                }

                var friendships = _repository.GetAcceptedFriendships(player.IdPlayer);

                foreach (var f in friendships)
                {
                    int friendId = (f.PlayerIdPlayer == player.IdPlayer) ? f.Player1_IdPlayer : f.PlayerIdPlayer;
                    var friend = _repository.GetPlayerById(friendId);

                    if (friend != null)
                    {
                        bool isOnline;
                        lock (_locker)
                        {
                            isOnline = _connectedClients.ContainsKey(friend.Username.ToLower());
                        }

                        resultList.Add(new FriendDto
                        {
                            Username = friend.Username,
                            AvatarPath = friend.Avatar,
                            IsOnline = isOnline
                        });
                    }
                }
            }
            catch (SqlException ex)
            {
                Log.Error("Error SQL obteniendo lista de amigos.", ex);
            }
            catch (TimeoutException ex)
            {
                Log.Error("Timeout obteniendo lista de amigos.", ex);
            }

            return resultList;
        }

        public async Task<List<FriendDto>> GetPendingRequestsAsync(string username)
        {
            var resultList = new List<FriendDto>();

            try
            {
                var player = await _repository.GetPlayerByUsernameAsync(username);

                if (player == null || player.IsGuest)
                {
                    return resultList;
                }

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
            catch (SqlException ex)
            {
                Log.Error("Error SQL obteniendo solicitudes pendientes.", ex);
            }
            catch (TimeoutException ex)
            {
                Log.Error("Timeout obteniendo solicitudes pendientes.", ex);
            }

            return resultList;
        }

        public static void SendGameInvitation(GameInvitationDto invitation)
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

        private static void NotifyUserRequestReceived(string username)
        {
            string key = username.ToLower();
            lock (_locker)
            {
                if (_connectedClients.ContainsKey(key))
                {
                    try
                    {
                        _connectedClients[key].OnFriendRequestReceived();
                    }
                    catch (CommunicationException)
                    {
                        _connectedClients.Remove(key);
                    }
                }
            }
        }

        private static void NotifyUserListUpdated(string username)
        {
            string key = username.ToLower();
            lock (_locker)
            {
                if (_connectedClients.ContainsKey(key))
                {
                    try
                    {
                        _connectedClients[key].OnFriendListUpdated();
                    }
                    catch (CommunicationException)
                    {
                        _connectedClients.Remove(key);
                    }
                }
            }
        }

        private static void NotifyUserPopUp(string targetUser, string senderUser)
        {
            string key = targetUser.ToLower();
            lock (_locker)
            {
                if (_connectedClients.ContainsKey(key))
                {
                    try
                    {
                        _connectedClients[key].OnFriendRequestPopUp(senderUser);
                    }
                    catch (CommunicationException)
                    {
                        _connectedClients.Remove(key);
                    }
                }
            }
        }

        private static void NotifyFriendsOfStatusChange(string username)
        {
            Task.Run(async () =>
            {
                try
                {
                    using (var repo = new FriendshipRepository())
                    {
                        var player = await repo.GetPlayerByUsernameAsync(username);
                        if (player != null && !player.IsGuest)
                        {
                            await NotifyFriendsAsync(player, repo);
                        }
                    }
                }
                catch (SqlException ex)
                {
                    Log.Error("Error SQL notificando estado de amigos", ex);
                }
                catch (TimeoutException ex)
                {
                    Log.Error("Timeout notificando estado de amigos", ex);
                }
            });
        }

        private static async Task NotifyFriendsAsync(Player player, FriendshipRepository repo)
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
            await Task.CompletedTask;
        }
    }
}