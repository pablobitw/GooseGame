using GameServer.Chat.Moderation;
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
        private static readonly ILog Log =
            LogManager.GetLogger(typeof(ChatAppService));

        private static readonly ConcurrentDictionary<string,
            ConcurrentDictionary<string, string>> _lobbies
            = new ConcurrentDictionary<string, ConcurrentDictionary<string, string>>();

        private static readonly SpamTracker _spamTracker = new SpamTracker();
        
        private const int MaxMessageLength = 50;

        private readonly IChatNotifier _notifier;

        public ChatAppService(IChatNotifier notifier)
        {
            _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
        }

        public void JoinChat(JoinChatRequest request)
        {
            if (request == null ||
                string.IsNullOrEmpty(request.Username) ||
                string.IsNullOrEmpty(request.LobbyCode))
            {
                return;
            }

            var lobbyChatters = _lobbies.GetOrAdd(
                request.LobbyCode,
                _ => new ConcurrentDictionary<string, string>()
            );

            lobbyChatters[request.Username] = request.Username;

            BroadcastSystemMessage(
                request.LobbyCode,
                $"{request.Username} se ha unido."
            );
        }

        public void SendMessage(ChatMessageDto messageDto)
        {
            if (messageDto == null || string.IsNullOrEmpty(messageDto.LobbyCode))
                return;

            if (string.IsNullOrWhiteSpace(messageDto.Message) ||
                messageDto.Message.Length > MaxMessageLength)
            {
                BroadcastSystemMessage(
                    messageDto.LobbyCode,
                    $"El mensaje de {messageDto.Sender} no fue enviado por exceder el límite permitido."
                );
                return;
            }

            // ---------- SPAM ----------
            var spamResult = _spamTracker.Analyze(
                messageDto.LobbyCode,
                messageDto.Sender
            );

            if (!spamResult.IsAllowed)
            {
                BroadcastSystemMessage(
                    messageDto.LobbyCode,
                    spamResult.SystemNotification
                );

                if (spamResult.RequiresKick)
                {
                    RemoveClient(messageDto.LobbyCode, messageDto.Sender);
                }
                return;
            }

          
            var profanityResult = ProfanityFilter.Analyze(messageDto.Message);

            if (profanityResult.IsBlocked)
            {
                BroadcastSystemMessage(
                    messageDto.LobbyCode,
                    profanityResult.SystemNotification
                );
                return;
            }

            if (profanityResult.IsCensored)
            {
                var level = WarningTracker.RegisterWarning(
                    messageDto.LobbyCode,
                    messageDto.Sender
                );

                switch (level)
                {
                    case WarningLevel.Warning:
                        BroadcastSystemMessage(
                            messageDto.LobbyCode,
                            $"{messageDto.Sender}: advertencia por lenguaje inapropiado."
                        );
                        break;

                    case WarningLevel.LastWarning:
                        BroadcastSystemMessage(
                            messageDto.LobbyCode,
                            $"{messageDto.Sender}: último aviso."
                        );
                        break;

                    case WarningLevel.Punishment:
                        BroadcastSystemMessage(
                            messageDto.LobbyCode,
                            $"{messageDto.Sender} fue expulsado por lenguaje inapropiado."
                        );
                        RemoveClient(messageDto.LobbyCode, messageDto.Sender);
                        return;
                }
            }

            messageDto.Message = profanityResult.FinalMessage;
            messageDto.IsPrivate = false;

            BroadcastInternal(
                messageDto.Sender,
                messageDto.LobbyCode,
                messageDto
            );
        }

        public void SendPrivateMessage(ChatMessageDto messageDto)
        {
            if (messageDto == null || string.IsNullOrEmpty(messageDto.TargetUser))
                return;

            messageDto.IsPrivate = true;

            if (_notifier.IsUserConnected(messageDto.TargetUser))
            {
                SafeSendMessageToClient(messageDto.TargetUser, messageDto);
                SafeSendMessageToClient(messageDto.Sender, messageDto);
            }
            else
            {
                Log.Warn($"[PrivateChat] Usuario destino no encontrado: {messageDto.TargetUser}");
            }
        }

        public void LeaveChat(JoinChatRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.LobbyCode))
                return;

            RemoveClient(request.LobbyCode, request.Username);

            BroadcastSystemMessage(
                request.LobbyCode,
                $"{request.Username} ha salido."
            );
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

        private void BroadcastInternal(
            string senderUsername,
            string lobbyCode,
            ChatMessageDto messageDto)
        {
            if (_lobbies.TryGetValue(lobbyCode, out var lobbyChatters))
            {
                foreach (var userKey in lobbyChatters.Keys.ToList())
                {
                    if (senderUsername == "SYSTEM" ||
                        !userKey.Equals(senderUsername, StringComparison.OrdinalIgnoreCase))
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
                Log.Warn($"[Chat] Timeout enviando a {userKey}", ex);
            }
            catch (CommunicationException ex)
            {
                Log.Warn($"[Chat] Comunicación interrumpida con {userKey}", ex);
            }
            catch (ObjectDisposedException ex)
            {
                Log.Warn($"[Chat] Canal ya eliminado para {userKey}", ex);
            }
        }

        public void RemoveClient(string lobbyCode, string username)
        {
            if (!string.IsNullOrEmpty(lobbyCode) &&
                _lobbies.TryGetValue(lobbyCode, out var lobbyChatters))
            {
                lobbyChatters.TryRemove(username, out _);
                WarningTracker.Reset(lobbyCode, username);
            }
        }
    }
}
