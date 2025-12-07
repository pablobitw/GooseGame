using GameServer.DTOs.Chat;
using GameServer.Interfaces;
using GameServer.Services.Logic;
using log4net;
using System;
using System.Collections.Concurrent;
using System.ServiceModel;

namespace GameServer.Services
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple)]
    public class ChatService : IChatService, IChatNotifier
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(ChatService));
        private readonly ChatAppService _logic;

        private readonly ConcurrentDictionary<string, IChatCallback> _callbacks = new ConcurrentDictionary<string, IChatCallback>();

        public ChatService()
        {
            _logic = new ChatAppService(this);
        }

        public void JoinLobbyChat(JoinChatRequest request)
        {
            var callback = OperationContext.Current.GetCallbackChannel<IChatCallback>();
            if (callback != null && request != null)
            {
                _callbacks[request.Username] = callback;

                _logic.JoinChat(request);
            }
        }

        public void SendLobbyMessage(ChatMessageDto messageDto)
        {
            _logic.SendMessage(messageDto);
        }

        public void LeaveLobbyChat(JoinChatRequest request)
        {
            _logic.LeaveChat(request);
            if (request != null)
            {
                _callbacks.TryRemove(request.Username, out _);
            }
        }

        public void SendMessageToClient(string clientKey, string sender, string message)
        {
            if (_callbacks.TryGetValue(clientKey, out var callback))
            {
                try
                {
                    callback.ReceiveMessage(sender, message);
                }
                catch (CommunicationException)
                {
                    Log.Warn($"Cliente {clientKey} desconectado. Removiendo.");
                    _callbacks.TryRemove(clientKey, out _);
                }
                catch (TimeoutException)
                {
                    _callbacks.TryRemove(clientKey, out _);
                }
            }
        }
    }
}