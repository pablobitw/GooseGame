using Xunit;
using Moq;
using GameServer.Services.Logic;
using GameServer.Repositories.Interfaces;
using GameServer.DTOs.Auth;
using GameServer.Models;
using System.Threading.Tasks;
using System.Data.SqlClient;
using System;
using System.Data.Entity.Core;
using System.Data.Entity.Infrastructure;
using System.Runtime.Serialization;
using GameServer;

namespace GameServer.Tests.Unit
{
    public class AuthServiceTests
    {
        private readonly Mock<IAuthRepository> _mockRepo;
        private readonly AuthAppService _service;

        public AuthServiceTests()
        {
            _mockRepo = new Mock<IAuthRepository>();
            _service = new AuthAppService(_mockRepo.Object);
        }

        private const int STATUS_ACTIVE = (int)AccountStatus.Active;

        [Fact]
        public async Task LogIn_CredencialesCorrectas_RetornaTrue()
        {
            string user = "ProGamer";
            string pass = "Pass123";
            string hash = BCrypt.Net.BCrypt.HashPassword(pass);
            var player = new Player { Username = user, Account = new Account { PasswordHash = hash, AccountStatus = STATUS_ACTIVE, Email = "test@test.com" } };

            _mockRepo.Setup(r => r.GetPlayerForLoginAsync(user)).ReturnsAsync(player);

            bool result = await _service.LogInAsync(user, pass);

            Assert.True(result);
        }

        [Theory]
        [InlineData("UsuarioNoExiste", "123")]
        [InlineData("ProGamer", "PassIncorrecta")]
        [InlineData("", "Pass123")]
        [InlineData(null, "Pass123")]
        [InlineData("   ", "Pass123")]
        [InlineData("' OR 1=1 --", "pass")]
        public async Task LogIn_DatosInvalidos_RetornaFalse(string user, string pass)
        {
            string hash = BCrypt.Net.BCrypt.HashPassword("PassReal");
            var player = new Player { Username = "ProGamer", Account = new Account { PasswordHash = hash, AccountStatus = STATUS_ACTIVE } };

            if (user == "ProGamer")
                _mockRepo.Setup(r => r.GetPlayerForLoginAsync(user)).ReturnsAsync(player);
            else
                _mockRepo.Setup(r => r.GetPlayerForLoginAsync(user)).ReturnsAsync((Player)null);

            bool result = await _service.LogInAsync(user, pass);

            Assert.False(result);
        }

        [Fact]
        public async Task LogIn_ExcepcionSql_RetornaFalse()
        {
            var sqlException = (SqlException)FormatterServices.GetUninitializedObject(typeof(SqlException));
            _mockRepo.Setup(r => r.GetPlayerForLoginAsync(It.IsAny<string>())).ThrowsAsync(sqlException);

            bool result = await _service.LogInAsync("User", "Pass");

            Assert.False(result);
        }

        [Fact]
        public async Task LogIn_ExcepcionTimeout_RetornaFalse()
        {
            _mockRepo.Setup(r => r.GetPlayerForLoginAsync(It.IsAny<string>())).ThrowsAsync(new TimeoutException());

            bool result = await _service.LogInAsync("User", "Pass");

            Assert.False(result);
        }

        [Fact]
        public async Task LogIn_ExcepcionEntity_RetornaFalse()
        {
            _mockRepo.Setup(r => r.GetPlayerForLoginAsync(It.IsAny<string>())).ThrowsAsync(new EntityException());

            bool result = await _service.LogInAsync("User", "Pass");

            Assert.False(result);
        }

        [Fact]
        public async Task ChangePassword_FlujoNormal_RetornaTrue()
        {
            string user = "User1";
            string current = "OldPass";
            string newP = "NewPass";
            string hash = BCrypt.Net.BCrypt.HashPassword(current);
            var player = new Player { Username = user, Account = new Account { PasswordHash = hash, Email = "a@a.com" } };

            _mockRepo.Setup(r => r.GetPlayerByUsernameAsync(user)).ReturnsAsync(player);

            bool result = await _service.ChangeUserPasswordAsync(user, current, newP);

            Assert.True(result);
        }

        [Fact]
        public async Task ChangePassword_FlujoNormal_GuardaCambiosEnBD()
        {
            string user = "User1";
            string current = "OldPass";
            string newP = "NewPass";
            string hash = BCrypt.Net.BCrypt.HashPassword(current);
            var player = new Player { Username = user, Account = new Account { PasswordHash = hash, Email = "a@a.com" } };

            _mockRepo.Setup(r => r.GetPlayerByUsernameAsync(user)).ReturnsAsync(player);

            await _service.ChangeUserPasswordAsync(user, current, newP);

            _mockRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
        }

        [Theory]
        [InlineData("User1", "WrongPass", "New")]
        [InlineData("User1", "OldPass", "OldPass")]
        [InlineData("Fantasma", "Pass", "New")]
        public async Task ChangePassword_FlujosAlternos_RetornaFalse(string user, string current, string newP)
        {
            string hash = BCrypt.Net.BCrypt.HashPassword("OldPass");
            var player = new Player { Username = "User1", Account = new Account { PasswordHash = hash } };

            if (user == "User1")
                _mockRepo.Setup(r => r.GetPlayerByUsernameAsync(user)).ReturnsAsync(player);
            else
                _mockRepo.Setup(r => r.GetPlayerByUsernameAsync(user)).ReturnsAsync((Player)null);

            bool result = await _service.ChangeUserPasswordAsync(user, current, newP);

            Assert.False(result);
        }

