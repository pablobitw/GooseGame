using GameServer.Chat.Moderation; // Asegúrate de tener estas clases o coméntalas si son mocks
using GameServer.DTOs.Chat;
using GameServer.Interfaces;
using log4net;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace GameServer.Services.Logic
{
    public class ChatAppService
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(ChatAppService));

        private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, string>> _lobbies
            = new ConcurrentDictionary<string, ConcurrentDictionary<string, string>>();

        private static readonly ConcurrentDictionary<string, IChatCallback> _callbacks
            = new ConcurrentDictionary<string, IChatCallback>();

        private static readonly SpamTracker _spamTracker = new SpamTracker();

        private const int MaxMessageLength = 100;

        private readonly Func<SanctionAppService> _sanctionServiceFactory;

        public ChatAppService()
        {
            _sanctionServiceFactory = () => new SanctionAppService();
        }

        public void JoinChat(JoinChatRequest request, IChatCallback callback)
        {
            if (request == null || string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.LobbyCode))
                return;

            _callbacks.AddOrUpdate(request.Username, callback, (k, v) => callback);

            var lobbyChatters = _lobbies.GetOrAdd(request.LobbyCode, _ => new ConcurrentDictionary<string, string>());
            lobbyChatters[request.Username] = request.Username;

            Log.Info($"Chat: {request.Username} se unió al lobby {request.LobbyCode}");
            BroadcastSystemMessage(request.LobbyCode, $"{request.Username} se ha unido.");
        }

        public void SendMessage(ChatMessageDto messageDto)
        {
            if (messageDto == null || string.IsNullOrEmpty(messageDto.LobbyCode)) return;

            if (string.IsNullOrWhiteSpace(messageDto.Message)) return;

            if (messageDto.Message.Length > MaxMessageLength)
            {
                SendSystemMessageToUser(messageDto.Sender, "Mensaje demasiado largo.");
                return;
            }

            var spamResult = _spamTracker.Analyze(messageDto.LobbyCode, messageDto.Sender);
            if (!spamResult.IsAllowed)
            {
                SendSystemMessageToUser(messageDto.Sender, spamResult.SystemNotification);
                if (spamResult.RequiresKick)
                {
                    KickUserForChatOffense(messageDto.LobbyCode, messageDto.Sender, "Spam masivo", "CHAT_SPAM");
                }
                return;
            }

            var profanityResult = ProfanityFilter.Analyze(messageDto.Message);
            if (profanityResult.IsBlocked)
            {
                SendSystemMessageToUser(messageDto.Sender, profanityResult.SystemNotification);
                return;
            }

            if (profanityResult.IsCensored)
            {
                var level = WarningTracker.RegisterWarning(messageDto.LobbyCode, messageDto.Sender);
                HandleWarning(messageDto.LobbyCode, messageDto.Sender, level);
                if (level == WarningLevel.Punishment) return; 
            }

            messageDto.Message = profanityResult.FinalMessage;
            messageDto.IsPrivate = false;

            BroadcastInternal(messageDto.Sender, messageDto.LobbyCode, messageDto);
        }

        public void SendPrivateMessage(ChatMessageDto messageDto)
        {
            if (messageDto == null || string.IsNullOrEmpty(messageDto.TargetUser)) return;

            messageDto.IsPrivate = true;

            bool sent = SafeSendMessageToClient(messageDto.TargetUser, messageDto);

            if (sent)
            {
                SafeSendMessageToClient(messageDto.Sender, messageDto);
            }
            else
            {
                SendSystemMessageToUser(messageDto.Sender, $"El usuario {messageDto.TargetUser} no está disponible.");
            }
        }

        public void LeaveChat(JoinChatRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.LobbyCode)) return;

            RemoveClient(request.LobbyCode, request.Username);
            BroadcastSystemMessage(request.LobbyCode, $"{request.Username} ha salido.");
        }


        private void HandleWarning(string lobbyCode, string username, WarningLevel level)
        {
            switch (level)
            {
                case WarningLevel.Warning:
                    SendSystemMessageToUser(username, "Advertencia: Lenguaje inapropiado.");
                    break;
                case WarningLevel.LastWarning:
                    SendSystemMessageToUser(username, "Último aviso antes de expulsión.");
                    break;
                case WarningLevel.Punishment:
                    BroadcastSystemMessage(lobbyCode, $"{username} fue expulsado por toxicidad.");
                    KickUserForChatOffense(lobbyCode, username, "Toxicidad", "CHAT_TOXICITY");
                    break;
            }
        }

        private void KickUserForChatOffense(string lobbyCode, string username, string reason, string source)
        {
            Task.Run(async () =>
            {
                try
                {
                    using (var sanctionService = _sanctionServiceFactory())
                    {
                        await sanctionService.ProcessKickAsync(username, lobbyCode, reason, source);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"Error aplicando kick a {username}", ex);
                }
            });

            RemoveClient(lobbyCode, username);
        }

        private void BroadcastSystemMessage(string lobbyCode, string message)
        {
            var systemMessage = new ChatMessageDto
            {
                Sender = "SYSTEM",
                LobbyCode = lobbyCode,
                Message = message,
                IsPrivate = false
            };
            BroadcastInternal("SYSTEM", lobbyCode, systemMessage);
        }

        private void SendSystemMessageToUser(string username, string message)
        {
            var msg = new ChatMessageDto { Sender = "SYSTEM", Message = message, IsPrivate = true };
            SafeSendMessageToClient(username, msg);
        }

        private void BroadcastInternal(string senderUsername, string lobbyCode, ChatMessageDto messageDto)
        {
            if (_lobbies.TryGetValue(lobbyCode, out var lobbyChatters))
            {
                var recipients = lobbyChatters.Keys.ToList();
                foreach (var userKey in recipients)
                {
                    if (senderUsername != "SYSTEM" && userKey == senderUsername) continue;

                    SafeSendMessageToClient(userKey, messageDto);
                }
            }
        }

        private bool SafeSendMessageToClient(string userKey, ChatMessageDto messageDto)
        {
            if (_callbacks.TryGetValue(userKey, out var callback))
            {
                try
                {
                    callback.ReceiveMessage(messageDto);
                    return true;
                }
                catch (Exception)
                {
                    Log.Warn($"No se pudo contactar a {userKey}, eliminando sesión.");
                    _callbacks.TryRemove(userKey, out _);
                    return false;
                }
            }
            return false;
        }

        public static void RemoveClient(string lobbyCode, string username)
        {
            if (!string.IsNullOrEmpty(lobbyCode) && _lobbies.TryGetValue(lobbyCode, out var lobbyChatters))
            {
                lobbyChatters.TryRemove(username, out _);
            }
            _callbacks.TryRemove(username, out _);
            WarningTracker.Reset(lobbyCode, username);
        }
    }
}