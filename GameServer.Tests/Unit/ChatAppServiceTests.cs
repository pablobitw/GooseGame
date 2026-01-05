using GameServer.Interfaces;
using GameServer.DTOs.Chat;
using GameServer.Services.Logic;
using Moq;
using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.ServiceModel;
using Xunit;

namespace GameServer.Tests.Services
{
    public class ChatAppServiceTests : IDisposable
    {
        private readonly Mock<IChatCallback> _mockCallbackA;
        private readonly Mock<IChatCallback> _mockCallbackB;
        private readonly ChatAppService _chatService;

        private readonly string _lobbyCode = "LobbyTest1";
        private readonly string _userA = "UserA";
        private readonly string _userB = "UserB";

        public ChatAppServiceTests()
        {
            _chatService = new ChatAppService();
            _mockCallbackA = new Mock<IChatCallback>();
            _mockCallbackB = new Mock<IChatCallback>();
            ResetStaticData();
        }

        public void Dispose()
        {
            ResetStaticData();
        }

        private void ResetStaticData()
        {
            var lobbiesField = typeof(ChatAppService).GetField("_lobbies", BindingFlags.NonPublic | BindingFlags.Static);
            if (lobbiesField != null)
            {
                var lobbies = (ConcurrentDictionary<string, ConcurrentDictionary<string, string>>)lobbiesField.GetValue(null);
                lobbies?.Clear();
            }

            var callbacksField = typeof(ChatAppService).GetField("_callbacks", BindingFlags.NonPublic | BindingFlags.Static);
            if (callbacksField != null)
            {
                var callbacks = (ConcurrentDictionary<string, IChatCallback>)callbacksField.GetValue(null);
                callbacks?.Clear();
            }
        }

        [Fact]
        public void SendMessage_LengthExactly100_ShouldSend()
        {
            string uA = "UserL100_" + Guid.NewGuid();
            string uB = "UserL100B_" + Guid.NewGuid();

            _chatService.JoinChat(new JoinChatRequest { Username = uA, LobbyCode = _lobbyCode }, _mockCallbackA.Object);
            _chatService.JoinChat(new JoinChatRequest { Username = uB, LobbyCode = _lobbyCode }, _mockCallbackB.Object);

            _mockCallbackB.Invocations.Clear();

            string msg = new string('a', 100);
            var dto = new ChatMessageDto { LobbyCode = _lobbyCode, Sender = uA, Message = msg };

            // Act
            _chatService.SendMessage(dto);

            // Assert
            _mockCallbackB.Verify(c => c.ReceiveMessage(It.Is<ChatMessageDto>(m => m.Message == msg)), Times.Once);
        }

        [Fact]
        public void SendMessage_Length101_ShouldNotBroadcast_AndWarnSender()
        {
            // Arrange
            _chatService.JoinChat(new JoinChatRequest { Username = _userA, LobbyCode = _lobbyCode }, _mockCallbackA.Object);
            _chatService.JoinChat(new JoinChatRequest { Username = _userB, LobbyCode = _lobbyCode }, _mockCallbackB.Object);

            _mockCallbackA.Invocations.Clear();
            _mockCallbackB.Invocations.Clear();

            string msg = new string('a', 101);
            var dto = new ChatMessageDto { LobbyCode = _lobbyCode, Sender = _userA, Message = msg };

            // Act
            _chatService.SendMessage(dto);

            // Assert
            _mockCallbackB.Verify(c => c.ReceiveMessage(It.IsAny<ChatMessageDto>()), Times.Never);
            _mockCallbackA.Verify(c => c.ReceiveMessage(It.Is<ChatMessageDto>(m => m.Sender == "SYSTEM" && m.Message.Contains("largo"))), Times.Once);
        }

        [Fact]
        public void JoinChat_NullRequest_ShouldDoNothing()
        {
            _chatService.JoinChat(null, _mockCallbackA.Object);
            _mockCallbackA.Verify(c => c.ReceiveMessage(It.IsAny<ChatMessageDto>()), Times.Never);
        }

        [Fact]
        public void JoinChat_EmptyUsername_ShouldDoNothing()
        {
            _chatService.JoinChat(new JoinChatRequest { Username = "", LobbyCode = _lobbyCode }, _mockCallbackA.Object);
            _mockCallbackA.Verify(c => c.ReceiveMessage(It.IsAny<ChatMessageDto>()), Times.Never);
        }

        [Fact]
        public void SendMessage_NullDto_ShouldReturn()
        {
            _chatService.SendMessage(null);
            _mockCallbackA.Verify(c => c.ReceiveMessage(It.IsAny<ChatMessageDto>()), Times.Never);
        }

