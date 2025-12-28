using GameServer.DTOs.Chat;
using System.ServiceModel;

namespace GameServer.Interfaces
{
    [ServiceContract]
    public interface IChatCallback
    {
        [OperationContract(IsOneWay = true)]
        void ReceiveMessage(ChatMessageDto message);
    }
}