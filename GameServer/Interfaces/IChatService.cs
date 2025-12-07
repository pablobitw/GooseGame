using GameServer.DTOs.Chat;
using System.ServiceModel;

namespace GameServer.Interfaces
{
    [ServiceContract(CallbackContract = typeof(IChatCallback))]
    public interface IChatService
    {
        [OperationContract(IsOneWay = true)]
        void JoinLobbyChat(JoinChatRequest request);

        [OperationContract(IsOneWay = true)]
        void SendLobbyMessage(ChatMessageDto messageDto);

        [OperationContract(IsOneWay = true)]
        void LeaveLobbyChat(JoinChatRequest request);
    }
}