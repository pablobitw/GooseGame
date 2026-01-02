#nullable disable

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
using System.Data.Entity.Validation;
using System.Runtime.Serialization;

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

        [Fact]
        public async Task LogIn_FlujoNormal_CredencialesCorrectas_RetornaTrue()
        {
            string user = "ProGamer";
            string pass = "Pass123";
            string hash = BCrypt.Net.BCrypt.HashPassword(pass);
            var player = new Player { Username = user, Account = new Account { PasswordHash = hash, AccountStatus = 1, Email = "test@test.com" } };

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
        [InlineData("admin'; DROP TABLE Players; --", "pass")]
        [InlineData("<script>alert(1)</script>", "pass")]
        public async Task LogIn_FlujosAlternos_DatosInvalidos_RetornaFalse(string user, string pass)
        {
            string hash = BCrypt.Net.BCrypt.HashPassword("PassReal");
            var player = new Player { Username = "ProGamer", Account = new Account { PasswordHash = hash, AccountStatus = 1 } };

            if (user == "ProGamer")
                _mockRepo.Setup(r => r.GetPlayerForLoginAsync(user)).ReturnsAsync(player);
            else
                _mockRepo.Setup(r => r.GetPlayerForLoginAsync(user)).ReturnsAsync((Player)null);

            bool result = await _service.LogInAsync(user, pass);
            Assert.False(result);
        }

        [Fact]
        public async Task LogIn_Excepcion_Sql_RetornaFalse()
        {
#pragma warning disable SYSLIB0050
            var sqlException = (SqlException)FormatterServices.GetUninitializedObject(typeof(SqlException));
#pragma warning restore SYSLIB0050

            _mockRepo.Setup(r => r.GetPlayerForLoginAsync(It.IsAny<string>()))
                     .ThrowsAsync(sqlException);

            bool result = await _service.LogInAsync("User", "Pass");
            Assert.False(result);
        }

        [Fact]
        public async Task LogIn_Excepcion_Timeout_RetornaFalse()
        {
            _mockRepo.Setup(r => r.GetPlayerForLoginAsync(It.IsAny<string>()))
                     .ThrowsAsync(new TimeoutException());

            bool result = await _service.LogInAsync("User", "Pass");
            Assert.False(result);
        }

        [Fact]
        public async Task LogIn_Excepcion_EntityError_RetornaFalse()
        {
            _mockRepo.Setup(r => r.GetPlayerForLoginAsync(It.IsAny<string>()))
                     .ThrowsAsync(new EntityException());

            bool result = await _service.LogInAsync("User", "Pass");
            Assert.False(result);
        }

        [Fact]
        public async Task ChangePassword_FlujoNormal_Exito_RetornaTrue()
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
        public async Task Register_FlujoNormal_Exito_RetornaSuccess()
        {
            var request = new RegisterUserRequest { Username = "NewUser", Email = "new@test.com", Password = "123" };

            _mockRepo.Setup(r => r.GetPlayerByUsernameAsync(request.Username)).ReturnsAsync((Player)null);
            _mockRepo.Setup(r => r.GetAccountByEmailAsync(request.Email)).ReturnsAsync((Account)null);

            var result = await _service.RegisterUserAsync(request);
            Assert.Equal(RegistrationResult.Success, result);
        }

        [Fact]
        public async Task Register_Alterno_UsuarioDuplicado_RetornaError()
        {
            var request = new RegisterUserRequest { Username = "Duplicado", Email = "a@a.com", Password = "123" };
            var existing = new Player { Username = "Duplicado" };

            _mockRepo.Setup(r => r.GetPlayerByUsernameAsync("Duplicado")).ReturnsAsync(existing);

            var result = await _service.RegisterUserAsync(request);
            Assert.Equal(RegistrationResult.UsernameAlreadyExists, result);
        }

        [Theory]
        [InlineData(null)]
        public async Task Register_Alterno_RequestNulo_RetornaFatalError(RegisterUserRequest req)
        {
            var result = await _service.RegisterUserAsync(req);
            Assert.Equal(RegistrationResult.FatalError, result);
        }

        [Fact]
        public async Task Guest_FlujoNormal_CreaUsuario_RetornaSuccess()
        {
            _mockRepo.Setup(r => r.IsUsernameTaken(It.IsAny<string>())).Returns(false);

            var result = await _service.LoginAsGuestAsync();
            Assert.True(result.Success);
            Assert.StartsWith("Guest_", result.Username);
        }

        [Fact]
        public async Task Guest_Excepcion_ErrorGuardar_RetornaFalse()
        {
            _mockRepo.Setup(r => r.IsUsernameTaken(It.IsAny<string>())).Returns(false);
            _mockRepo.Setup(r => r.SaveChangesAsync()).ThrowsAsync(new DbUpdateException());

            var result = await _service.LoginAsGuestAsync();
            Assert.False(result.Success);
            Assert.Equal("Error de base de datos.", result.Message);
        }
    }
}