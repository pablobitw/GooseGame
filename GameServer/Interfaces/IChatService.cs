using GameServer.DTOs.Chat;
using GameServer.Faults;
using System.ServiceModel;
using System.Threading.Tasks;

namespace GameServer.Interfaces
{
    [ServiceContract(CallbackContract = typeof(IChatCallback))]
    public interface IChatService
    {
        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        Task<ChatOperationResult> JoinLobbyChat(JoinChatRequest request);

        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        Task<ChatOperationResult> SendLobbyMessage(ChatMessageDto messageDto);

        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        Task<ChatOperationResult> SendPrivateMessage(ChatMessageDto messageDto);

        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        Task<ChatOperationResult> LeaveLobbyChat(JoinChatRequest request);
    }
}