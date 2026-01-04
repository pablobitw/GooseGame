using GameServer.DTOs.Friendship;
using System.Collections.Generic;
using System.ServiceModel;
using System.Threading.Tasks;

namespace GameServer.Interfaces
{
    [ServiceContract(CallbackContract = typeof(IFriendshipServiceCallback))]
    public interface IFriendshipService
    {
        [OperationContract]
        void Connect(string username);

        [OperationContract]
        void Disconnect(string username);

        [OperationContract]
        Task<FriendRequestResult> SendFriendRequest(string senderUsername, string receiverUsername);

        [OperationContract]
        Task<bool> RespondToFriendRequest(RespondRequestDto request);

        [OperationContract]
        Task<List<FriendDto>> GetFriendList(string username);

        [OperationContract]
        Task<List<FriendDto>> GetPendingRequests(string username);

        [OperationContract]
        Task<bool> RemoveFriend(string username, string friendUsername);

        [OperationContract]
        void SendGameInvitation(GameInvitationDto invitation);
    }
}