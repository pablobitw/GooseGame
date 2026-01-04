using GameServer.DTOs.Friendship;
using GameServer.Interfaces;
using GameServer.Repositories;
using GameServer.Services.Logic;
using System.Collections.Generic;
using System.ServiceModel;
using System.Threading.Tasks;

namespace GameServer.Services
{
    [ServiceBehavior(ConcurrencyMode = ConcurrencyMode.Multiple, InstanceContextMode = InstanceContextMode.PerCall)]
    public class FriendshipService : IFriendshipService
    {
        public void Connect(string username)
        {
            var callback = OperationContext.Current.GetCallbackChannel<IFriendshipServiceCallback>();
            using (var repository = new FriendshipRepository())
            {
                var logic = new FriendshipAppService(repository);
                logic.ConnectUser(username, callback);
            }
        }

        public void Disconnect(string username)
        {
            using (var repository = new FriendshipRepository())
            {
                var logic = new FriendshipAppService(repository);
                logic.DisconnectUser(username);
            }
        }

        public async Task<FriendRequestResult> SendFriendRequest(string senderUsername, string receiverUsername)
        {
            using (var repository = new FriendshipRepository())
            {
                var logic = new FriendshipAppService(repository);
                return await logic.SendFriendRequestAsync(senderUsername, receiverUsername);
            }
        }

        public async Task<bool> RespondToFriendRequest(RespondRequestDto request)
        {
            using (var repository = new FriendshipRepository())
            {
                var logic = new FriendshipAppService(repository);
                return await logic.RespondToFriendRequestAsync(request);
            }
        }

        public async Task<bool> RemoveFriend(string username, string friendUsername)
        {
            using (var repository = new FriendshipRepository())
            {
                var logic = new FriendshipAppService(repository);
                return await logic.RemoveFriendAsync(username, friendUsername);
            }
        }

        public async Task<List<FriendDto>> GetFriendList(string username)
        {
            using (var repository = new FriendshipRepository())
            {
                var logic = new FriendshipAppService(repository);
                return await logic.GetFriendListAsync(username);
            }
        }

        public async Task<List<FriendDto>> GetPendingRequests(string username)
        {
            using (var repository = new FriendshipRepository())
            {
                var logic = new FriendshipAppService(repository);
                return await logic.GetPendingRequestsAsync(username);
            }
        }

        public async Task<List<FriendDto>> GetSentRequests(string username)
        {
            using (var repository = new FriendshipRepository())
            {
                var logic = new FriendshipAppService(repository);
                return await logic.GetSentRequestsAsync(username);
            }
        }

        public void SendGameInvitation(GameInvitationDto invitation)
        {
            FriendshipAppService.SendGameInvitation(invitation);
        }
    }
}