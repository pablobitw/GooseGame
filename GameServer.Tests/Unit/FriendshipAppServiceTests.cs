using GameServer.DTOs.Friendship;
using GameServer.Interfaces;
using GameServer.Repositories;
using GameServer.Services.Logic;
using Moq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Entity.Infrastructure;
using System.Data.SqlClient;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Xunit;

namespace GameServer.Tests.Services
{
    public class FriendshipAppServiceTests : IDisposable
    {
        private readonly Mock<IFriendshipRepository> _mockRepository;
        private readonly FriendshipAppService _service;

        public FriendshipAppServiceTests()
        {
            ResetStaticState();
            _mockRepository = new Mock<IFriendshipRepository>();
            _service = new FriendshipAppService(_mockRepository.Object);
        }

        public void Dispose()
        {
            ResetStaticState();
        }

        private void ResetStaticState()
        {
            var field = typeof(FriendshipAppService).GetField("_connectedClients", BindingFlags.NonPublic | BindingFlags.Static);
            if (field != null)
            {
                var dict = (IDictionary)field.GetValue(null);
                dict?.Clear();
            }
        }

        [Fact]
        public void ConnectUser_NewUser_ShouldAddCallback()
        {
            string username = "User1";
            var mockCallback = new Mock<IFriendshipServiceCallback>();

            _service.ConnectUser(username, mockCallback.Object);

            FriendshipAppService.SendGameInvitation(new GameInvitationDto
            {
                SenderUsername = "Sender",
                TargetUsername = username,
                LobbyCode = "123"
            });

            mockCallback.Verify(c => c.OnGameInvitationReceived("Sender", "123"), Times.Once);
        }

        [Fact]
        public async Task SendFriendRequestAsync_ValidRequest_ShouldAddFriendshipAndSave()
        {
            string sender = "UserA";
            string receiver = "UserB";
            var playerA = new Player { IdPlayer = 1, Username = sender, IsGuest = false };
            var playerB = new Player { IdPlayer = 2, Username = receiver, IsGuest = false };

            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync(sender)).ReturnsAsync(playerA);
            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync(receiver)).ReturnsAsync(playerB);
            _mockRepository.Setup(r => r.GetFriendship(1, 2)).Returns((Friendship)null);

            var result = await _service.SendFriendRequestAsync(sender, receiver);

            Assert.Equal(FriendRequestResult.Success, result);
            _mockRepository.Verify(r => r.AddFriendship(It.Is<Friendship>(f => f.FriendshipStatus == 0)), Times.Once);
            _mockRepository.Verify(r => r.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task SendFriendRequestAsync_GuestUser_ShouldReturnFalse()
        {
            string sender = "Guest_123";
            string receiver = "UserB";
            var playerA = new Player { Username = sender, IsGuest = true };
            var playerB = new Player { Username = receiver, IsGuest = false };

            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync(sender)).ReturnsAsync(playerA);
            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync(receiver)).ReturnsAsync(playerB);

            var result = await _service.SendFriendRequestAsync(sender, receiver);

