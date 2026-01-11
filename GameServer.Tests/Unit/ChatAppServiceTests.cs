#nullable disable
using GameServer.Chat.Moderation;
using GameServer.DTOs.Chat;
using GameServer.Helpers;
using GameServer.Interfaces;
using GameServer.Services.Logic;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GameServer.Services.Common;
using Xunit;
using IChatSessionManager = GameServer.Helpers.IChatSessionManager;

namespace GameServer.Tests.Unit
{
    public sealed class ChatAppServiceTests
    {
        private readonly Mock<IChatSessionManager> _sessionMock;
        private readonly Mock<IChatServiceFactory> _factoryMock;
        private readonly Mock<IChatSpamTracker> _spamMock;
        private readonly Mock<IChatProfanityFilter> _profanityMock;
        private readonly Mock<IChatWarningTracker> _warningMock;
        private readonly Mock<IChatCallback> _callbackMock;
        private readonly Mock<ISanctionService> _sanctionMock;
        private readonly Mock<ILobbyServiceWrapper> _lobbyServiceMock;

        private readonly ChatAppService _service;

        private const string LOBBY = "Lobby1";
        private const string USER = "Player1";
        private const string USER2 = "Player2";

        public ChatAppServiceTests()
        {
            _sessionMock = new Mock<IChatSessionManager>();
            _factoryMock = new Mock<IChatServiceFactory>();
            _spamMock = new Mock<IChatSpamTracker>();
            _profanityMock = new Mock<IChatProfanityFilter>();
            _warningMock = new Mock<IChatWarningTracker>();
            _callbackMock = new Mock<IChatCallback>();
            _sanctionMock = new Mock<ISanctionService>();
            _lobbyServiceMock = new Mock<ILobbyServiceWrapper>();

            _factoryMock.Setup(f => f.CreateSanctionService()).Returns(_sanctionMock.Object);
            _factoryMock.Setup(f => f.CreateLobbyService()).Returns(_lobbyServiceMock.Object);

            _spamMock.Setup(s => s.Analyze(It.IsAny<string>(), It.IsAny<string>()))
                     .Returns(ChatModerationResult.Allowed("msg"));

            _profanityMock.Setup(p => p.Analyze(It.IsAny<string>()))
                          .Returns(ChatModerationResult.Allowed("msg"));

            _service = new ChatAppService(
                _sessionMock.Object,
                _factoryMock.Object,
                _spamMock.Object,
                _profanityMock.Object,
                _warningMock.Object
            );
        }

        [Fact]
        public void JoinChat_NullRequest_ReturnsInternalError()
        {
            var res = _service.JoinChat(null, _callbackMock.Object);
            Assert.Equal(ChatOperationResult.InternalError, res);
        }

        [Theory]
        [InlineData(null, LOBBY)]
        [InlineData("", LOBBY)]
        [InlineData(USER, null)]
        [InlineData(USER, "")]
        public void JoinChat_InvalidData_ReturnsInternalError(string u, string l)
        {
            var req = new JoinChatRequest { Username = u, LobbyCode = l };
            var res = _service.JoinChat(req, _callbackMock.Object);
            Assert.Equal(ChatOperationResult.InternalError, res);
        }

        [Fact]
        public async Task JoinChat_Success_RegistersAndBroadcasts()
        {
            var req = new JoinChatRequest { Username = USER, LobbyCode = LOBBY };
            _sessionMock.Setup(s => s.GetLobbyParticipants(LOBBY)).Returns(new List<string> { USER, USER2 });
            _sessionMock.Setup(s => s.GetClientCallback(USER2)).Returns(_callbackMock.Object);

            var res = _service.JoinChat(req, _callbackMock.Object);

            Assert.Equal(ChatOperationResult.Success, res);
            _sessionMock.Verify(s => s.RegisterClient(USER, _callbackMock.Object), Times.Once);
            _sessionMock.Verify(s => s.AddUserToLobby(LOBBY, USER), Times.Once);

            await Task.Delay(50);
            _callbackMock.Verify(c => c.ReceiveMessage(It.Is<ChatMessageDto>(m => m.Sender == "SYSTEM")), Times.AtLeastOnce);
        }

        [Fact]
        public void JoinChat_Exception_ReturnsInternalError()
        {
            var req = new JoinChatRequest { Username = USER, LobbyCode = LOBBY };
            _sessionMock.Setup(s => s.RegisterClient(It.IsAny<string>(), It.IsAny<IChatCallback>())).Throws(new Exception());

            var res = _service.JoinChat(req, _callbackMock.Object);

            Assert.Equal(ChatOperationResult.InternalError, res);
        }

        [Fact]
        public void SendMessage_NullDto_ReturnsInternalError()
        {
            var res = _service.SendMessage(null);
            Assert.Equal(ChatOperationResult.InternalError, res);
        }

