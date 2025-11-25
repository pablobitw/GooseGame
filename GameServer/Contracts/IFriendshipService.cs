using System.Collections.Generic;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Threading.Tasks;

namespace GameServer.Contracts
{
    [DataContract]
    public class FriendDto
    {
        [DataMember]
        public string Username { get; set; }

        [DataMember]
        public string AvatarPath { get; set; }

        [DataMember]
        public bool IsOnline { get; set; }
    }
    [ServiceContract]
    public interface IFriendshipService
    {
        [OperationContract]
        Task<bool> SendFriendRequest(string senderUsername, string receiverUsername);

        [OperationContract]
        Task<bool> RespondToFriendRequest(string respondingUsername, string requesterUsername, bool isAccepted);

        [OperationContract]
        Task<List<FriendDto>> GetFriendList(string username);

        [OperationContract]
        Task<List<FriendDto>> GetPendingRequests(string username);

        [OperationContract]
        Task<bool> RemoveFriend(string username, string friendUsername);
    }
}