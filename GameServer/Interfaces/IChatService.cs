using GameServer.DTOs.Chat;
using System.ServiceModel;
using System.Threading.Tasks;

namespace GameServer.Interfaces
{
    [ServiceContract(CallbackContract = typeof(IChatCallback))]
    public interface IChatService
    {
        [OperationContract]
        Task<ChatOperationResult> JoinLobbyChat(JoinChatRequest request);

        [OperationContract]
        Task<ChatOperationResult> SendLobbyMessage(ChatMessageDto messageDto);

        [OperationContract]
        Task<ChatOperationResult> SendPrivateMessage(ChatMessageDto messageDto);

        [OperationContract]
        Task<ChatOperationResult> LeaveLobbyChat(JoinChatRequest request);
    }
}