            Assert.NotEqual(FriendRequestResult.Success, result);
            _mockRepository.Verify(r => r.SaveChangesAsync(), Times.Never);
        }

        [Fact]
        public async Task SendFriendRequestAsync_AlreadyExists_ShouldReturnFalse()
        {
            string sender = "UserA";
            string receiver = "UserB";
            var playerA = new Player { IdPlayer = 1 };
            var playerB = new Player { IdPlayer = 2 };
            var existing = new Friendship { FriendshipStatus = 1 };

            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync(sender)).ReturnsAsync(playerA);
            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync(receiver)).ReturnsAsync(playerB);
            _mockRepository.Setup(r => r.GetFriendship(1, 2)).Returns(existing);

            var result = await _service.SendFriendRequestAsync(sender, receiver);

            Assert.Equal(FriendRequestResult.AlreadyFriends, result);
        }

        [Fact]
        public async Task SendFriendRequestAsync_ReversePending_ShouldAcceptAndSave()
        {
            string sender = "UserA";
            string receiver = "UserB";
            var playerA = new Player { IdPlayer = 1, Username = sender };
            var playerB = new Player { IdPlayer = 2, Username = receiver };

            var existing = new Friendship
            {
                PlayerIdPlayer = 2,
                Player1_IdPlayer = 1,
                FriendshipStatus = 0
            };

            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync(sender)).ReturnsAsync(playerA);
            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync(receiver)).ReturnsAsync(playerB);
            _mockRepository.Setup(r => r.GetFriendship(1, 2)).Returns(existing);

            var result = await _service.SendFriendRequestAsync(sender, receiver);

            Assert.Equal(FriendRequestResult.Success, result);
            Assert.Equal(1, existing.FriendshipStatus);
            _mockRepository.Verify(r => r.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task SendFriendRequestAsync_SqlException_ShouldReturnFalse()
        {
            string sender = "UserA";
            string receiver = "UserB";
            var playerA = new Player { IdPlayer = 1 };
            var playerB = new Player { IdPlayer = 2 };

            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync(sender)).ReturnsAsync(playerA);
            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync(receiver)).ReturnsAsync(playerB);
            _mockRepository.Setup(r => r.SaveChangesAsync()).ThrowsAsync(CreateSqlException(53));

            var result = await _service.SendFriendRequestAsync(sender, receiver);

            Assert.Equal(FriendRequestResult.Error, result);
        }

        [Fact]
        public async Task RespondToFriendRequestAsync_Accept_ShouldUpdateStatus()
        {
            var dto = new RespondRequestDto { RequesterUsername = "UserA", RespondingUsername = "UserB", IsAccepted = true };
            var pA = new Player { IdPlayer = 1 };
            var pB = new Player { IdPlayer = 2, IsGuest = false };
            var friendship = new Friendship { FriendshipStatus = 0 };

            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync("UserA")).ReturnsAsync(pA);
            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync("UserB")).ReturnsAsync(pB);
            _mockRepository.Setup(r => r.GetPendingRequest(1, 2)).Returns(friendship);

            var result = await _service.RespondToFriendRequestAsync(dto);

            Assert.True(result);
            Assert.Equal(1, friendship.FriendshipStatus);
            _mockRepository.Verify(r => r.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task RespondToFriendRequestAsync_Reject_ShouldRemoveFriendship()
        {
            var dto = new RespondRequestDto { RequesterUsername = "UserA", RespondingUsername = "UserB", IsAccepted = false };
            var pA = new Player { IdPlayer = 1 };
            var pB = new Player { IdPlayer = 2 };
            var friendship = new Friendship { FriendshipStatus = 0 };

            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync("UserA")).ReturnsAsync(pA);
            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync("UserB")).ReturnsAsync(pB);
            _mockRepository.Setup(r => r.GetPendingRequest(1, 2)).Returns(friendship);

            var result = await _service.RespondToFriendRequestAsync(dto);

            Assert.True(result);
            _mockRepository.Verify(r => r.RemoveFriendship(friendship), Times.Once);
            _mockRepository.Verify(r => r.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task RespondToFriendRequestAsync_RequestNotFound_ShouldReturnFalse()
        {
            var dto = new RespondRequestDto { RequesterUsername = "UserA", RespondingUsername = "UserB" };
            var pA = new Player { IdPlayer = 1 };
            var pB = new Player { IdPlayer = 2 };

            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync("UserA")).ReturnsAsync(pA);
            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync("UserB")).ReturnsAsync(pB);
            _mockRepository.Setup(r => r.GetPendingRequest(1, 2)).Returns((Friendship)null);

            var result = await _service.RespondToFriendRequestAsync(dto);

            Assert.False(result);
        }

        [Fact]
        public async Task RemoveFriendAsync_ValidFriendship_ShouldRemove()
        {
            string user = "UserA";
            string friend = "UserB";
            var pA = new Player { IdPlayer = 1, IsGuest = false };
            var pB = new Player { IdPlayer = 2 };
            var friendship = new Friendship { FriendshipStatus = 1 };

            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync(user)).ReturnsAsync(pA);
            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync(friend)).ReturnsAsync(pB);
            _mockRepository.Setup(r => r.GetFriendship(1, 2)).Returns(friendship);

            var result = await _service.RemoveFriendAsync(user, friend);

            Assert.True(result);
            _mockRepository.Verify(r => r.RemoveFriendship(friendship), Times.Once);
        }

        [Fact]
        public async Task RemoveFriendAsync_DbUpdateException_ShouldReturnFalse()
        {
            string user = "UserA";
            string friend = "UserB";
            var pA = new Player { IdPlayer = 1 };
            var pB = new Player { IdPlayer = 2 };
            var friendship = new Friendship { FriendshipStatus = 1 };

            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync(user)).ReturnsAsync(pA);
            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync(friend)).ReturnsAsync(pB);
            _mockRepository.Setup(r => r.GetFriendship(1, 2)).Returns(friendship);
            _mockRepository.Setup(r => r.SaveChangesAsync()).ThrowsAsync(new DbUpdateException());

            var result = await _service.RemoveFriendAsync(user, friend);

            Assert.False(result);
        }

        [Fact]
        public async Task GetFriendListAsync_UserWithFriends_ShouldReturnDtoList()
        {
            string username = "UserA";
            var pA = new Player { IdPlayer = 1, Username = username };
            var pB = new Player { IdPlayer = 2, Username = "UserB", Avatar = "path" };
            var friendship = new Friendship { PlayerIdPlayer = 1, Player1_IdPlayer = 2, FriendshipStatus = 1 };
            var list = new List<Friendship> { friendship };

            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync(username)).ReturnsAsync(pA);
            _mockRepository.Setup(r => r.GetAcceptedFriendships(1)).Returns(list);
            _mockRepository.Setup(r => r.GetPlayerById(2)).Returns(pB);

            var result = await _service.GetFriendListAsync(username);

            Assert.Single(result);
            Assert.Equal("UserB", result[0].Username);
            Assert.False(result[0].IsOnline);
        }

        [Fact]
        public async Task GetFriendListAsync_FriendOnline_ShouldReturnIsOnlineTrue()
        {
            string username = "UserA";
            var pA = new Player { IdPlayer = 1, Username = username };
            var pB = new Player { IdPlayer = 2, Username = "OnlineFriend", Avatar = "path" };
            var friendship = new Friendship { PlayerIdPlayer = 1, Player1_IdPlayer = 2, FriendshipStatus = 1 };

            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync(username)).ReturnsAsync(pA);
            _mockRepository.Setup(r => r.GetAcceptedFriendships(1)).Returns(new List<Friendship> { friendship });
            _mockRepository.Setup(r => r.GetPlayerById(2)).Returns(pB);

            _service.ConnectUser("OnlineFriend", new Mock<IFriendshipServiceCallback>().Object);

            var result = await _service.GetFriendListAsync(username);

            Assert.True(result[0].IsOnline);
        }

        [Fact]
        public async Task GetPendingRequestsAsync_HasRequests_ShouldReturnList()
        {
            string username = "UserA";
            var pA = new Player { IdPlayer = 1 };
            var pB = new Player { IdPlayer = 2, Username = "Requester", Avatar = "path" };
            var request = new Friendship { PlayerIdPlayer = 2, Player1_IdPlayer = 1, FriendshipStatus = 0 };

            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync(username)).ReturnsAsync(pA);
            _mockRepository.Setup(r => r.GetIncomingPendingRequests(1)).Returns(new List<Friendship> { request });
            _mockRepository.Setup(r => r.GetPlayerById(2)).Returns(pB);

            var result = await _service.GetPendingRequestsAsync(username);

            Assert.Single(result);
            Assert.Equal("Requester", result[0].Username);
        }

        [Fact]
        public async Task GetPendingRequestsAsync_Timeout_ShouldReturnEmptyList()
        {
            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync(It.IsAny<string>())).ThrowsAsync(new TimeoutException());

            var result = await _service.GetPendingRequestsAsync("UserA");

            Assert.Empty(result);
        }

        [Fact]
        public void FriendDto_Serialization_ShouldWork()
        {
            var dto = new FriendDto { Username = "Test", AvatarPath = "Path", IsOnline = true };
            string xml = SerializeDto(dto);
            var deserialized = DeserializeDto<FriendDto>(xml);
            Assert.Equal(dto.Username, deserialized.Username);
        }

        [Fact]
        public void GameInvitationDto_Serialization_ShouldWork()
        {
            var dto = new GameInvitationDto { SenderUsername = "A", TargetUsername = "B", LobbyCode = "123" };
            string xml = SerializeDto(dto);
            var deserialized = DeserializeDto<GameInvitationDto>(xml);
            Assert.Equal(dto.LobbyCode, deserialized.LobbyCode);
        }

        [Fact]
        public void RespondRequestDto_Serialization_ShouldWork()
        {
            var dto = new RespondRequestDto { RequesterUsername = "A", RespondingUsername = "B", IsAccepted = true };
            string xml = SerializeDto(dto);
            var deserialized = DeserializeDto<RespondRequestDto>(xml);
            Assert.True(deserialized.IsAccepted);
        }

        private string SerializeDto<T>(T instance)
        {
            var serializer = new DataContractSerializer(typeof(T));
            using (var stream = new MemoryStream())
            {
                serializer.WriteObject(stream, instance);
                return System.Text.Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        private T DeserializeDto<T>(string xml)
        {
            var serializer = new DataContractSerializer(typeof(T));
            using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml)))
            {
                return (T)serializer.ReadObject(stream);
            }
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