        [Fact]
        public async Task ChangePassword_FlujosAlternos_NoGuardaCambios()
        {
            string user = "Fantasma";
            _mockRepo.Setup(r => r.GetPlayerByUsernameAsync(user)).ReturnsAsync((Player)null);

            await _service.ChangeUserPasswordAsync(user, "Any", "Any");

            _mockRepo.Verify(r => r.SaveChangesAsync(), Times.Never);
        }

        [Fact]
        public async Task Register_DatosValidos_RetornaSuccess()
        {
            var request = new RegisterUserRequest { Username = "NewUser", Email = "new@test.com", Password = "123" };
            _mockRepo.Setup(r => r.GetPlayerByUsernameAsync(request.Username)).ReturnsAsync((Player)null);
            _mockRepo.Setup(r => r.GetAccountByEmailAsync(request.Email)).ReturnsAsync((Account)null);

            var result = await _service.RegisterUserAsync(request);

            Assert.Equal(RegistrationResult.Success, result);
        }

        [Fact]
        public async Task Register_DatosValidos_AgregaJugador()
        {
            var request = new RegisterUserRequest { Username = "NewUser", Email = "new@test.com", Password = "123" };
            _mockRepo.Setup(r => r.GetPlayerByUsernameAsync(request.Username)).ReturnsAsync((Player)null);
            _mockRepo.Setup(r => r.GetAccountByEmailAsync(request.Email)).ReturnsAsync((Account)null);

            await _service.RegisterUserAsync(request);

            _mockRepo.Verify(r => r.AddPlayer(It.IsAny<Player>()), Times.Once);
        }

        [Fact]
        public async Task Register_DatosValidos_GuardaCambios()
        {
            var request = new RegisterUserRequest { Username = "NewUser", Email = "new@test.com", Password = "123" };
            _mockRepo.Setup(r => r.GetPlayerByUsernameAsync(request.Username)).ReturnsAsync((Player)null);
            _mockRepo.Setup(r => r.GetAccountByEmailAsync(request.Email)).ReturnsAsync((Account)null);

            await _service.RegisterUserAsync(request);

            _mockRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task Register_UsuarioDuplicado_RetornaError()
        {
            var request = new RegisterUserRequest { Username = "Duplicado", Email = "a@a.com", Password = "123" };
            _mockRepo.Setup(r => r.GetPlayerByUsernameAsync("Duplicado")).ReturnsAsync(new Player());

            var result = await _service.RegisterUserAsync(request);

            Assert.Equal(RegistrationResult.UsernameAlreadyExists, result);
        }

        [Fact]
        public async Task Register_UsuarioDuplicado_NoGuardaNada()
        {
            var request = new RegisterUserRequest { Username = "Duplicado", Email = "a@a.com", Password = "123" };
            _mockRepo.Setup(r => r.GetPlayerByUsernameAsync("Duplicado")).ReturnsAsync(new Player());

            await _service.RegisterUserAsync(request);

            _mockRepo.Verify(r => r.SaveChangesAsync(), Times.Never);
        }

        [Fact]
        public async Task Register_RequestNulo_RetornaFatalError()
        {
            var result = await _service.RegisterUserAsync(null);
            Assert.Equal(RegistrationResult.FatalError, result);
        }

        [Fact]
        public async Task Guest_FlujoNormal_RetornaSuccess()
        {
            _mockRepo.Setup(r => r.IsUsernameTaken(It.IsAny<string>())).Returns(false);

            var result = await _service.LoginAsGuestAsync();

            Assert.True(result.Success);
        }

        [Fact]
        public async Task Guest_FlujoNormal_GeneraUsernameCorrecto()
        {
            _mockRepo.Setup(r => r.IsUsernameTaken(It.IsAny<string>())).Returns(false);

            var result = await _service.LoginAsGuestAsync();

            Assert.StartsWith("Guest_", result.Username);
        }

        [Fact]
        public async Task Guest_FlujoNormal_GuardaEnBD()
        {
            _mockRepo.Setup(r => r.IsUsernameTaken(It.IsAny<string>())).Returns(false);

            await _service.LoginAsGuestAsync();

            _mockRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task Guest_ErrorBD_RetornaFalse()
        {
            _mockRepo.Setup(r => r.IsUsernameTaken(It.IsAny<string>())).Returns(false);
            _mockRepo.Setup(r => r.SaveChangesAsync()).ThrowsAsync(new DbUpdateException());

            var result = await _service.LoginAsGuestAsync();

            Assert.False(result.Success);
        }

        [Fact]
        public async Task Guest_ErrorBD_RetornaMensajeError()
        {
            _mockRepo.Setup(r => r.IsUsernameTaken(It.IsAny<string>())).Returns(false);
            _mockRepo.Setup(r => r.SaveChangesAsync()).ThrowsAsync(new DbUpdateException());

            var result = await _service.LoginAsGuestAsync();

            Assert.Equal("Error de base de datos.", result.Message);
        }
    }
}