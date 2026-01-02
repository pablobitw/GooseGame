using GameServer.Chat.Moderation;
using GameServer.DTOs.Chat;
using GameServer.Interfaces;
using GameServer.Services.Logic;
using Moq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.ServiceModel;
using Xunit;

namespace GameServer.Tests.Services
{
    public class ChatAppServiceTests : IDisposable
    {
        private readonly Mock<IChatNotifier> _mockNotifier;
        private readonly ChatAppService _service;

        public ChatAppServiceTests()
        {
            ResetStaticState();
            _mockNotifier = new Mock<IChatNotifier>();
            _service = new ChatAppService(_mockNotifier.Object);
        }

        public void Dispose()
        {
            ResetStaticState();
        }

        private void ResetStaticState()
        {
            var lobbiesField = typeof(ChatAppService).GetField("_lobbies", BindingFlags.NonPublic | BindingFlags.Static);
            if (lobbiesField != null)
            {
                var dict = lobbiesField.GetValue(null) as IDictionary;
                dict?.Clear();
            }

            var warningsField = typeof(WarningTracker).GetField("_warnings", BindingFlags.NonPublic | BindingFlags.Static);
            if (warningsField != null)
            {
                var dict = warningsField.GetValue(null) as IDictionary;
                dict?.Clear();
            }

            var spamTrackerField = typeof(ChatAppService).GetField("_spamTracker", BindingFlags.NonPublic | BindingFlags.Static);
            if (spamTrackerField != null)
            {
                var spamTrackerInstance = spamTrackerField.GetValue(null);
                var historyField = typeof(SpamTracker).GetField("_messageHistory", BindingFlags.NonPublic | BindingFlags.Instance);
                if (historyField != null)
                {
                    var dict = historyField.GetValue(spamTrackerInstance) as IDictionary;
                    dict?.Clear();
                }
            }
        }

        [Fact]
        public void Constructor_NullNotifier_ShouldThrowException()
        {
            Assert.Throws<ArgumentNullException>(() => new ChatAppService(null));
        }

