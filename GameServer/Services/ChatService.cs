using GameServer.DTOs.Chat;
using GameServer.Interfaces;
using GameServer.Services.Logic;
using System.ServiceModel;

namespace GameServer.Services
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerCall, ConcurrencyMode = ConcurrencyMode.Multiple)]
    public class ChatService : IChatService
    {
        private readonly ChatAppService _logic;

        public ChatService()
        {
            _logic = new ChatAppService();
        }

        public void JoinLobbyChat(JoinChatRequest request)
        {
            var callback = OperationContext.Current.GetCallbackChannel<IChatCallback>();

            _logic.JoinChat(request, callback);
        }

        public void SendLobbyMessage(ChatMessageDto messageDto)
        {
            _logic.SendMessage(messageDto);
        }

        public void SendPrivateMessage(ChatMessageDto messageDto)
        {
            _logic.SendPrivateMessage(messageDto);
        }

        public void LeaveLobbyChat(JoinChatRequest request)
        {
            _logic.LeaveChat(request);
        }
    }
}