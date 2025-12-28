using GameServer.DTOs.Chat;
using GameServer.Interfaces;
using log4net;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.ServiceModel; 

namespace GameServer.Services.Logic
{
    public class ChatAppService
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(ChatAppService));

        private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, string>> _lobbies
            = new ConcurrentDictionary<string, ConcurrentDictionary<string, string>>();

        private readonly IChatNotifier _notifier;

        public ChatAppService(IChatNotifier notifier)
        {
            _notifier = notifier;
        }

        public void JoinChat(JoinChatRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.LobbyCode))
            {
                return;
            }

            var lobbyChatters = _lobbies.GetOrAdd(request.LobbyCode, new ConcurrentDictionary<string, string>());
            lobbyChatters[request.Username] = request.Username;

            var msg = new ChatMessageDto
            {
                Sender = "SYSTEM",
                LobbyCode = request.LobbyCode,
                Message = $"{request.Username} se ha unido.",
                IsPrivate = false
            };

            BroadcastInternal(request.Username, request.LobbyCode, msg);
        }

        public void SendMessage(ChatMessageDto messageDto)
        {
            if (messageDto == null || string.IsNullOrEmpty(messageDto.LobbyCode)) return;

            messageDto.IsPrivate = false;
            BroadcastInternal(messageDto.Sender, messageDto.LobbyCode, messageDto);
        }

        public void SendPrivateMessage(ChatMessageDto messageDto)
        {
            if (messageDto == null || string.IsNullOrEmpty(messageDto.TargetUser)) return;

            messageDto.IsPrivate = true;

            if (_notifier.IsUserConnected(messageDto.TargetUser))
            {
                SafeSendMessageToClient(messageDto.TargetUser, messageDto);

                SafeSendMessageToClient(messageDto.Sender, messageDto);
            }
            else
            {
                Log.Warn($"[PrivateChat] Usuario destino no encontrado o desconectado: {messageDto.TargetUser}");
            }
        }

        public void LeaveChat(JoinChatRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.LobbyCode)) return;

            if (_lobbies.TryGetValue(request.LobbyCode, out var lobbyChatters))
            {
                if (lobbyChatters.TryRemove(request.Username, out _))
                {
                    var msg = new ChatMessageDto
                    {
                        Sender = "SYSTEM",
                        LobbyCode = request.LobbyCode,
                        Message = $"{request.Username} ha salido.",
                        IsPrivate = false
                    };
                    BroadcastInternal(request.Username, request.LobbyCode, msg);
                }

                if (lobbyChatters.IsEmpty)
                {
                    _lobbies.TryRemove(request.LobbyCode, out _);
                }
            }
        }

        private void BroadcastInternal(string senderUsername, string lobbyCode, ChatMessageDto messageDto)
        {
            if (_lobbies.TryGetValue(lobbyCode, out var lobbyChatters))
            {
                var users = lobbyChatters.Keys.ToList();
                foreach (var userKey in users)
                {
                    if (!userKey.Equals(senderUsername, StringComparison.OrdinalIgnoreCase) || messageDto.Sender == "SYSTEM")
                    {
                        SafeSendMessageToClient(userKey, messageDto);
                    }
                }
            }
        }

        private void SafeSendMessageToClient(string userKey, ChatMessageDto messageDto)
        {
            try
            {
                _notifier.SendMessageToClient(userKey, messageDto);
            }
            catch (TimeoutException ex)
            {
                Log.Warn($"[Chat] Timeout enviando a {userKey}. El cliente podría estar lento o desconectado.", ex);
            }
            catch (CommunicationException ex)
            {
                Log.Warn($"[Chat] Error de comunicación con {userKey}. Conexión interrumpida.", ex);
            }
            catch (ObjectDisposedException ex)
            {
                Log.Warn($"[Chat] El canal de {userKey} ya ha sido eliminado/dispuesto.", ex);
            }
        }

        public void RemoveClient(string lobbyCode, string username)
        {
            if (!string.IsNullOrEmpty(lobbyCode) && _lobbies.TryGetValue(lobbyCode, out var lobbyChatters))
            {
                lobbyChatters.TryRemove(username, out _);
            }
        }
    }
}