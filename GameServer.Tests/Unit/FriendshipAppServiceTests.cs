#nullable disable
using GameServer;
using GameServer.DTOs.Friendship;
using GameServer.Faults;
using GameServer.Helpers;
using GameServer.Interfaces;
using GameServer.Models;
using GameServer.Repositories;
using GameServer.Repositories.Interfaces;
using GameServer.Services.Logic;
using Moq;
using System;
using System.Collections.Generic;
using System.Data.Entity.Core;
using System.Data.SqlClient;
using System.Reflection;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Threading.Tasks;
using Xunit;

namespace GameServer.Tests.Unit
{
    public sealed class FriendshipAppServiceTests : IDisposable
    {
        private readonly Mock<IFriendshipRepository> _repoMock;
        private readonly Mock<IFriendshipConnectionManager> _connManagerMock;
        private readonly Mock<IClientCallbackProvider> _callbackProviderMock;
        private readonly Mock<IFriendshipRepositoryFactory> _repoFactoryMock;
        private readonly Mock<IFriendshipServiceCallback> _callbackMock;

        private readonly FriendshipAppService _service;

        private const string USER1 = "PlayerOne";
        private const string USER2 = "PlayerTwo";
        private const int ID1 = 10;
        private const int ID2 = 20;

        public FriendshipAppServiceTests()
        {
            _repoMock = new Mock<IFriendshipRepository>(MockBehavior.Strict);
            _connManagerMock = new Mock<IFriendshipConnectionManager>();
            _callbackProviderMock = new Mock<IClientCallbackProvider>();
            _repoFactoryMock = new Mock<IFriendshipRepositoryFactory>();
            _callbackMock = new Mock<IFriendshipServiceCallback>();

            _callbackProviderMock.Setup(c => c.GetCallback()).Returns(_callbackMock.Object);
            _repoFactoryMock.Setup(f => f.Create()).Returns(_repoMock.Object);

            _service = new FriendshipAppService(
                _repoMock.Object,
                _connManagerMock.Object,
                _callbackProviderMock.Object,
                _repoFactoryMock.Object);
        }

        public void Dispose()
        {
            _repoMock.VerifyAll();
        }

        private SqlException CreateSqlException()
        {
            var exception = FormatterServices.GetUninitializedObject(typeof(SqlException)) as SqlException;

            var collectionCtor = typeof(SqlErrorCollection).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, new Type[] { }, null);
            var collection = (SqlErrorCollection)collectionCtor.Invoke(new object[] { });

