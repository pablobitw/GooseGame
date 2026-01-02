using Xunit;
using Moq;
using GameServer.Services.Logic;
using GameServer.Repositories.Interfaces;
using GameServer.Repositories;
using GameServer.DTOs.Friendship;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using GameServer;

namespace GameServer.Tests.Unit
{
    public class FriendshipServiceTests
    {
        private readonly Mock<IFriendshipRepository> _mockRepo;
        private readonly FriendshipAppService _service;

        public FriendshipServiceTests()
        {
            _mockRepo = new Mock<IFriendshipRepository>();
            _service = new FriendshipAppService(_mockRepo.Object);
        }

        private const int STATUS_PENDING = (int)FriendshipStatus.Pending;
        private const int STATUS_ACCEPTED = (int)FriendshipStatus.Accepted;

        [Fact]
        public async Task SendRequest_DatosValidos_RetornaTrue()
        {
            SetupUserExists("A", 1);
            SetupUserExists("B", 2);
            SetupFriendshipDoesNotExist(1, 2);

            bool result = await _service.SendFriendRequestAsync("A", "B");

            Assert.True(result);
        }

        [Fact]
        public async Task SendRequest_DatosValidos_GuardaEnRepositorio()
        {
            SetupUserExists("A", 1);
            SetupUserExists("B", 2);
            SetupFriendshipDoesNotExist(1, 2);

            await _service.SendFriendRequestAsync("A", "B");

            _mockRepo.Verify(r => r.AddFriendship(It.Is<Friendship>(f =>
                f.PlayerIdPlayer == 1 &&
                f.FriendshipStatus == STATUS_PENDING)), Times.Once);
        }

        [Fact]
        public async Task SendRequest_DatosValidos_GuardaCambiosEnBaseDeDatos()
        {
            SetupUserExists("A", 1);
            SetupUserExists("B", 2);
            SetupFriendshipDoesNotExist(1, 2);

            await _service.SendFriendRequestAsync("A", "B");

            _mockRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task SendRequest_UsuarioDuplicado_NoGuardaNada()
        {
            SetupUserExists("Yo", 1);
            SetupUserExists("Otro", 2);
            _mockRepo.Setup(r => r.GetFriendship(1, 2)).Returns(new Friendship());

            await _service.SendFriendRequestAsync("Yo", "Otro");

            _mockRepo.Verify(r => r.SaveChangesAsync(), Times.Never);
        }

        [Fact]
        public async Task SendRequest_CruceDeSolicitudes_RetornaTrue()
        {
            SetupUserExists("B", 20);
            SetupUserExists("A", 10);

            var solicitudPrevia = new Friendship { PlayerIdPlayer = 10, Player1_IdPlayer = 20, FriendshipStatus = STATUS_PENDING };
            _mockRepo.Setup(r => r.GetFriendship(20, 10)).Returns(solicitudPrevia);

            bool result = await _service.SendFriendRequestAsync("B", "A");

            Assert.True(result);
        }

        [Fact]
        public async Task SendRequest_CruceDeSolicitudes_CambiaEstadoAAceptado()
        {
            SetupUserExists("B", 20);
            SetupUserExists("A", 10);
            var solicitudPrevia = new Friendship { PlayerIdPlayer = 10, Player1_IdPlayer = 20, FriendshipStatus = STATUS_PENDING };
            _mockRepo.Setup(r => r.GetFriendship(20, 10)).Returns(solicitudPrevia);

            await _service.SendFriendRequestAsync("B", "A");

            Assert.Equal(STATUS_ACCEPTED, solicitudPrevia.FriendshipStatus);
        }

        [Fact]
        public async Task SendRequest_CruceDeSolicitudes_ActualizaBaseDeDatos()
        {
            SetupUserExists("B", 20);
            SetupUserExists("A", 10);
            var solicitudPrevia = new Friendship { PlayerIdPlayer = 10, Player1_IdPlayer = 20, FriendshipStatus = STATUS_PENDING };
            _mockRepo.Setup(r => r.GetFriendship(20, 10)).Returns(solicitudPrevia);

            await _service.SendFriendRequestAsync("B", "A");

            _mockRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task Respond_Aceptar_RetornaTrue()
        {
            var solicitud = SetupPendingRequest("Solicitante", 10, "Yo", 20);
            var req = new RespondRequestDto { RequesterUsername = "Solicitante", RespondingUsername = "Yo", IsAccepted = true };

            bool result = await _service.RespondToFriendRequestAsync(req);

            Assert.True(result);
        }

        [Fact]
        public async Task Respond_Aceptar_CambiaEstadoAAceptado()
        {
            var solicitud = SetupPendingRequest("Solicitante", 10, "Yo", 20);
            var req = new RespondRequestDto { RequesterUsername = "Solicitante", RespondingUsername = "Yo", IsAccepted = true };

            await _service.RespondToFriendRequestAsync(req);

            Assert.Equal(STATUS_ACCEPTED, solicitud.FriendshipStatus);
        }

        [Fact]
        public async Task Respond_Rechazar_EliminaLaAmistad()
        {
            var solicitud = SetupPendingRequest("Solicitante", 10, "Yo", 20);
            var req = new RespondRequestDto { RequesterUsername = "Solicitante", RespondingUsername = "Yo", IsAccepted = false };

            await _service.RespondToFriendRequestAsync(req);

            _mockRepo.Verify(r => r.RemoveFriendship(solicitud), Times.Once);
        }

        private void SetupUserExists(string username, int id)
        {
            var p = new Player { IdPlayer = id, Username = username };
            _mockRepo.Setup(r => r.GetPlayerByUsernameAsync(username)).ReturnsAsync(p);
        }

        private void SetupFriendshipDoesNotExist(int id1, int id2)
        {
            _mockRepo.Setup(r => r.GetFriendship(id1, id2)).Returns((Friendship)null);
        }

        private Friendship SetupPendingRequest(string reqName, int reqId, string resName, int resId)
        {
            SetupUserExists(reqName, reqId);
            SetupUserExists(resName, resId);
            var f = new Friendship { FriendshipStatus = STATUS_PENDING };
            _mockRepo.Setup(r => r.GetPendingRequest(reqId, resId)).Returns(f);
            return f;
        }
    }
}