        [Theory]
        [InlineData(null, "msg")]
        [InlineData("", "msg")]
        [InlineData(LOBBY, null)]
        [InlineData(LOBBY, "   ")]
        public void SendMessage_InvalidData_ReturnsInternalError(string l, string m)
        {
            var dto = new ChatMessageDto { LobbyCode = l, Message = m, Sender = USER };
            var res = _service.SendMessage(dto);
            Assert.Equal(ChatOperationResult.InternalError, res);
        }

        [Fact]
        public async Task SendMessage_TooLong_ReturnsMessageTooLong()
        {
            var longMsg = new string('a', 101);
            var dto = new ChatMessageDto { LobbyCode = LOBBY, Message = longMsg, Sender = USER };

            _sessionMock.Setup(s => s.GetClientCallback(USER)).Returns(_callbackMock.Object);

            var res = _service.SendMessage(dto);

            Assert.Equal(ChatOperationResult.MessageTooLong, res);

            await Task.Delay(50);
            _callbackMock.Verify(c => c.ReceiveMessage(It.Is<ChatMessageDto>(m => m.Sender == "SYSTEM")), Times.Once);
        }

        [Fact]
        public async Task SendMessage_SpamBlocked_ReturnsSpamBlocked()
        {
            var dto = new ChatMessageDto { LobbyCode = LOBBY, Message = "spam", Sender = USER };
            var spamRes = ChatModerationResult.Blocked("Stop spamming");

            _spamMock.Setup(s => s.Analyze(LOBBY, USER)).Returns(spamRes);
            _sessionMock.Setup(s => s.GetClientCallback(USER)).Returns(_callbackMock.Object);

            var res = _service.SendMessage(dto);

            Assert.Equal(ChatOperationResult.SpamBlocked, res);

            await Task.Delay(50);
            _callbackMock.Verify(c => c.ReceiveMessage(It.Is<ChatMessageDto>(m => m.Message == "Stop spamming")), Times.Once);
        }

        [Fact]
        public async Task SendMessage_SpamKick_KicksUser()
        {
            var dto = new ChatMessageDto { LobbyCode = LOBBY, Message = "MEGA SPAM", Sender = USER };
            var spamRes = ChatModerationResult.Kick("Banned for spam");

            _spamMock.Setup(s => s.Analyze(LOBBY, USER)).Returns(spamRes);
            _sessionMock.Setup(s => s.GetClientCallback(USER)).Returns(_callbackMock.Object);

            var res = _service.SendMessage(dto);

            Assert.Equal(ChatOperationResult.SpamBlocked, res);

            await Task.Delay(100);
            _sanctionMock.Verify(s => s.ProcessKickAsync(USER, LOBBY, It.IsAny<string>(), "CHAT_SPAM"), Times.Once);
            _sessionMock.Verify(s => s.RemoveUserFromLobby(LOBBY, USER), Times.Once);
        }

        [Fact]
        public async Task SendMessage_ProfanityBlocked_ReturnsContentBlocked()
        {
            var dto = new ChatMessageDto { LobbyCode = LOBBY, Message = "puto", Sender = USER };
            var profRes = ChatModerationResult.Blocked("Lenguaje inapropiado detectado");

            _spamMock.Setup(s => s.Analyze(LOBBY, USER)).Returns(ChatModerationResult.Allowed("ok"));

            _profanityMock.Setup(p => p.Analyze("puto")).Returns(profRes);

            _sessionMock.Setup(s => s.GetClientCallback(USER)).Returns(_callbackMock.Object);

            var res = _service.SendMessage(dto);

            Assert.Equal(ChatOperationResult.ContentBlocked, res);

            await Task.Delay(50);
            _callbackMock.Verify(c => c.ReceiveMessage(It.Is<ChatMessageDto>(m => m.Message == "Lenguaje inapropiado detectado")), Times.Once);
        }

        [Fact]
        public async Task SendMessage_ProfanityCensored_Warning_SendsMessage()
        {
            var dto = new ChatMessageDto { LobbyCode = LOBBY, Message = "damn", Sender = USER };
            var profRes = ChatModerationResult.Censored("****");

            _spamMock.Setup(s => s.Analyze(LOBBY, USER)).Returns(ChatModerationResult.Allowed("ok"));
            _profanityMock.Setup(p => p.Analyze("damn")).Returns(profRes);
            _warningMock.Setup(w => w.RegisterWarning(LOBBY, USER)).Returns(WarningLevel.Warning);

            _sessionMock.Setup(s => s.GetLobbyParticipants(LOBBY)).Returns(new List<string> { USER, USER2 });
            _sessionMock.Setup(s => s.GetClientCallback(USER)).Returns(_callbackMock.Object);
            _sessionMock.Setup(s => s.GetClientCallback(USER2)).Returns(new Mock<IChatCallback>().Object);

            var res = _service.SendMessage(dto);

            Assert.Equal(ChatOperationResult.Success, res);

            await Task.Delay(50);
            _callbackMock.Verify(c => c.ReceiveMessage(It.Is<ChatMessageDto>(m => m.Message == "Advertencia: Lenguaje inapropiado.")), Times.Once);
            _sessionMock.Verify(s => s.GetClientCallback(USER2), Times.Once);
        }