        [Fact]
        public void SendMessage_EmptyMessage_ShouldDoNothing()
        {
            // Arrange
            _chatService.JoinChat(new JoinChatRequest { Username = _userA, LobbyCode = _lobbyCode }, _mockCallbackA.Object);

            _mockCallbackA.Invocations.Clear();

            var dto = new ChatMessageDto { LobbyCode = _lobbyCode, Sender = _userA, Message = "   " };

            // Act
            _chatService.SendMessage(dto);

            // Assert
            _mockCallbackA.Verify(c => c.ReceiveMessage(It.IsAny<ChatMessageDto>()), Times.Never);
        }

        [Fact]
        public void SendPrivateMessage_NullTarget_ShouldReturn()
        {
            var dto = new ChatMessageDto { Sender = _userA, TargetUser = null, Message = "Hi" };
            _chatService.SendPrivateMessage(dto);
            _mockCallbackA.Verify(c => c.ReceiveMessage(It.IsAny<ChatMessageDto>()), Times.Never);
        }

        [Fact]
        public void JoinChat_ValidUser_ShouldNotifyLobby()
        {
            // Arrange
            string uniqueB = "UserB_" + Guid.NewGuid();
            _chatService.JoinChat(new JoinChatRequest { Username = uniqueB, LobbyCode = _lobbyCode }, _mockCallbackB.Object);
            _mockCallbackB.Invocations.Clear();

            string uniqueA = "UserA_" + Guid.NewGuid();
            var request = new JoinChatRequest { Username = uniqueA, LobbyCode = _lobbyCode };

            // Act
            _chatService.JoinChat(request, _mockCallbackA.Object);

            // Assert 
            _mockCallbackB.Verify(c => c.ReceiveMessage(It.Is<ChatMessageDto>(m => m.Message.Contains(uniqueA) && m.Sender == "SYSTEM")), Times.Once);
        }



        [Fact]
        public void SendPrivateMessage_TargetOnline_ShouldSendToTarget_AndSender()
        {
            _chatService.JoinChat(new JoinChatRequest { Username = _userA, LobbyCode = _lobbyCode }, _mockCallbackA.Object);
            _chatService.JoinChat(new JoinChatRequest { Username = _userB, LobbyCode = _lobbyCode }, _mockCallbackB.Object);
            _mockCallbackA.Invocations.Clear();
            _mockCallbackB.Invocations.Clear();

            var dto = new ChatMessageDto { Sender = _userA, TargetUser = _userB, Message = "Psst" };

            // Act
            _chatService.SendPrivateMessage(dto);

            // Assert
            _mockCallbackB.Verify(c => c.ReceiveMessage(It.Is<ChatMessageDto>(m => m.IsPrivate == true && m.Message == "Psst")), Times.Once);
            _mockCallbackA.Verify(c => c.ReceiveMessage(It.Is<ChatMessageDto>(m => m.IsPrivate == true && m.Message == "Psst")), Times.Once);
        }

        [Fact]
        public void LeaveChat_UserInLobby_ShouldBroadcastLeaveMessage()
        {
            _chatService.JoinChat(new JoinChatRequest { Username = _userA, LobbyCode = _lobbyCode }, _mockCallbackA.Object);
            _chatService.JoinChat(new JoinChatRequest { Username = _userB, LobbyCode = _lobbyCode }, _mockCallbackB.Object);
            _mockCallbackB.Invocations.Clear();

            var request = new JoinChatRequest { Username = _userA, LobbyCode = _lobbyCode };

            // Act
            _chatService.LeaveChat(request);

            // Assert
            _mockCallbackB.Verify(c => c.ReceiveMessage(It.Is<ChatMessageDto>(m => m.Message.Contains("ha salido") && m.Sender == "SYSTEM")), Times.Once);
        }

        [Fact]
        public void SendPrivateMessage_TargetOffline_ShouldWarnSender()
        {
            _chatService.JoinChat(new JoinChatRequest { Username = _userA, LobbyCode = _lobbyCode }, _mockCallbackA.Object);
            _mockCallbackA.Invocations.Clear();

            var dto = new ChatMessageDto { Sender = _userA, TargetUser = _userB, Message = "Hello?" };

            // Act
            _chatService.SendPrivateMessage(dto);

            // Assert
            _mockCallbackA.Verify(c => c.ReceiveMessage(It.Is<ChatMessageDto>(m => m.Sender == "SYSTEM" && m.Message.Contains("no está disponible"))), Times.Once);
        }

        [Fact]
        public void JoinChat_CallbackThrowTimeout_ShouldCatchAndNotCrash()
        {
            _mockCallbackA.Setup(c => c.ReceiveMessage(It.IsAny<ChatMessageDto>())).Throws(new TimeoutException());
            _chatService.JoinChat(new JoinChatRequest { Username = _userA, LobbyCode = _lobbyCode }, _mockCallbackA.Object);

            // Act
            var dto = new ChatMessageDto { LobbyCode = _lobbyCode, Sender = "SYSTEM", Message = "Welcome" };
            var exception = Record.Exception(() => _chatService.SendMessage(dto));

            // Assert
            Assert.Null(exception);
        }
    }
}