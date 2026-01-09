using GameServer.DTOs.Friendship;
using GameServer.Helpers;
using GameServer.Interfaces;
using GameServer.Models;
using GameServer.Repositories;
using GameServer.Repositories.Interfaces;
using log4net;
using System;
using System.Collections.Generic;
using System.Data.Entity.Core;
using System.Data.Entity.Infrastructure;
using System.Data.SqlClient;
using System.ServiceModel;
using System.Threading.Tasks;
using GameServer.Services.Common;


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

            _connectionManager.RemoveClient(username);
            NotifyFriendsOfStatusChange(username);
        }

        public async Task<FriendRequestResult> SendFriendRequest(string senderUsername, string receiverUsername)
        {
            FriendRequestResult result = FriendRequestResult.Error;

            try
            {
                if (string.IsNullOrEmpty(senderUsername) || string.IsNullOrEmpty(receiverUsername) ||
                    senderUsername.Equals(receiverUsername, StringComparison.OrdinalIgnoreCase))
                {
                    return FriendRequestResult.Error;
                }

                var sender = await _repository.GetPlayerByUsernameAsync(senderUsername);
                var receiver = await _repository.GetPlayerByUsernameAsync(receiverUsername);

                if (sender == null || receiver == null)
                {
                    result = FriendRequestResult.TargetNotFound;
                }
                else if (sender.IsGuest || receiver.IsGuest)
                {
                    result = FriendRequestResult.GuestRestriction;
                }
                else
                {
                    var existing = _repository.GetFriendship(sender.IdPlayer, receiver.IdPlayer);

                    if (existing != null)
                    {
                        if (existing.FriendshipStatus == (int)FriendshipStatus.Accepted)
                        {
                            result = FriendRequestResult.AlreadyFriends;
                        }
                        else
                        {
                            if (existing.PlayerIdPlayer == receiver.IdPlayer)
                            {
                                existing.FriendshipStatus = (int)FriendshipStatus.Accepted;
                                await _repository.SaveChangesAsync();

                                _ = Task.Run(() => NotifyUserListUpdated(senderUsername));
                                _ = Task.Run(() => NotifyUserListUpdated(receiverUsername));

                                result = FriendRequestResult.MutualAccepted;
                                Log.InfoFormat("Solicitud mutua detectada: {0} y {1} ahora son amigos.", senderUsername, receiverUsername);
                            }
                            else
                            {
                                result = FriendRequestResult.Pending;
                            }
                        }
                    }
                    else
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

                        _ = Task.Run(() => NotifyUserRequestReceived(receiverUsername));
                        _ = Task.Run(() => NotifyUserPopUp(receiverUsername, senderUsername));

                        result = FriendRequestResult.Success;
                    }
                }
            }
            catch (DbUpdateException ex)
            {
                Log.Error("Error DB Update enviando solicitud.", ex);
                result = FriendRequestResult.DatabaseError;
            }
            catch (SqlException ex)
            {
                Log.Fatal("Error SQL enviando solicitud.", ex);
                result = FriendRequestResult.DatabaseError;
            }
            catch (EntityException ex)
            {
                Log.Error("Error EF enviando solicitud.", ex);
                result = FriendRequestResult.DatabaseError;
            }
            catch (TimeoutException ex)
            {
                Log.Error("Timeout enviando solicitud.", ex);
                result = FriendRequestResult.TimeOutError;
            }
            catch (Exception ex)
            {
                Log.Error("Error general enviando solicitud.", ex);
                result = FriendRequestResult.Error;
            }

            return result;
        }

        public async Task<FriendRequestResult> RespondToFriendRequest(RespondRequestDto request)
        {
            FriendRequestResult result = FriendRequestResult.Error;

            try
            {
                var responder = await _repository.GetPlayerByUsernameAsync(request.RespondingUsername);
                var requester = await _repository.GetPlayerByUsernameAsync(request.RequesterUsername);

                if (responder != null && requester != null && !responder.IsGuest)
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

                        _ = Task.Run(() => NotifyUserListUpdated(request.RequesterUsername));
                        _ = Task.Run(() => NotifyUserListUpdated(request.RespondingUsername));

                        result = FriendRequestResult.Success;
                    }
                    else
                    {
                        result = FriendRequestResult.TargetNotFound;
                    }
                }
                else
                {
                    result = FriendRequestResult.TargetNotFound;
                }
            }
            catch (DbUpdateException ex)
            {
                Log.Error("Error DB Update respondiendo solicitud.", ex);
                result = FriendRequestResult.DatabaseError;
            }
            catch (SqlException ex)
            {
                Log.Fatal("Error SQL respondiendo solicitud.", ex);
                result = FriendRequestResult.DatabaseError;
            }
            catch (EntityException ex)
            {
                Log.Error("Error EF respondiendo solicitud.", ex);
                result = FriendRequestResult.DatabaseError;
            }
            catch (TimeoutException ex)
            {
                Log.Error("Timeout respondiendo solicitud.", ex);
                result = FriendRequestResult.DatabaseError;
            }
            catch (Exception ex)
            {
                Log.Error("Error general respondiendo solicitud.", ex);
                result = FriendRequestResult.Error;
            }

            return result;
        }

        public async Task<FriendRequestResult> RemoveFriend(string username, string friendUsername)
        {
            FriendRequestResult result = FriendRequestResult.Error;

            try
            {
                var user1 = await _repository.GetPlayerByUsernameAsync(username);
                var user2 = await _repository.GetPlayerByUsernameAsync(friendUsername);

                if (user1 != null && user2 != null && !user1.IsGuest)
                {
                    var friendship = _repository.GetFriendship(user1.IdPlayer, user2.IdPlayer);

                    if (friendship != null)
                    {
                        _repository.RemoveFriendship(friendship);
                        await _repository.SaveChangesAsync();

                        _ = Task.Run(() => NotifyUserListUpdated(username));
                        _ = Task.Run(() => NotifyUserListUpdated(friendUsername));

                        result = FriendRequestResult.Success;
                    }
                    else
                    {
                        result = FriendRequestResult.TargetNotFound;
                    }
                }
                else
                {
                    result = FriendRequestResult.TargetNotFound;
                }
            }
            catch (DbUpdateException ex)
            {
                Log.Error("Error DB Update eliminando amigo.", ex);
                result = FriendRequestResult.DatabaseError;
            }
            catch (SqlException ex)
            {
                Log.Fatal("Error SQL eliminando amigo.", ex);
                result = FriendRequestResult.DatabaseError;
            }
            catch (EntityException ex)
            {
                Log.Error("Error EF eliminando amigo.", ex);
                result = FriendRequestResult.DatabaseError;
            }
            catch (TimeoutException ex)
            {
                Log.Error("Timeout eliminando amigo.", ex);
                result = FriendRequestResult.TimeOutError;
            }
            catch (Exception ex)
            {
                Log.Error("Error general eliminando amigo.", ex);
                result = FriendRequestResult.Error;
            }

            return result;
        }

        public async Task<List<FriendDto>> GetFriendList(string username)
        {
            var resultList = new List<FriendDto>();
            try
            {
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
            }
            catch (SqlException ex)
            {
                Log.Fatal("Error SQL obteniendo lista de amigos.", ex);
            }
            catch (EntityException ex)
            {
                Log.Error("Error EF obteniendo lista de amigos.", ex);
            }
            catch (TimeoutException ex)
            {
                Log.Error("Timeout obteniendo lista de amigos.", ex);
            }
            catch (Exception ex)
            {
                Log.Error($"Error obteniendo lista de amigos para {username}.", ex);
            }
            return resultList;
        }

        public async Task<List<FriendDto>> GetPendingRequests(string username)
        {
            var resultList = new List<FriendDto>();
            try
            {
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
            }
            catch (SqlException ex)
            {
                Log.Fatal("Error SQL obteniendo solicitudes pendientes.", ex);
            }
            catch (EntityException ex)
            {
                Log.Error("Error EF obteniendo solicitudes pendientes.", ex);
            }
            catch (TimeoutException ex)
            {
                Log.Error("Timeout obteniendo solicitudes pendientes.", ex);
            }
            catch (Exception ex)
            {
                Log.Error($"Error obteniendo solicitudes pendientes para {username}.", ex);
            }
            return resultList;
        }

        public async Task<List<FriendDto>> GetSentRequests(string username)
        {
            var resultList = new List<FriendDto>();
            try
            {
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
            }
            catch (SqlException ex)
            {
                Log.Fatal("Error SQL obteniendo solicitudes enviadas.", ex);
            }
            catch (EntityException ex)
            {
                Log.Error("Error EF obteniendo solicitudes enviadas.", ex);
            }
            catch (TimeoutException ex)
            {
                Log.Error("Timeout obteniendo solicitudes enviadas.", ex);
            }
            catch (Exception ex)
            {
                Log.Error($"Error obteniendo solicitudes enviadas para {username}.", ex);
            }
            return resultList;
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