            typeof(SqlException).GetField("_errors", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(exception, collection);
            typeof(Exception).GetField("_message", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(exception, "Test DB Error");

            return exception;
        }

        [Fact]
        public void Constructor_RepositoryNull_ThrowsException()
        {
            Assert.Throws<ArgumentNullException>(() => new FriendshipAppService(null));
        }

        [Fact]
        public void Constructor_Succeeds_WithNullOptionals()
        {
            var s = new FriendshipAppService(_repoMock.Object, null, null, null);
            Assert.NotNull(s);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Connect_InvalidUsername_DoesNothing(string u)
        {
            _service.Connect(u);
            _connManagerMock.Verify(c => c.AddClient(It.IsAny<string>(), It.IsAny<IFriendshipServiceCallback>()), Times.Never);
        }

        [Fact]
        public void Connect_ValidUser_AddsClientAndNotifies()
        {
            var looseRepo = new Mock<IFriendshipRepository>();
            looseRepo.Setup(r => r.GetPlayerByUsernameAsync(It.IsAny<string>())).ReturnsAsync((Player)null);
            _repoFactoryMock.Setup(f => f.Create()).Returns(looseRepo.Object);

            _service.Connect(USER1);

            _connManagerMock.Verify(c => c.AddClient(USER1, _callbackMock.Object), Times.Once);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Disconnect_InvalidUsername_DoesNothing(string u)
        {
            _service.Disconnect(u);
            _connManagerMock.Verify(c => c.RemoveClient(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void Disconnect_ValidUser_RemovesClient()
        {
            var looseRepo = new Mock<IFriendshipRepository>();
            looseRepo.Setup(r => r.GetPlayerByUsernameAsync(It.IsAny<string>())).ReturnsAsync((Player)null);
            _repoFactoryMock.Setup(f => f.Create()).Returns(looseRepo.Object);

            _service.Disconnect(USER1);

            _connManagerMock.Verify(c => c.RemoveClient(USER1), Times.Once);
        }

        [Theory]
        [InlineData(null, "Target")]
        [InlineData("Sender", null)]
        [InlineData("", "Target")]
        [InlineData("Sender", "")]
        [InlineData("User", "User")]
        [InlineData("User", "user")]
        public async Task SendRequest_InvalidInput_ReturnsError(string s, string r)
        {
            var res = await _service.SendFriendRequest(s, r);
            Assert.Equal(FriendRequestResult.Error, res);
        }

        [Fact]
        public async Task SendRequest_UserNotFound_ReturnsTargetNotFound()
        {
            _repoMock.Setup(r => r.GetPlayerByUsernameAsync(USER1)).ReturnsAsync((Player)null);
            _repoMock.Setup(r => r.GetPlayerByUsernameAsync(USER2)).ReturnsAsync((Player)null);

            var res = await _service.SendFriendRequest(USER1, USER2);
            Assert.Equal(FriendRequestResult.TargetNotFound, res);
        }

        [Fact]
        public async Task SendRequest_GuestUser_ReturnsGuestRestriction()
        {
            var p1 = new Player { IsGuest = true };
            var p2 = new Player { IsGuest = false };
            _repoMock.Setup(r => r.GetPlayerByUsernameAsync(USER1)).ReturnsAsync(p1);
            _repoMock.Setup(r => r.GetPlayerByUsernameAsync(USER2)).ReturnsAsync(p2);

            var res = await _service.SendFriendRequest(USER1, USER2);
            Assert.Equal(FriendRequestResult.GuestRestriction, res);
        }

        [Fact]
        public async Task SendRequest_AlreadyAccepted_ReturnsAlreadyFriends()
        {
            var p1 = new Player { IdPlayer = ID1 };
            var p2 = new Player { IdPlayer = ID2 };
            var friendship = new Friendship { FriendshipStatus = (int)FriendshipStatus.Accepted };

            _repoMock.Setup(r => r.GetPlayerByUsernameAsync(USER1)).ReturnsAsync(p1);
            _repoMock.Setup(r => r.GetPlayerByUsernameAsync(USER2)).ReturnsAsync(p2);
            _repoMock.Setup(r => r.GetFriendship(ID1, ID2)).Returns(friendship);

            var res = await _service.SendFriendRequest(USER1, USER2);
            Assert.Equal(FriendRequestResult.AlreadyFriends, res);
        }

        [Fact]
        public async Task SendRequest_MutualPending_AcceptsFriendship()
        {
            var p1 = new Player { IdPlayer = ID1 };
            var p2 = new Player { IdPlayer = ID2 };
            var friendship = new Friendship
            {
                PlayerIdPlayer = ID2,
                Player1_IdPlayer = ID1,
                FriendshipStatus = (int)FriendshipStatus.Pending
            };

            _repoMock.Setup(r => r.GetPlayerByUsernameAsync(USER1)).ReturnsAsync(p1);
            _repoMock.Setup(r => r.GetPlayerByUsernameAsync(USER2)).ReturnsAsync(p2);
            _repoMock.Setup(r => r.GetFriendship(ID1, ID2)).Returns(friendship);
            _repoMock.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

            var res = await _service.SendFriendRequest(USER1, USER2);

            Assert.Equal(FriendRequestResult.MutualAccepted, res);
            Assert.Equal((int)FriendshipStatus.Accepted, friendship.FriendshipStatus);
        }

        [Fact]
        public async Task SendRequest_AlreadyPendingSameDirection_ReturnsPending()
        {
            var p1 = new Player { IdPlayer = ID1 };
            var p2 = new Player { IdPlayer = ID2 };
            var friendship = new Friendship
            {
                PlayerIdPlayer = ID1,
                Player1_IdPlayer = ID2,
                FriendshipStatus = (int)FriendshipStatus.Pending
            };

            _repoMock.Setup(r => r.GetPlayerByUsernameAsync(USER1)).ReturnsAsync(p1);
            _repoMock.Setup(r => r.GetPlayerByUsernameAsync(USER2)).ReturnsAsync(p2);
            _repoMock.Setup(r => r.GetFriendship(ID1, ID2)).Returns(friendship);

            var res = await _service.SendFriendRequest(USER1, USER2);
            Assert.Equal(FriendRequestResult.Pending, res);
        }

        [Fact]
        public async Task SendRequest_NewRequest_CreatesFriendship()
        {
            var p1 = new Player { IdPlayer = ID1 };
            var p2 = new Player { IdPlayer = ID2 };

            _repoMock.Setup(r => r.GetPlayerByUsernameAsync(USER1)).ReturnsAsync(p1);
            _repoMock.Setup(r => r.GetPlayerByUsernameAsync(USER2)).ReturnsAsync(p2);
            _repoMock.Setup(r => r.GetFriendship(ID1, ID2)).Returns((Friendship)null);
            _repoMock.Setup(r => r.AddFriendship(It.IsAny<Friendship>()));
            _repoMock.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

            var res = await _service.SendFriendRequest(USER1, USER2);

            Assert.Equal(FriendRequestResult.Success, res);
            _repoMock.Verify(r => r.AddFriendship(It.Is<Friendship>(f => f.PlayerIdPlayer == ID1 && f.Player1_IdPlayer == ID2)), Times.Once);
        }

       
        [Fact]
        public async Task RespondRequest_UserNotFound_ReturnsTargetNotFound()
        {
            var req = new RespondRequestDto { RequesterUsername = USER1, RespondingUsername = USER2, IsAccepted = true };
            _repoMock.Setup(r => r.GetPlayerByUsernameAsync(USER2)).ReturnsAsync((Player)null);
            _repoMock.Setup(r => r.GetPlayerByUsernameAsync(USER1)).ReturnsAsync((Player)null);

            var res = await _service.RespondToFriendRequest(req);
            Assert.Equal(FriendRequestResult.TargetNotFound, res);
        }

        [Fact]
        public async Task RespondRequest_RequestNotFound_ReturnsTargetNotFound()
        {
            var req = new RespondRequestDto { RequesterUsername = USER1, RespondingUsername = USER2, IsAccepted = true };
            var p1 = new Player { IdPlayer = ID1 };
            var p2 = new Player { IdPlayer = ID2 };

            _repoMock.Setup(r => r.GetPlayerByUsernameAsync(USER2)).ReturnsAsync(p2);
            _repoMock.Setup(r => r.GetPlayerByUsernameAsync(USER1)).ReturnsAsync(p1);
            _repoMock.Setup(r => r.GetPendingRequest(ID1, ID2)).Returns((Friendship)null);

            var res = await _service.RespondToFriendRequest(req);
            Assert.Equal(FriendRequestResult.TargetNotFound, res);
        }

        [Fact]
        public async Task RespondRequest_Accept_UpdatesStatus()
        {
            var req = new RespondRequestDto { RequesterUsername = USER1, RespondingUsername = USER2, IsAccepted = true };
            var p1 = new Player { IdPlayer = ID1 };
            var p2 = new Player { IdPlayer = ID2 };
            var friendship = new Friendship { FriendshipStatus = (int)FriendshipStatus.Pending };

            _repoMock.Setup(r => r.GetPlayerByUsernameAsync(USER2)).ReturnsAsync(p2);
            _repoMock.Setup(r => r.GetPlayerByUsernameAsync(USER1)).ReturnsAsync(p1);
            _repoMock.Setup(r => r.GetPendingRequest(ID1, ID2)).Returns(friendship);
            _repoMock.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

            var res = await _service.RespondToFriendRequest(req);

            Assert.Equal(FriendRequestResult.Success, res);
            Assert.Equal((int)FriendshipStatus.Accepted, friendship.FriendshipStatus);
        }

        [Fact]
        public async Task RespondRequest_Reject_RemovesFriendship()
        {
            var req = new RespondRequestDto { RequesterUsername = USER1, RespondingUsername = USER2, IsAccepted = false };
            var p1 = new Player { IdPlayer = ID1 };
            var p2 = new Player { IdPlayer = ID2 };
            var friendship = new Friendship { FriendshipStatus = (int)FriendshipStatus.Pending };

            _repoMock.Setup(r => r.GetPlayerByUsernameAsync(USER2)).ReturnsAsync(p2);
            _repoMock.Setup(r => r.GetPlayerByUsernameAsync(USER1)).ReturnsAsync(p1);
            _repoMock.Setup(r => r.GetPendingRequest(ID1, ID2)).Returns(friendship);
            _repoMock.Setup(r => r.RemoveFriendship(friendship));
            _repoMock.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

            var res = await _service.RespondToFriendRequest(req);

            Assert.Equal(FriendRequestResult.Success, res);
            _repoMock.Verify(r => r.RemoveFriendship(friendship), Times.Once);
        }

        [Fact]
        public async Task RemoveFriend_UserNotFound_ReturnsTargetNotFound()
        {
            _repoMock.Setup(r => r.GetPlayerByUsernameAsync(USER1)).ReturnsAsync((Player)null);
            _repoMock.Setup(r => r.GetPlayerByUsernameAsync(USER2)).ReturnsAsync((Player)null);

            var res = await _service.RemoveFriend(USER1, USER2);
            Assert.Equal(FriendRequestResult.TargetNotFound, res);
        }

        [Fact]
        public async Task RemoveFriend_LinkNotFound_ReturnsTargetNotFound()
        {
            var p1 = new Player { IdPlayer = ID1 };
            var p2 = new Player { IdPlayer = ID2 };

            _repoMock.Setup(r => r.GetPlayerByUsernameAsync(USER1)).ReturnsAsync(p1);
            _repoMock.Setup(r => r.GetPlayerByUsernameAsync(USER2)).ReturnsAsync(p2);
            _repoMock.Setup(r => r.GetFriendship(ID1, ID2)).Returns((Friendship)null);

            var res = await _service.RemoveFriend(USER1, USER2);
            Assert.Equal(FriendRequestResult.TargetNotFound, res);
        }

        [Fact]
        public async Task RemoveFriend_Success_RemovesAndSaves()
        {
            var p1 = new Player { IdPlayer = ID1 };
            var p2 = new Player { IdPlayer = ID2 };
            var friendship = new Friendship();

            _repoMock.Setup(r => r.GetPlayerByUsernameAsync(USER1)).ReturnsAsync(p1);
            _repoMock.Setup(r => r.GetPlayerByUsernameAsync(USER2)).ReturnsAsync(p2);
            _repoMock.Setup(r => r.GetFriendship(ID1, ID2)).Returns(friendship);
            _repoMock.Setup(r => r.RemoveFriendship(friendship));
            _repoMock.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

            var res = await _service.RemoveFriend(USER1, USER2);

            Assert.Equal(FriendRequestResult.Success, res);
            _repoMock.Verify(r => r.RemoveFriendship(friendship), Times.Once);
        }

        [Fact]
        public async Task GetFriendList_UserNotFound_ReturnsEmpty()
        {
            _repoMock.Setup(r => r.GetPlayerByUsernameAsync(USER1)).ReturnsAsync((Player)null);
            var res = await _service.GetFriendList(USER1);
            Assert.Empty(res);
        }

        [Fact]
        public async Task GetFriendList_Success_MapsDto()
        {
            var p1 = new Player { IdPlayer = ID1 };
            var p2 = new Player { IdPlayer = ID2, Username = USER2, Avatar = "img.png" };
            var friendship = new Friendship { PlayerIdPlayer = ID1, Player1_IdPlayer = ID2 };
            var list = new List<Friendship> { friendship };

            _repoMock.Setup(r => r.GetPlayerByUsernameAsync(USER1)).ReturnsAsync(p1);
            _repoMock.Setup(r => r.GetAcceptedFriendships(ID1)).Returns(list);
            _repoMock.Setup(r => r.GetPlayerById(ID2)).Returns(p2);
            _connManagerMock.Setup(c => c.IsClientConnected(USER2)).Returns(true);

            var res = await _service.GetFriendList(USER1);

            Assert.Single(res);
            Assert.Equal(USER2, res[0].Username);
            Assert.True(res[0].IsOnline);
        }

        [Fact]
        public async Task GetPendingRequests_Success_ReturnsList()
        {
            var p1 = new Player { IdPlayer = ID1 };
            var p2 = new Player { IdPlayer = ID2, Username = USER2 };
            var req = new Friendship { PlayerIdPlayer = ID2, Player1_IdPlayer = ID1 };
            var list = new List<Friendship> { req };

            _repoMock.Setup(r => r.GetPlayerByUsernameAsync(USER1)).ReturnsAsync(p1);
            _repoMock.Setup(r => r.GetIncomingPendingRequests(ID1)).Returns(list);
            _repoMock.Setup(r => r.GetPlayerById(ID2)).Returns(p2);

            var res = await _service.GetPendingRequests(USER1);

            Assert.Single(res);
            Assert.Equal(USER2, res[0].Username);
        }

        [Fact]
        public async Task GetSentRequests_Success_ReturnsList()
        {
            var p1 = new Player { IdPlayer = ID1 };
            var p2 = new Player { IdPlayer = ID2, Username = USER2 };
            var req = new Friendship { PlayerIdPlayer = ID1, Player1_IdPlayer = ID2 };
            var list = new List<Friendship> { req };

            _repoMock.Setup(r => r.GetPlayerByUsernameAsync(USER1)).ReturnsAsync(p1);
            _repoMock.Setup(r => r.GetOutgoingPendingRequests(ID1)).Returns(list);
            _repoMock.Setup(r => r.GetPlayerById(ID2)).Returns(p2);

            var res = await _service.GetSentRequests(USER1);

            Assert.Single(res);
            Assert.Equal(USER2, res[0].Username);
        }

        [Fact]
        public void SendGameInvitation_InvalidInput_DoesNothing()
        {
            _service.SendGameInvitation(null);
            _connManagerMock.Verify(c => c.GetClient(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void SendGameInvitation_UserOffline_DoesNothing()
        {
            var inv = new GameInvitationDto { TargetUsername = USER1 };
            _connManagerMock.Setup(c => c.GetClient(USER1)).Returns((IFriendshipServiceCallback)null);

            _service.SendGameInvitation(inv);

            _connManagerMock.Verify(c => c.GetClient(USER1), Times.Once);
        }

        [Fact]
        public void SendGameInvitation_UserOnline_CallsCallback()
        {
            var inv = new GameInvitationDto { TargetUsername = USER1, SenderUsername = USER2, LobbyCode = "CODE" };
            _connManagerMock.Setup(c => c.GetClient(USER1)).Returns(_callbackMock.Object);

            _service.SendGameInvitation(inv);

            _callbackMock.Verify(c => c.OnGameInvitationReceived(USER2, "CODE"), Times.Once);
        }

        [Fact]
        public void SendGameInvitation_CommunicationException_RemovesClient()
        {
            var inv = new GameInvitationDto { TargetUsername = USER1 };
            _connManagerMock.Setup(c => c.GetClient(USER1)).Returns(_callbackMock.Object);
            _callbackMock.Setup(c => c.OnGameInvitationReceived(It.IsAny<string>(), It.IsAny<string>()))
                        .Throws(new CommunicationException());

            _service.SendGameInvitation(inv);

            _connManagerMock.Verify(c => c.RemoveClient(USER1), Times.Once);
        }

        [Fact]
        public async Task RespondRequest_TimeoutException_ReturnsDatabaseError()
        {
            var req = new RespondRequestDto { RequesterUsername = USER1, RespondingUsername = USER2 };
            _repoMock.Setup(r => r.GetPlayerByUsernameAsync(USER2)).ThrowsAsync(new TimeoutException());

            await Assert.ThrowsAsync<FaultException<ServiceFault>>(() => _service.RespondToFriendRequest(req));
        }

        [Fact]
        public async Task RespondRequest_EntityException_ReturnsDatabaseError()
        {
            var req = new RespondRequestDto { RequesterUsername = USER1, RespondingUsername = USER2 };
            _repoMock.Setup(r => r.GetPlayerByUsernameAsync(USER2)).ThrowsAsync(new EntityException());

            await Assert.ThrowsAsync<FaultException<ServiceFault>>(() => _service.RespondToFriendRequest(req));
        }
    }
}