using GameServer.DTOs.Chat;

namespace GameServer.Interfaces
{
    public interface IChatNotifier
    {
        void SendMessageToClient(string clientKey, ChatMessageDto message);
        bool IsUserConnected(string username);
    }
}