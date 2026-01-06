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
            using (var repository = new FriendshipRepository())
            {
                var logic = new FriendshipAppService(repository);
                logic.Connect(username);
            }
        }

        public void Disconnect(string username)
        {
            using (var repository = new FriendshipRepository())
            {
                var logic = new FriendshipAppService(repository);
                logic.Disconnect(username);
            }
        }

        public async Task<FriendRequestResult> SendFriendRequest(string senderUsername, string receiverUsername)
        {
            using (var repository = new FriendshipRepository())
            {
                var logic = new FriendshipAppService(repository);
                return await logic.SendFriendRequest(senderUsername, receiverUsername);
            }
        }

        public async Task<FriendRequestResult> RespondToFriendRequest(RespondRequestDto request)
        {
            using (var repository = new FriendshipRepository())
            {
                var logic = new FriendshipAppService(repository);
                return await logic.RespondToFriendRequest(request);
            }
        }

        public async Task<FriendRequestResult> RemoveFriend(string username, string friendUsername)
        {
            using (var repository = new FriendshipRepository())
            {
                var logic = new FriendshipAppService(repository);
                return await logic.RemoveFriend(username, friendUsername);
            }
        }

        public async Task<List<FriendDto>> GetFriendList(string username)
        {
            using (var repository = new FriendshipRepository())
            {
                var logic = new FriendshipAppService(repository);
                return await logic.GetFriendList(username);
            }
        }

        public async Task<List<FriendDto>> GetPendingRequests(string username)
        {
            using (var repository = new FriendshipRepository())
            {
                var logic = new FriendshipAppService(repository);
                return await logic.GetPendingRequests(username);
            }
        }

        public async Task<List<FriendDto>> GetSentRequests(string username)
        {
            using (var repository = new FriendshipRepository())
            {
                var logic = new FriendshipAppService(repository);
                return await logic.GetSentRequests(username);
            }
        }

        public void SendGameInvitation(GameInvitationDto invitation)
        {
            using (var repository = new FriendshipRepository())
            {
                var logic = new FriendshipAppService(repository);
                logic.SendGameInvitation(invitation);
            }
        }
    }
}