        [Fact]
        public async Task SendMessage_ProfanityCensored_Punishment_KicksUser()
        {
            var dto = new ChatMessageDto { LobbyCode = LOBBY, Message = "bad", Sender = USER };
            var profRes = ChatModerationResult.Censored("***");

            _spamMock.Setup(s => s.Analyze(LOBBY, USER)).Returns(ChatModerationResult.Allowed("ok"));
            _profanityMock.Setup(p => p.Analyze("bad")).Returns(profRes);
            _warningMock.Setup(w => w.RegisterWarning(LOBBY, USER)).Returns(WarningLevel.Punishment);
            _sessionMock.Setup(s => s.GetLobbyParticipants(LOBBY)).Returns(new List<string> { USER });
            _sessionMock.Setup(s => s.GetClientCallback(USER)).Returns(_callbackMock.Object);

            var res = _service.SendMessage(dto);

            Assert.Equal(ChatOperationResult.ContentBlocked, res);

            await Task.Delay(100);
            _sanctionMock.Verify(s => s.ProcessKickAsync(USER, LOBBY, "Toxicidad", "CHAT_TOXICITY"), Times.Once);
            _sessionMock.Verify(s => s.RemoveUserFromLobby(LOBBY, USER), Times.Once);
        }

        [Fact]
        public async Task SendMessage_Success_BroadcastsMessage()
        {
            var dto = new ChatMessageDto { LobbyCode = LOBBY, Message = "Hello", Sender = USER };

            _profanityMock.Setup(p => p.Analyze("Hello"))
                          .Returns(ChatModerationResult.Allowed("Hello"));

            _sessionMock.Setup(s => s.GetLobbyParticipants(LOBBY)).Returns(new List<string> { USER, USER2 });
            _sessionMock.Setup(s => s.GetClientCallback(USER)).Returns(_callbackMock.Object);
            var cb2 = new Mock<IChatCallback>();
            _sessionMock.Setup(s => s.GetClientCallback(USER2)).Returns(cb2.Object);

            var res = _service.SendMessage(dto);

            Assert.Equal(ChatOperationResult.Success, res);

            await Task.Delay(50);
            cb2.Verify(c => c.ReceiveMessage(It.Is<ChatMessageDto>(m => m.Message == "Hello" && m.Sender == USER)), Times.Once);
            _callbackMock.Verify(c => c.ReceiveMessage(It.IsAny<ChatMessageDto>()), Times.Never);
        }

        [Fact]
        public void SendMessage_Exception_ReturnsInternalError()
        {
            var dto = new ChatMessageDto { LobbyCode = LOBBY, Message = "Hi", Sender = USER };
            _spamMock.Setup(s => s.Analyze(It.IsAny<string>(), It.IsAny<string>())).Throws(new Exception());

            var res = _service.SendMessage(dto);

            Assert.Equal(ChatOperationResult.InternalError, res);
        }

        [Fact]
        public void SendPrivate_NullDto_ReturnsInternalError()
        {
            var res = _service.SendPrivateMessage(null);
            Assert.Equal(ChatOperationResult.InternalError, res);
        }

        [Fact]
        public void SendPrivate_NoTarget_ReturnsInternalError()
        {
            var dto = new ChatMessageDto { TargetUser = "", Message = "Hi" };
            var res = _service.SendPrivateMessage(dto);
            Assert.Equal(ChatOperationResult.InternalError, res);
        }

        [Fact]
        public async Task SendPrivate_TargetNotFound_ReturnsTargetNotFound()
        {
            var dto = new ChatMessageDto { Sender = USER, TargetUser = "Ghost", Message = "Hi" };
            _sessionMock.Setup(s => s.GetClientCallback("Ghost")).Returns((IChatCallback)null);
            _sessionMock.Setup(s => s.GetClientCallback(USER)).Returns(_callbackMock.Object);

            var res = _service.SendPrivateMessage(dto);

            Assert.Equal(ChatOperationResult.TargetNotFound, res);

            await Task.Delay(50);
            _callbackMock.Verify(c => c.ReceiveMessage(It.Is<ChatMessageDto>(m => m.Sender == "SYSTEM" && m.Message.Contains("Ghost"))), Times.Once);
        }

