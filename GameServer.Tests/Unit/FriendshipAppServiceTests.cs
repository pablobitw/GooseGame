using GameServer.Interfaces;     
using GameServer.DTOs.Friendship;
using GameServer.Repositories;   
using GameServer.Services.Logic;
using GameServer;                
using Moq;
using System;
using System.Collections.Generic;
using System.Data.Entity.Core;
using System.Data.Entity.Infrastructure;
using System.Data.SqlClient;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace GameServer.Tests.Services
{
    public class FriendshipAppServiceTests : IDisposable
    {
        private readonly Mock<IFriendshipRepository> _mockRepository;
        private readonly FriendshipAppService _service;
        private readonly Mock<IFriendshipServiceCallback> _mockCallback;

        public FriendshipAppServiceTests()
        {
            _mockRepository = new Mock<IFriendshipRepository>();
            _service = new FriendshipAppService(_mockRepository.Object);
            _mockCallback = new Mock<IFriendshipServiceCallback>();

            ResetStaticData();
        }

        public void Dispose()
        {
            ResetStaticData();
        }

        private void ResetStaticData()
        {
            var field = typeof(FriendshipAppService).GetField("_connectedClients", BindingFlags.NonPublic | BindingFlags.Static);
            if (field != null)
            {
                var dict = (Dictionary<string, IFriendshipServiceCallback>)field.GetValue(null);
                if (dict != null)
                {
                    lock (dict)
                    {
                        dict.Clear();
                    }
                }
            }
        }

        [Fact]
        public void ConnectUser_NewUser_ShouldAddToDictionary()
        {
            // Arrange
            string username = "UserConnect";

            // Act
            _service.ConnectUser(username, _mockCallback.Object);

            // Assert 
            FriendshipAppService.SendGameInvitation(new GameInvitationDto { TargetUsername = username, SenderUsername = "S", LobbyCode = "1" });
            _mockCallback.Verify(c => c.OnGameInvitationReceived(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public void ConnectUser_ExistingUser_ShouldUpdateCallback()
        {
            // Arrange
            string username = "UserUpdate";
            var mockCallback2 = new Mock<IFriendshipServiceCallback>();
            _service.ConnectUser(username, _mockCallback.Object);

            // Act
            _service.ConnectUser(username, mockCallback2.Object); 

            // Assert
            FriendshipAppService.SendGameInvitation(new GameInvitationDto { TargetUsername = username, SenderUsername = "S", LobbyCode = "1" });

            _mockCallback.Verify(c => c.OnGameInvitationReceived(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            mockCallback2.Verify(c => c.OnGameInvitationReceived(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public void DisconnectUser_UserConnected_ShouldRemoveFromDictionary()
        {
            // Arrange
            string username = "UserDisconnect";
            _service.ConnectUser(username, _mockCallback.Object);

            // Act
            _service.DisconnectUser(username);

            // Assert
            FriendshipAppService.SendGameInvitation(new GameInvitationDto { TargetUsername = username, SenderUsername = "S", LobbyCode = "1" });
            _mockCallback.Verify(c => c.OnGameInvitationReceived(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task SendFriendRequest_NullSender_ReturnsError()
        {
            var result = await _service.SendFriendRequestAsync(null, "UserB");
            Assert.Equal(FriendRequestResult.Error, result);
        }

        [Fact]
        public async Task SendFriendRequest_SameUser_ReturnsError()
        {
            var result = await _service.SendFriendRequestAsync("UserA", "UserA");
            Assert.Equal(FriendRequestResult.Error, result);
        }

        [Fact]
        public async Task SendFriendRequest_UserNotFound_ReturnsTargetNotFound()
        {
            // Arrange
            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync("UserA")).ReturnsAsync(new Player());
            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync("UserB")).ReturnsAsync((Player)null);

            // Act
            var result = await _service.SendFriendRequestAsync("UserA", "UserB");

            // Assert
            Assert.Equal(FriendRequestResult.TargetNotFound, result);
        }

        [Fact]
        public async Task SendFriendRequest_GuestUser_ReturnsGuestRestriction()
        {
            // Arrange
            SetupPlayers("GuestA", "UserB", true);

            // Act
            var result = await _service.SendFriendRequestAsync("GuestA", "UserB");

            // Assert
            Assert.Equal(FriendRequestResult.GuestRestriction, result);
        }

        [Fact]
        public async Task SendFriendRequest_AlreadyFriends_ReturnsAlreadyFriends()
        {
            // Arrange
            SetupPlayers("UserA", "UserB");
            var friendship = new Friendship { FriendshipStatus = (int)FriendshipStatus.Accepted };
            _mockRepository.Setup(r => r.GetFriendship(It.IsAny<int>(), It.IsAny<int>())).Returns(friendship);

            // Act
            var result = await _service.SendFriendRequestAsync("UserA", "UserB");

            // Assert
            Assert.Equal(FriendRequestResult.AlreadyFriends, result);
        }

        [Fact]
        public async Task SendFriendRequest_PendingSameDirection_ReturnsPending()
        {
            // Arrange
            SetupPlayers("UserA", "UserB");
            var friendship = new Friendship { PlayerIdPlayer = 1, Player1_IdPlayer = 2, FriendshipStatus = (int)FriendshipStatus.Pending };
            _mockRepository.Setup(r => r.GetFriendship(1, 2)).Returns(friendship);

            // Act
            var result = await _service.SendFriendRequestAsync("UserA", "UserB");

            // Assert
            Assert.Equal(FriendRequestResult.Pending, result);
        }

        [Fact]
        public async Task SendFriendRequest_PendingReverse_ShouldAcceptRequest()
        {
            // Arrange
            SetupPlayers("UserA", "UserB");
            var friendship = new Friendship { PlayerIdPlayer = 2, Player1_IdPlayer = 1, FriendshipStatus = (int)FriendshipStatus.Pending };
            _mockRepository.Setup(r => r.GetFriendship(1, 2)).Returns(friendship);

            // Act
            var result = await _service.SendFriendRequestAsync("UserA", "UserB");

            // Assert
            Assert.Equal(FriendRequestResult.Success, result);
            Assert.Equal((int)FriendshipStatus.Accepted, friendship.FriendshipStatus);
            _mockRepository.Verify(r => r.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task SendFriendRequest_NewRequest_ShouldCreateAndSave()
        {
            // Arrange
            SetupPlayers("UserA", "UserB");
            _mockRepository.Setup(r => r.GetFriendship(It.IsAny<int>(), It.IsAny<int>())).Returns((Friendship)null);

            // Act
            var result = await _service.SendFriendRequestAsync("UserA", "UserB");

            // Assert
            Assert.Equal(FriendRequestResult.Success, result);
            _mockRepository.Verify(r => r.AddFriendship(It.IsAny<Friendship>()), Times.Once);
            _mockRepository.Verify(r => r.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task SendFriendRequest_SqlException_ReturnsError()
        {
            // Arrange
            SetupPlayers("UserA", "UserB");
            _mockRepository.Setup(r => r.GetFriendship(It.IsAny<int>(), It.IsAny<int>())).Returns((Friendship)null);
            _mockRepository.Setup(r => r.SaveChangesAsync()).ThrowsAsync(CreateSqlException(53));

            // Act
            var result = await _service.SendFriendRequestAsync("UserA", "UserB");

            // Assert
            Assert.Equal(FriendRequestResult.Error, result);
        }

        [Fact]
        public async Task RespondRequest_Accept_UpdatesStatus()
        {
            // Arrange
            SetupPlayers("Requester", "Responder");
            var friendship = new Friendship { FriendshipStatus = (int)FriendshipStatus.Pending };
            _mockRepository.Setup(r => r.GetPendingRequest(It.IsAny<int>(), It.IsAny<int>())).Returns(friendship);

            var dto = new RespondRequestDto { RequesterUsername = "Requester", RespondingUsername = "Responder", IsAccepted = true };

            // Act
            var result = await _service.RespondToFriendRequestAsync(dto);

            // Assert
            Assert.True(result);
            Assert.Equal((int)FriendshipStatus.Accepted, friendship.FriendshipStatus);
            _mockRepository.Verify(r => r.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task RespondRequest_Reject_RemovesFriendship()
        {
            // Arrange
            SetupPlayers("Requester", "Responder");
            var friendship = new Friendship { FriendshipStatus = (int)FriendshipStatus.Pending };
            _mockRepository.Setup(r => r.GetPendingRequest(It.IsAny<int>(), It.IsAny<int>())).Returns(friendship);

            var dto = new RespondRequestDto { RequesterUsername = "Requester", RespondingUsername = "Responder", IsAccepted = false };

            // Act
            var result = await _service.RespondToFriendRequestAsync(dto);

            // Assert
            Assert.True(result);
            _mockRepository.Verify(r => r.RemoveFriendship(friendship), Times.Once);
            _mockRepository.Verify(r => r.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task RespondRequest_NoPendingRequest_ReturnsFalse()
        {
            // Arrange
            SetupPlayers("Requester", "Responder");
            _mockRepository.Setup(r => r.GetPendingRequest(It.IsAny<int>(), It.IsAny<int>())).Returns((Friendship)null);

            var dto = new RespondRequestDto { RequesterUsername = "Requester", RespondingUsername = "Responder", IsAccepted = true };

            // Act
            var result = await _service.RespondToFriendRequestAsync(dto);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task RemoveFriend_Existing_ReturnsTrue()
        {
            // Arrange
            SetupPlayers("UserA", "UserB");
            var friendship = new Friendship();
            _mockRepository.Setup(r => r.GetFriendship(It.IsAny<int>(), It.IsAny<int>())).Returns(friendship);

            // Act
            var result = await _service.RemoveFriendAsync("UserA", "UserB");

            // Assert
            Assert.True(result);
            _mockRepository.Verify(r => r.RemoveFriendship(friendship), Times.Once);
            _mockRepository.Verify(r => r.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task RemoveFriend_NotExists_ReturnsFalse()
        {
            // Arrange
            SetupPlayers("UserA", "UserB");
            _mockRepository.Setup(r => r.GetFriendship(It.IsAny<int>(), It.IsAny<int>())).Returns((Friendship)null);

            // Act
            var result = await _service.RemoveFriendAsync("UserA", "UserB");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task GetFriendList_UserWithFriends_ReturnsListWithOnlineStatus()
        {
            // Arrange
            string user = "UserMain";
            string friendName = "UserFriend";
            SetupPlayers(user, friendName);

            var friendship = new Friendship { PlayerIdPlayer = 1, Player1_IdPlayer = 2, FriendshipStatus = (int)FriendshipStatus.Accepted };
            _mockRepository.Setup(r => r.GetAcceptedFriendships(1)).Returns(new List<Friendship> { friendship });
            _mockRepository.Setup(r => r.GetPlayerById(2)).Returns(new Player { IdPlayer = 2, Username = friendName, Avatar = "img.png" });

            _service.ConnectUser(friendName, _mockCallback.Object);

            // Act
            var list = await _service.GetFriendListAsync(user);

            // Assert
            Assert.Single(list);
            Assert.Equal(friendName, list[0].Username);
            Assert.True(list[0].IsOnline);
        }

        [Fact]
        public async Task GetSentRequests_HasRequests_ReturnsList()
        {
            // Arrange
            SetupPlayers("UserA", "UserB");
            var req = new Friendship { PlayerIdPlayer = 1, Player1_IdPlayer = 2, FriendshipStatus = (int)FriendshipStatus.Pending };
            _mockRepository.Setup(r => r.GetOutgoingPendingRequests(1)).Returns(new List<Friendship> { req });
            _mockRepository.Setup(r => r.GetPlayerById(2)).Returns(new Player { Username = "UserB" });

            // Act
            var list = await _service.GetSentRequestsAsync("UserA");

            // Assert
            Assert.Single(list);
            Assert.Equal("UserB", list[0].Username);
        }

        [Fact]
        public async Task GetPendingRequests_HasRequests_ReturnsList()
        {
            // Arrange
            SetupPlayers("UserA", "UserB");
            var req = new Friendship { PlayerIdPlayer = 2, Player1_IdPlayer = 1, FriendshipStatus = (int)FriendshipStatus.Pending };
            _mockRepository.Setup(r => r.GetIncomingPendingRequests(1)).Returns(new List<Friendship> { req });
            _mockRepository.Setup(r => r.GetPlayerById(2)).Returns(new Player { Username = "UserB" });

            // Act
            var list = await _service.GetPendingRequestsAsync("UserA");

            // Assert
            Assert.Single(list);
            Assert.Equal("UserB", list[0].Username);
        }

        [Fact]
        public void SendGameInvitation_TargetConnected_InvokesCallback()
        {
            // Arrange
            string target = "TargetUser";
            _service.ConnectUser(target, _mockCallback.Object);
            var dto = new GameInvitationDto { TargetUsername = target, SenderUsername = "Sender", LobbyCode = "123" };

            // Act
            FriendshipAppService.SendGameInvitation(dto);

            // Assert
            _mockCallback.Verify(c => c.OnGameInvitationReceived("Sender", "123"), Times.Once);
        }

        [Fact]
        public void SendGameInvitation_TargetDisconnected_DoesNothing()
        {
            // Arrange
            var dto = new GameInvitationDto { TargetUsername = "OfflineUser", SenderUsername = "Sender", LobbyCode = "123" };

            // Act
            FriendshipAppService.SendGameInvitation(dto);

            // Assert
            _mockCallback.Verify(c => c.OnGameInvitationReceived(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }


        private void SetupPlayers(string u1, string u2, bool u1Guest = false)
        {
            var p1 = new Player { IdPlayer = 1, Username = u1, IsGuest = u1Guest };
            var p2 = new Player { IdPlayer = 2, Username = u2, IsGuest = false };

            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync(u1)).ReturnsAsync(p1);
            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync(u2)).ReturnsAsync(p2);
        }

        private SqlException CreateSqlException(int number)
        {
            var collectionConstructor = typeof(SqlErrorCollection).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[0], null);
            var errorCollection = (SqlErrorCollection)collectionConstructor.Invoke(null);
            var errorConstructor = typeof(SqlError).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(int), typeof(byte), typeof(byte), typeof(string), typeof(string), typeof(string), typeof(int) }, null);
            var error = (SqlError)errorConstructor.Invoke(new object[] { number, (byte)0, (byte)0, "server", "errMsg", "proc", 100 });
            var addMethod = typeof(SqlErrorCollection).GetMethod("Add", BindingFlags.NonPublic | BindingFlags.Instance);
            addMethod.Invoke(errorCollection, new object[] { error });
            var exceptionConstructor = typeof(SqlException).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(string), typeof(SqlErrorCollection), typeof(Exception), typeof(Guid) }, null);
            return (SqlException)exceptionConstructor.Invoke(new object[] { "Error simulated", errorCollection, null, Guid.NewGuid() });
        }
    }
}