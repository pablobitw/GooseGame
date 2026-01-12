using GameServer.DTOs.Friendship;
using GameServer.Faults;
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
        [FaultContract(typeof(ServiceFault))]
        Task<FriendRequestResult> SendFriendRequest(string senderUsername, string receiverUsername);

        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        Task<FriendRequestResult> RespondToFriendRequest(RespondRequestDto request);

        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        Task<List<FriendDto>> GetFriendList(string username);

        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        Task<List<FriendDto>> GetPendingRequests(string username);

        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        Task<FriendRequestResult> RemoveFriend(string username, string friendUsername);

        [OperationContract]
        void SendGameInvitation(GameInvitationDto invitation);

        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        Task<List<FriendDto>> GetSentRequests(string username);
    }
}