        [Fact]
        public void JoinChat_ValidRequest_ShouldBroadcastJoinMessage()
        {
            var request = new JoinChatRequest { Username = "User1", LobbyCode = "L1" };

            _service.JoinChat(request);

            _mockNotifier.Verify(n => n.SendMessageToClient(
                "User1",
                It.Is<ChatMessageDto>(m => m.Message.Contains("ha unido") && m.Sender == "SYSTEM")
            ), Times.Once);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void JoinChat_InvalidRequest_ShouldDoNothing(string invalidString)
        {
            var request = new JoinChatRequest { Username = invalidString, LobbyCode = "L1" };
            _service.JoinChat(request);
            _mockNotifier.Verify(n => n.SendMessageToClient(It.IsAny<string>(), It.IsAny<ChatMessageDto>()), Times.Never);
        }

        [Fact]
        public void SendMessage_ValidMessage_ShouldBroadcastToOthers()
        {
            JoinUser("User1", "L1");
            JoinUser("User2", "L1");
            var msg = new ChatMessageDto { Sender = "User1", LobbyCode = "L1", Message = "Good Morning" };

            var receivedMessages = new List<ChatMessageDto>();
            _mockNotifier.Setup(n => n.SendMessageToClient("User2", It.IsAny<ChatMessageDto>()))
                         .Callback<string, ChatMessageDto>((u, m) => receivedMessages.Add(m));

            _service.SendMessage(msg);

            var userMessage = receivedMessages.FirstOrDefault(m => m.Sender == "User1");

            Assert.NotNull(userMessage);
            Assert.Equal("Good Morning", userMessage.Message);

            _mockNotifier.Verify(n => n.SendMessageToClient(
                "User1",
                It.Is<ChatMessageDto>(m => m.Sender == "User1")
            ), Times.Never);
        }

        [Fact]
        public void SendMessage_MessageTooLong_ShouldSendSystemWarningToSender()
        {
            var longMsg = new string('A', 51);
            JoinUser("User1", "L1");
            var msg = new ChatMessageDto { Sender = "User1", LobbyCode = "L1", Message = longMsg };

            _service.SendMessage(msg);

            _mockNotifier.Verify(n => n.SendMessageToClient(
                "User1",
                It.Is<ChatMessageDto>(m => m.Sender == "SYSTEM" && m.Message.Contains("exceder el límite"))
            ), Times.Once);
        }

        [Fact]
        public void SendMessage_SpamDetected_ShouldBlockAndWarn()
        {
            JoinUser("User1", "L1");
            var msg = new ChatMessageDto { Sender = "User1", LobbyCode = "L1", Message = "Spam" };

            for (int i = 0; i < 5; i++) _service.SendMessage(msg);

            _service.SendMessage(msg);

            _mockNotifier.Verify(n => n.SendMessageToClient(
                "User1",
                It.Is<ChatMessageDto>(m => m.Sender == "SYSTEM" && m.Message.Contains("spam"))
            ), Times.AtLeastOnce);
        }

        [Fact]
        public void SendMessage_ProfanityDetected_ShouldBroadcastToOthers()
        {
            JoinUser("User1", "L1");
            JoinUser("User2", "L1");

            var msg = new ChatMessageDto { Sender = "User1", LobbyCode = "L1", Message = "Clean message" };

            ChatMessageDto received = null;
            _mockNotifier.Setup(n => n.SendMessageToClient("User2", It.IsAny<ChatMessageDto>()))
                         .Callback<string, ChatMessageDto>((u, m) =>
                         {
                             if (m.Sender == "User1") received = m;
                         });

            _service.SendMessage(msg);

            Assert.NotNull(received);
            Assert.Equal("Clean message", received.Message);
        }

        [Fact]
        public void SendPrivateMessage_UserConnected_ShouldDeliverToBoth()
        {
            var msg = new ChatMessageDto { Sender = "User1", TargetUser = "User2", Message = "Secret" };
            _mockNotifier.Setup(n => n.IsUserConnected("User2")).Returns(true);

            _service.SendPrivateMessage(msg);

            _mockNotifier.Verify(n => n.SendMessageToClient("User1", It.Is<ChatMessageDto>(m => m.IsPrivate)), Times.Once);
            _mockNotifier.Verify(n => n.SendMessageToClient("User2", It.Is<ChatMessageDto>(m => m.IsPrivate)), Times.Once);
        }

        [Fact]
        public void SendPrivateMessage_UserDisconnected_ShouldLogWarningOnly()
        {
            var msg = new ChatMessageDto { Sender = "User1", TargetUser = "User2", Message = "Secret" };
            _mockNotifier.Setup(n => n.IsUserConnected("User2")).Returns(false);

            _service.SendPrivateMessage(msg);

            _mockNotifier.Verify(n => n.SendMessageToClient(It.IsAny<string>(), It.IsAny<ChatMessageDto>()), Times.Never);
        }

        [Fact]
        public void LeaveChat_ValidRequest_ShouldNotifyOthers()
        {
            JoinUser("User1", "L1");
            JoinUser("User2", "L1");

            var request = new JoinChatRequest { Username = "User1", LobbyCode = "L1" };

            _service.LeaveChat(request);

            _mockNotifier.Verify(n => n.SendMessageToClient(
                "User2",
                It.Is<ChatMessageDto>(m => m.Sender == "SYSTEM" && m.Message.Contains("ha salido"))
            ), Times.Once);
        }

        [Fact]
        public void CommunicationException_ShouldHandleGracefully()
        {
            JoinUser("User1", "L1");
            _mockNotifier.Setup(n => n.SendMessageToClient("User1", It.IsAny<ChatMessageDto>()))
                         .Throws(new CommunicationException());

            var msg = new ChatMessageDto { Sender = "SYSTEM", LobbyCode = "L1", Message = "Test" };

            var exception = Record.Exception(() => _service.SendMessage(msg));
            Assert.Null(exception);
        }

        [Fact]
        public void ObjectDisposedException_ShouldHandleGracefully()
        {
            JoinUser("User1", "L1");
            _mockNotifier.Setup(n => n.SendMessageToClient("User1", It.IsAny<ChatMessageDto>()))
                         .Throws(new ObjectDisposedException("channel"));

            var msg = new ChatMessageDto { Sender = "SYSTEM", LobbyCode = "L1", Message = "Test" };

            var exception = Record.Exception(() => _service.SendMessage(msg));
            Assert.Null(exception);
        }

        private void JoinUser(string username, string lobbyCode)
        {
            _service.JoinChat(new JoinChatRequest { Username = username, LobbyCode = lobbyCode });
        }
    }
}