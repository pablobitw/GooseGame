using GameServer.DTOs.Friendship;
using GameServer.Faults;
using GameServer.Helpers;
using GameServer.Interfaces;
using GameServer.Models;
using GameServer.Repositories;
using GameServer.Repositories.Interfaces;
using GameServer.Services.Common;
using log4net;
using System;
using System.Collections.Generic;
using System.Data.Entity.Core;
using System.Data.SqlClient;
using System.ServiceModel;
using System.Threading.Tasks;

namespace GameServer.Services.Logic
{
    public class FriendshipAppService
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(FriendshipAppService));

        private readonly IFriendshipRepository _repository;
        private readonly IFriendshipConnectionManager _connectionManager;
        private readonly IClientCallbackProvider _callbackProvider;
        private readonly IFriendshipRepositoryFactory _repoFactory;

        public FriendshipAppService(
            IFriendshipRepository repository,
            IFriendshipConnectionManager connectionManager = null,
            IClientCallbackProvider callbackProvider = null,
            IFriendshipRepositoryFactory repoFactory = null)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _connectionManager = connectionManager ?? new FriendshipConnectionManager();
            _callbackProvider = callbackProvider ?? new ClientCallbackProvider();
            _repoFactory = repoFactory ?? new FriendshipRepositoryFactory();
        }

        public void Connect(string username)
        {
            if (string.IsNullOrWhiteSpace(username)) return;

            try
            {
                var callback = _callbackProvider.GetCallback();
                if (callback != null)
                {
                    _connectionManager.AddClient(username, callback);
                    NotifyFriendsOfStatusChange(username);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error en Connect para {username}", ex);
            }
        }

        public void Disconnect(string username)
        {
            if (string.IsNullOrWhiteSpace(username)) return;

            try
            {
                _connectionManager.RemoveClient(username);
                NotifyFriendsOfStatusChange(username);
            }
            catch (Exception ex)
            {
                Log.Error($"Error en Disconnect para {username}", ex);
            }
        }

        public async Task<FriendRequestResult> SendFriendRequest(string senderUsername, string receiverUsername)
        {
            try
            {
                if (string.IsNullOrEmpty(senderUsername) || string.IsNullOrEmpty(receiverUsername) ||
                    senderUsername.Equals(receiverUsername, StringComparison.OrdinalIgnoreCase))
                {
                    return FriendRequestResult.Error;
                }

                var sender = await _repository.GetPlayerByUsernameAsync(senderUsername);
                var receiver = await _repository.GetPlayerByUsernameAsync(receiverUsername);

                if (sender == null || receiver == null) return FriendRequestResult.TargetNotFound;
                if (sender.IsGuest || receiver.IsGuest) return FriendRequestResult.GuestRestriction;

                var existing = _repository.GetFriendship(sender.IdPlayer, receiver.IdPlayer);

                if (existing != null)
                {
                    if (existing.FriendshipStatus == (int)FriendshipStatus.Accepted)
                    {
                        return FriendRequestResult.AlreadyFriends;
                    }

                    if (existing.PlayerIdPlayer == receiver.IdPlayer)
                    {
                        existing.FriendshipStatus = (int)FriendshipStatus.Accepted;
                        await _repository.SaveChangesAsync();

                        _ = Task.Run(() => NotifyUserListUpdated(senderUsername));
                        _ = Task.Run(() => NotifyUserListUpdated(receiverUsername));

                        return FriendRequestResult.MutualAccepted;
                    }

                    return FriendRequestResult.Pending;
                }

                var newFriendship = new Friendship
                {
                    PlayerIdPlayer = sender.IdPlayer,
                    Player1_IdPlayer = receiver.IdPlayer,
                    FriendshipStatus = (int)FriendshipStatus.Pending,
                    RequestDate = DateTime.Now
                };

                _repository.AddFriendship(newFriendship);
                await _repository.SaveChangesAsync();

                _ = Task.Run(() => NotifyUserRequestReceived(receiverUsername));
                _ = Task.Run(() => NotifyUserPopUp(receiverUsername, senderUsername));

                return FriendRequestResult.Success;
            }
            catch (Exception ex)
            {
                Log.Error("Error crítico en SendFriendRequest", ex);
                throw ExceptionManager.Map(ex);
            }
        }

        public async Task<FriendRequestResult> RespondToFriendRequest(RespondRequestDto request)
        {
            try
            {
                var responder = await _repository.GetPlayerByUsernameAsync(request.RespondingUsername);
                var requester = await _repository.GetPlayerByUsernameAsync(request.RequesterUsername);

                if (responder == null || requester == null || responder.IsGuest)
                {
                    return FriendRequestResult.TargetNotFound;
                }

                var friendship = _repository.GetPendingRequest(requester.IdPlayer, responder.IdPlayer);

                if (friendship == null) return FriendRequestResult.TargetNotFound;

                if (request.IsAccepted)
                {
                    friendship.FriendshipStatus = (int)FriendshipStatus.Accepted;
                }
                else
                {
                    _repository.RemoveFriendship(friendship);
                }

                await _repository.SaveChangesAsync();

                if (request.IsAccepted)
                {
                    _ = Task.Run(() => NotifyUserListUpdated(request.RequesterUsername));
                    _ = Task.Run(() => NotifyUserListUpdated(request.RespondingUsername));
                }

                return FriendRequestResult.Success;
            }
            catch (Exception ex)
            {
                Log.Error("Error crítico en RespondToFriendRequest", ex);
                throw ExceptionManager.Map(ex);
            }
        }

        public async Task<FriendRequestResult> RemoveFriend(string username, string friendUsername)
        {
            try
            {
                var user1 = await _repository.GetPlayerByUsernameAsync(username);
                var user2 = await _repository.GetPlayerByUsernameAsync(friendUsername);

                if (user1 == null || user2 == null || user1.IsGuest)
                {
                    return FriendRequestResult.TargetNotFound;
                }

                var friendship = _repository.GetFriendship(user1.IdPlayer, user2.IdPlayer);

                if (friendship != null)
                {
                    _repository.RemoveFriendship(friendship);
                    await _repository.SaveChangesAsync();

                    _ = Task.Run(() => NotifyUserListUpdated(username));
                    _ = Task.Run(() => NotifyUserListUpdated(friendUsername));

                    return FriendRequestResult.Success;
                }

                return FriendRequestResult.TargetNotFound;
            }
            catch (Exception ex)
            {
                Log.Error("Error crítico en RemoveFriend", ex);
                throw ExceptionManager.Map(ex);
            }
        }

        public async Task<List<FriendDto>> GetFriendList(string username)
        {
            try
            {
                var resultList = new List<FriendDto>();
                var player = await _repository.GetPlayerByUsernameAsync(username);

                if (player != null && !player.IsGuest)
                {
                    var friendships = _repository.GetAcceptedFriendships(player.IdPlayer);

                    foreach (var f in friendships)
                    {
                        int friendId = (f.PlayerIdPlayer == player.IdPlayer) ? f.Player1_IdPlayer : f.PlayerIdPlayer;
                        var friend = _repository.GetPlayerById(friendId);

                        if (friend != null)
                        {
                            bool isOnline = _connectionManager.IsClientConnected(friend.Username);
                            resultList.Add(new FriendDto
                            {
                                Username = friend.Username,
                                AvatarPath = friend.Avatar,
                                IsOnline = isOnline
                            });
                        }
                    }
                }
                return resultList;
            }
            catch (Exception ex)
            {
                Log.Error($"Error obteniendo lista de amigos para {username}", ex);
                throw ExceptionManager.Map(ex);
            }
        }

        public async Task<List<FriendDto>> GetPendingRequests(string username)
        {
            try
            {
                var resultList = new List<FriendDto>();
                var player = await _repository.GetPlayerByUsernameAsync(username);

                if (player != null && !player.IsGuest)
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
                return resultList;
            }
            catch (Exception ex)
            {
                Log.Error($"Error obteniendo solicitudes pendientes para {username}", ex);
                throw ExceptionManager.Map(ex);
            }
        }

        public async Task<List<FriendDto>> GetSentRequests(string username)
        {
            try
            {
                var resultList = new List<FriendDto>();
                var player = await _repository.GetPlayerByUsernameAsync(username);

                if (player != null && !player.IsGuest)
                {
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
                return resultList;
            }
            catch (Exception ex)
            {
                Log.Error($"Error obteniendo solicitudes enviadas para {username}", ex);
                throw ExceptionManager.Map(ex);
            }
        }

        public void SendGameInvitation(GameInvitationDto invitation)
        {
            if (invitation == null || string.IsNullOrEmpty(invitation.TargetUsername)) return;

            SafeNotifyClient(invitation.TargetUsername, client =>
                client.OnGameInvitationReceived(invitation.SenderUsername, invitation.LobbyCode));
        }

        private void NotifyUserRequestReceived(string username) => SafeNotifyClient(username, c => c.OnFriendRequestReceived());
        private void NotifyUserListUpdated(string username) => SafeNotifyClient(username, c => c.OnFriendListUpdated());
        private void NotifyUserPopUp(string target, string sender) => SafeNotifyClient(target, c => c.OnFriendRequestPopUp(sender));

        private void SafeNotifyClient(string username, Action<IFriendshipServiceCallback> action)
        {
            if (string.IsNullOrEmpty(username)) return;

            var client = _connectionManager.GetClient(username);
            if (client != null)
            {
                try
                {
                    action(client);
                }
                catch (CommunicationException)
                {
                    _connectionManager.RemoveClient(username);
                }
                catch (TimeoutException)
                {
                    _connectionManager.RemoveClient(username);
                }
                catch (Exception ex)
                {
                    Log.Error($"Error notificando a {username}", ex);
                }
            }
        }

        private void NotifyFriendsOfStatusChange(string username)
        {
            Task.Run(async () =>
            {
                try
                {
                    var repo = _repoFactory.Create();
                    if (repo is IDisposable disposableRepo)
                    {
                        using (disposableRepo)
                        {
                            await ProcessStatusChange(repo, username);
                        }
                    }
                    else
                    {
                        await ProcessStatusChange(repo, username);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"Error notificando estado de {username}", ex);
                }
            });
        }

        private async Task ProcessStatusChange(IFriendshipRepository repo, string username)
        {
            var player = await repo.GetPlayerByUsernameAsync(username);
            if (player != null && !player.IsGuest)
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
}