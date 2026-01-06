using GameServer.DTOs.Chat;
using GameServer.Interfaces;
using GameServer.Services.Logic;
using System.ServiceModel;
using System.Threading.Tasks;

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

        public Task<ChatOperationResult> JoinLobbyChat(JoinChatRequest request)
        {
            var callback = OperationContext.Current.GetCallbackChannel<IChatCallback>();
            return Task.FromResult(_logic.JoinChat(request, callback));
        }

        public Task<ChatOperationResult> SendLobbyMessage(ChatMessageDto messageDto)
        {
            return Task.FromResult(_logic.SendMessage(messageDto));
        }

        public Task<ChatOperationResult> SendPrivateMessage(ChatMessageDto messageDto)
        {
            return Task.FromResult(_logic.SendPrivateMessage(messageDto));
        }

        public Task<ChatOperationResult> LeaveLobbyChat(JoinChatRequest request)
        {
            return Task.FromResult(_logic.LeaveChat(request));
        }
    }
}