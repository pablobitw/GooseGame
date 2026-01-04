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
            IFriendshipRepository repository = new FriendshipRepository();
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

        public async Task<FriendRequestResult> SendFriendRequest(string senderUsername, string receiverUsername)
        {
            return await _logic.SendFriendRequestAsync(senderUsername, receiverUsername);
        }

        public async Task<bool> RespondToFriendRequest(RespondRequestDto request)
        {
            return await _logic.RespondToFriendRequestAsync(request);
        }

        public async Task<bool> RemoveFriend(string username, string friendUsername)
        {
            return await _logic.RemoveFriendAsync(username, friendUsername);
        }

        public async Task<List<FriendDto>> GetFriendList(string username)
        {
            return await _logic.GetFriendListAsync(username);
        }

        public async Task<List<FriendDto>> GetPendingRequests(string username)
        {
            return await _logic.GetPendingRequestsAsync(username);
        }

        public void SendGameInvitation(GameInvitationDto invitation)
        {
            FriendshipAppService.SendGameInvitation(invitation);
        }
    }
}