using GameServer.DTOs.Chat;
using GameServer.Interfaces;
using log4net;
using System;
using System.Collections.Concurrent;
using System.Linq;

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
                Log.Warn("Intento de unirse al chat con datos inválidos.");
                return;
            }

            try
            {
                var lobbyChatters = _lobbies.GetOrAdd(request.LobbyCode, new ConcurrentDictionary<string, string>());
                lobbyChatters[request.Username] = request.Username;

                Log.Info($"Jugador '{request.Username}' unido al chat {request.LobbyCode}");
                BroadcastInternal(request.Username, request.LobbyCode, $"[Sistema]: {request.Username} se ha unido.");
            }
            catch (OverflowException ex)
            {
                Log.Error("Desbordamiento de memoria en diccionario de chat.", ex);
            }
            catch (ArgumentException ex)
            {
                Log.Error($"Error de argumento al unir a {request.Username}.", ex);
            }
        }

        public void SendMessage(ChatMessageDto messageDto)
        {
            if (messageDto == null || string.IsNullOrEmpty(messageDto.LobbyCode)) return;

            BroadcastInternal(messageDto.Sender, messageDto.LobbyCode, messageDto.Message);
        }

        public void LeaveChat(JoinChatRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.LobbyCode)) return;

            try
            {
                if (_lobbies.TryGetValue(request.LobbyCode, out var lobbyChatters))
                {
                    if (lobbyChatters.TryRemove(request.Username, out _))
                    {
                        Log.Info($"Jugador '{request.Username}' salió del chat {request.LobbyCode}");
                        BroadcastInternal(request.Username, request.LobbyCode, $"[Sistema]: {request.Username} ha salido.");
                    }

                    if (lobbyChatters.IsEmpty)
                    {
                        _lobbies.TryRemove(request.LobbyCode, out _);
                    }
                }
            }
            catch (ArgumentNullException ex)
            {
                Log.Error("Error nulo al salir del chat.", ex);
            }
        }

        private void BroadcastInternal(string senderUsername, string lobbyCode, string message)
        {
            if (_lobbies.TryGetValue(lobbyCode, out var lobbyChatters))
            {
                foreach (var userKey in lobbyChatters.Keys.ToList())
                {
                    if (!userKey.Equals(senderUsername, StringComparison.OrdinalIgnoreCase) || message.StartsWith("[Sistema]"))
                    {
                        _notifier.SendMessageToClient(userKey, senderUsername, message);
                    }
                }
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