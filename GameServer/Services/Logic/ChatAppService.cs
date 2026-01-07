using GameServer.Chat.Moderation;
using GameServer.DTOs.Chat;
using GameServer.Interfaces;
using GameServer.Repositories;
using GameServer.Services.Logic;
using log4net;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;

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

        public ChatOperationResult JoinChat(JoinChatRequest request, IChatCallback callback)
        {
            try
            {
                if (request == null || string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.LobbyCode))
                {
                    return ChatOperationResult.InternalError;
                }

                _callbacks.AddOrUpdate(request.Username, callback, (k, v) => callback);

                var lobbyChatters = _lobbies.GetOrAdd(request.LobbyCode, _ => new ConcurrentDictionary<string, string>());
                lobbyChatters[request.Username] = request.Username;

                Log.Info($"Chat: {request.Username} se unió al lobby {request.LobbyCode}");

                Task.Run(() => BroadcastSystemMessage(request.LobbyCode, $"{request.Username} se ha unido."));

                return ChatOperationResult.Success;
            }
            catch (Exception ex)
            {
                Log.Error("Error en JoinChat", ex);
                return ChatOperationResult.InternalError;
            }
        }

        public ChatOperationResult SendMessage(ChatMessageDto messageDto)
        {
            try
            {
                if (messageDto == null || string.IsNullOrEmpty(messageDto.LobbyCode) || string.IsNullOrWhiteSpace(messageDto.Message))
                {
                    return ChatOperationResult.InternalError;
                }

                if (messageDto.Message.Length > MaxMessageLength)
                {
                    Task.Run(() => SendSystemMessageToUser(messageDto.Sender, "Mensaje demasiado largo."));
                    return ChatOperationResult.MessageTooLong;
                }

                var spamResult = _spamTracker.Analyze(messageDto.LobbyCode, messageDto.Sender);
                if (!spamResult.IsAllowed)
                {
                    Task.Run(() => {
                        SendSystemMessageToUser(messageDto.Sender, spamResult.SystemNotification);
                        if (spamResult.RequiresKick)
                        {
                            KickUserForChatOffense(messageDto.LobbyCode, messageDto.Sender, "Spam masivo", "CHAT_SPAM");
                        }
                    });
                    return ChatOperationResult.SpamBlocked;
                }

                var profanityResult = ProfanityFilter.Analyze(messageDto.Message);
                if (profanityResult.IsBlocked)
                {
                    Task.Run(() => SendSystemMessageToUser(messageDto.Sender, profanityResult.SystemNotification));
                    return ChatOperationResult.ContentBlocked;
                }

                if (profanityResult.IsCensored)
                {
                    var level = WarningTracker.RegisterWarning(messageDto.LobbyCode, messageDto.Sender);
                    Task.Run(() => HandleWarning(messageDto.LobbyCode, messageDto.Sender, level));
                    if (level == WarningLevel.Punishment)
                    {
                        return ChatOperationResult.ContentBlocked;
                    }
                }

                messageDto.Message = profanityResult.FinalMessage;
                messageDto.IsPrivate = false;

                Task.Run(() => BroadcastInternal(messageDto.Sender, messageDto.LobbyCode, messageDto));

                return ChatOperationResult.Success;
            }
            catch (Exception ex)
            {
                Log.Error("Error en SendMessage", ex);
                return ChatOperationResult.InternalError;
            }
        }

        public ChatOperationResult SendPrivateMessage(ChatMessageDto messageDto)
        {
            try
            {
                if (messageDto == null || string.IsNullOrEmpty(messageDto.TargetUser))
                {
                    return ChatOperationResult.InternalError;
                }

                messageDto.IsPrivate = true;

                bool sent = SafeSendMessageToClient(messageDto.TargetUser, messageDto);

                if (sent)
                {
                    Task.Run(() => SafeSendMessageToClient(messageDto.Sender, messageDto));
                    return ChatOperationResult.Success;
                }
                else
                {
                    Task.Run(() => SendSystemMessageToUser(messageDto.Sender, $"El usuario {messageDto.TargetUser} no está disponible."));
                    return ChatOperationResult.TargetNotFound;
                }
            }
            catch (Exception ex)
            {
                Log.Error("Error en SendPrivateMessage", ex);
                return ChatOperationResult.InternalError;
            }
        }

        public ChatOperationResult LeaveChat(JoinChatRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrEmpty(request.LobbyCode))
                {
                    return ChatOperationResult.InternalError;
                }

                RemoveClient(request.LobbyCode, request.Username);
                Task.Run(() => BroadcastSystemMessage(request.LobbyCode, $"{request.Username} ha salido."));
                return ChatOperationResult.Success;
            }
            catch (Exception ex)
            {
                Log.Error("Error en LeaveChat", ex);
                return ChatOperationResult.InternalError;
            }
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

                    using (var lobbyRepo = new LobbyRepository())
                    {
                        var lobbyService = new LobbyAppService(lobbyRepo);
                        await lobbyService.SystemKickPlayerAsync(lobbyCode, username, reason);
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