        [Fact]
        public async Task SendPrivate_Success_SendsToBoth()
        {
            var dto = new ChatMessageDto { Sender = USER, TargetUser = USER2, Message = "Secret" };
            var cb2 = new Mock<IChatCallback>();

            _sessionMock.Setup(s => s.GetClientCallback(USER2)).Returns(cb2.Object);
            _sessionMock.Setup(s => s.GetClientCallback(USER)).Returns(_callbackMock.Object);

            var res = _service.SendPrivateMessage(dto);

            Assert.Equal(ChatOperationResult.Success, res);

            await Task.Delay(50);
            cb2.Verify(c => c.ReceiveMessage(It.Is<ChatMessageDto>(m => m.Message == "Secret" && m.IsPrivate)), Times.Once);
            _callbackMock.Verify(c => c.ReceiveMessage(It.Is<ChatMessageDto>(m => m.Message == "Secret" && m.IsPrivate)), Times.Once);
        }

        [Fact]
        public void SendPrivate_Exception_ReturnsInternalError()
        {
            var dto = new ChatMessageDto { Sender = USER, TargetUser = USER2, Message = "Hi" };
            _sessionMock.Setup(s => s.GetClientCallback(It.IsAny<string>())).Throws(new Exception());

            var res = _service.SendPrivateMessage(dto);

            Assert.Equal(ChatOperationResult.InternalError, res);
        }

        [Fact]
        public void LeaveChat_NullRequest_ReturnsInternalError()
        {
            var res = _service.LeaveChat(null);
            Assert.Equal(ChatOperationResult.InternalError, res);
        }

        [Fact]
        public void LeaveChat_InvalidData_ReturnsInternalError()
        {
            var req = new JoinChatRequest { LobbyCode = "" };
            var res = _service.LeaveChat(req);
            Assert.Equal(ChatOperationResult.InternalError, res);
        }

        [Fact]
        public async Task LeaveChat_Success_RemovesAndBroadcasts()
        {
            var req = new JoinChatRequest { Username = USER, LobbyCode = LOBBY };
            _sessionMock.Setup(s => s.GetLobbyParticipants(LOBBY)).Returns(new List<string> { USER2 });
            var cb2 = new Mock<IChatCallback>();
            _sessionMock.Setup(s => s.GetClientCallback(USER2)).Returns(cb2.Object);

            var res = _service.LeaveChat(req);

            Assert.Equal(ChatOperationResult.Success, res);
            _sessionMock.Verify(s => s.RemoveUserFromLobby(LOBBY, USER), Times.Once);
            _sessionMock.Verify(s => s.UnregisterClient(USER), Times.Once);
            _warningMock.Verify(w => w.Reset(LOBBY, USER), Times.Once);

            await Task.Delay(50);
            cb2.Verify(c => c.ReceiveMessage(It.Is<ChatMessageDto>(m => m.Message.Contains(USER) && m.Sender == "SYSTEM")), Times.Once);
        }

        [Fact]
        public void LeaveChat_Exception_ReturnsInternalError()
        {
            var req = new JoinChatRequest { Username = USER, LobbyCode = LOBBY };
            _sessionMock.Setup(s => s.RemoveUserFromLobby(It.IsAny<string>(), It.IsAny<string>())).Throws(new Exception());

            var res = _service.LeaveChat(req);

            Assert.Equal(ChatOperationResult.InternalError, res);
        }

        [Fact]
        public async Task KickUser_FactoryDisposesServices()
        {
            var dto = new ChatMessageDto { LobbyCode = LOBBY, Message = "KICK ME", Sender = USER };
            var spamRes = ChatModerationResult.Kick("Bye");

            _spamMock.Setup(s => s.Analyze(LOBBY, USER)).Returns(spamRes);
            _sessionMock.Setup(s => s.GetClientCallback(USER)).Returns(_callbackMock.Object);

            _service.SendMessage(dto);

            await Task.Delay(100);
            _sanctionMock.Verify(s => s.Dispose(), Times.Once);
            _lobbyServiceMock.Verify(l => l.Dispose(), Times.Once);
        }

        [Fact]
        public async Task BroadcastInternal_CallbackError_RemovesClient()
        {
            var dto = new ChatMessageDto { LobbyCode = LOBBY, Message = "Hi", Sender = USER };

            _sessionMock.Setup(s => s.GetLobbyParticipants(LOBBY)).Returns(new List<string> { USER2 });
            var cb2 = new Mock<IChatCallback>();
            _sessionMock.Setup(s => s.GetClientCallback(USER2)).Returns(cb2.Object);

            cb2.Setup(c => c.ReceiveMessage(It.IsAny<ChatMessageDto>())).Throws(new Exception());

            _service.SendMessage(dto);

            await Task.Delay(50);
            _sessionMock.Verify(s => s.UnregisterClient(USER2), Times.Once);
        }
    }
}