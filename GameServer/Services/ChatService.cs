using GameServer.Contracts;
using log4net;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;

namespace GameServer.Services
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple)]
    public class ChatService : IChatService
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(ChatService));

        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, IChatCallback>> _lobbies;

        public ChatService()
        {
            _lobbies = new ConcurrentDictionary<string, ConcurrentDictionary<string, IChatCallback>>();
        }

        public void JoinLobbyChat(string username, string lobbyCode)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(lobbyCode)) return;

            var callback = OperationContext.Current.GetCallbackChannel<IChatCallback>();

            var lobbyChatters = _lobbies.GetOrAdd(lobbyCode, new ConcurrentDictionary<string, IChatCallback>());

            lobbyChatters[username] = callback;

            Log.Info($"El jugador '{username}' se unió al chat del lobby {lobbyCode}");
            BroadcastMessage(username, lobbyCode, "[Sistema]: " + username + " se ha unido al chat.");
        }

        public void LeaveLobbyChat(string username, string lobbyCode)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(lobbyCode)) return;

            if (_lobbies.TryGetValue(lobbyCode, out var lobbyChatters))
            {
                if (lobbyChatters.TryRemove(username, out _))
                {
                    Log.Info($"El jugador '{username}' abandonó el chat del lobby {lobbyCode}");
                    BroadcastMessage(username, lobbyCode, "[Sistema]: " + username + " ha abandonado el chat.");
                }

                if (lobbyChatters.IsEmpty)
                {
                    _lobbies.TryRemove(lobbyCode, out _);
                    Log.Info($"Chat del lobby {lobbyCode} cerrado por estar vacío.");
                }
            }
        }

        public void SendLobbyMessage(string username, string lobbyCode, string message)
        {
            BroadcastMessage(username, lobbyCode, message);
        }

        private void BroadcastMessage(string senderUsername, string lobbyCode, string message)
        {
            if (!_lobbies.TryGetValue(lobbyCode, out var lobbyChatters))
            {
                return;
            }

            var clientsToRemove = new List<string>();

            foreach (var chatter in lobbyChatters.ToList())
            {
                if (chatter.Key.Equals(senderUsername, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    chatter.Value.ReceiveMessage(senderUsername, message);
                }
                catch (Exception ex)
                {
                    Log.Warn($"No se pudo enviar mensaje a {chatter.Key} en lobby {lobbyCode}. Removiendo.", ex);
                    clientsToRemove.Add(chatter.Key);
                }
            }

            foreach (var clientKeyToRemove in clientsToRemove)
            {
                lobbyChatters.TryRemove(clientKeyToRemove, out _);
            }
        }
    }
}