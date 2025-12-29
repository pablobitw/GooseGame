using GameServer.DTOs.Friendship;
using GameServer.Interfaces;
using GameServer.Repositories;
using GameServer.Services.Logic;
using System.Collections.Generic;
using System.ServiceModel;
using System.Threading.Tasks;

namespace GameServer.Services
{
    [ServiceBehavior(ConcurrencyMode = ConcurrencyMode.Multiple, InstanceContextMode = InstanceContextMode.Single)]
    public class FriendshipService : IFriendshipService
    {
        private readonly FriendshipAppService _logic;

        public FriendshipService()
        {
            var repository = new FriendshipRepository();
            _logic = new FriendshipAppService(repository);
        }

        public void Connect(string username)
        {
            var callback = OperationContext.Current.GetCallbackChannel<IFriendshipServiceCallback>();
            _logic.ConnectUser(username, callback);
        }

        public void Disconnect(string username)
        {
            _logic.DisconnectUser(username);
        }

        public async Task<bool> SendFriendRequest(string senderUsername, string receiverUsername)
        {
            bool result;
            result = await _logic.SendFriendRequestAsync(senderUsername, receiverUsername);
            return result;
        }

        public async Task<bool> RespondToFriendRequest(RespondRequestDto request)
        {
            bool result;
            result = await _logic.RespondToFriendRequestAsync(request);
            return result;
        }

        public async Task<bool> RemoveFriend(string username, string friendUsername)
        {
            bool result;
            result = await _logic.RemoveFriendAsync(username, friendUsername);
            return result;
        }

        public async Task<List<FriendDto>> GetFriendList(string username)
        {
            List<FriendDto> result;
            result = await _logic.GetFriendListAsync(username);
            return result;
        }

        public async Task<List<FriendDto>> GetPendingRequests(string username)
        {
            List<FriendDto> result;
            result = await _logic.GetPendingRequestsAsync(username);
            return result;
        }

        public void SendGameInvitation(GameInvitationDto invitation)
        {
            FriendshipAppService.SendGameInvitation(invitation);
        }
    }
}