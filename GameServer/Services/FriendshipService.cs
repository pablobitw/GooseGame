using GameServer.DTOs.Friendship;
using GameServer.Interfaces;
using GameServer.Repositories;
using GameServer.Services.Logic;
using System;
using System.Collections.Generic;
using System.ServiceModel;
using System.Threading.Tasks;

namespace GameServer.Services
{
    [ServiceBehavior(ConcurrencyMode = ConcurrencyMode.Multiple, InstanceContextMode = InstanceContextMode.PerCall)]
    public class FriendshipService : IFriendshipService, IDisposable
    {
        private readonly FriendshipRepository _repository;
        private readonly FriendshipAppService _logic;

        public FriendshipService()
        {
            _repository = new FriendshipRepository();
            _logic = new FriendshipAppService(_repository);
        }

        public void Connect(string username)
        {
            _logic.Connect(username);
        }

        public void Disconnect(string username)
        {
            _logic.Disconnect(username);
        }

        public Task<FriendRequestResult> SendFriendRequest(string senderUsername, string receiverUsername)
        {
            return _logic.SendFriendRequest(senderUsername, receiverUsername);
        }

        public Task<FriendRequestResult> RespondToFriendRequest(RespondRequestDto request)
        {
            return _logic.RespondToFriendRequest(request);
        }

        public Task<FriendRequestResult> RemoveFriend(string username, string friendUsername)
        {
            return _logic.RemoveFriend(username, friendUsername);
        }

        public Task<List<FriendDto>> GetFriendList(string username)
        {
            return _logic.GetFriendList(username);
        }

        public Task<List<FriendDto>> GetPendingRequests(string username)
        {
            return _logic.GetPendingRequests(username);
        }

        public Task<List<FriendDto>> GetSentRequests(string username)
        {
            return _logic.GetSentRequests(username);
        }

        public void SendGameInvitation(GameInvitationDto invitation)
        {
            _logic.SendGameInvitation(invitation);
        }

        public void Dispose()
        {
            _repository?.Dispose();
        }
    }
}