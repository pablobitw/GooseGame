using GameServer.DTOs.Auth;
using GameServer.Repositories.Interfaces;
using GameServer.Services.Logic;
using Moq;
using System;
using System.Data.Entity.Core;
using System.Data.Entity.Infrastructure;
using System.Data.SqlClient;
using System.Reflection; 
using System.Threading.Tasks;
using Xunit;

namespace GameServer.Tests.Services
{
    public class AuthAppServiceTests
    {
        private readonly Mock<IAuthRepository> _mockRepository;
        private readonly AuthAppService _authService;

        public AuthAppServiceTests()
        {
            _mockRepository = new Mock<IAuthRepository>();
            _authService = new AuthAppService(_mockRepository.Object);
        }

        [Fact]
        public async Task LoginAsGuestAsync_RepositoryReturnsAvailableUsername_ShouldReturnSuccessTrue()
        {
            _mockRepository.Setup(r => r.IsUsernameTaken(It.IsAny<string>())).Returns(false);

            var result = await _authService.LoginAsGuestAsync();

            Assert.True(result.Success);
            Assert.Equal("Bienvenido Invitado", result.Message);
        }

        [Fact]
        public async Task LoginAsGuestAsync_SqlExceptionOccurs_ShouldReturnSuccessFalse()
        {
            var sqlException = CreateSqlException(53);
            _mockRepository.Setup(r => r.IsUsernameTaken(It.IsAny<string>())).Throws(sqlException);

            var result = await _authService.LoginAsGuestAsync();

            Assert.False(result.Success);
            Assert.Equal("Error de conexión.", result.Message);
        }

        [Fact]
        public async Task LoginAsGuestAsync_TimeoutExceptionOccurs_ShouldReturnSuccessFalse()
        {
            _mockRepository.Setup(r => r.IsUsernameTaken(It.IsAny<string>())).Throws(new TimeoutException());

            var result = await _authService.LoginAsGuestAsync();

            Assert.False(result.Success);
            Assert.Equal("Tiempo de espera agotado.", result.Message);
        }

        // ------------------------------------------------------------------------
        // REGISTRO (RegistrationResult Enum)
        // ------------------------------------------------------------------------

        [Fact]
        public async Task RegisterUserAsync_RequestIsNull_ShouldReturnFatalError()
        {
            var result = await _authService.RegisterUserAsync(null);
            Assert.Equal(RegistrationResult.FatalError, result);
        }

        [Fact]
        public async Task RegisterUserAsync_ValidRequestNewUser_ShouldReturnSuccess()
        {
            var request = new RegisterUserRequest { Username = "NewUser", Email = "new@test.com", Password = "Password123" };

            // Simulamos que no existe ni usuario ni email
            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync(request.Username)).ReturnsAsync((Player)null);
            _mockRepository.Setup(r => r.GetAccountByEmailAsync(request.Email)).ReturnsAsync((Account)null);

            var result = await _authService.RegisterUserAsync(request);

            Assert.Equal(RegistrationResult.Success, result);
            _mockRepository.Verify(r => r.AddPlayer(It.IsAny<Player>()), Times.Once);
            _mockRepository.Verify(r => r.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task RegisterUserAsync_UsernameExistsAndActive_ShouldReturnUsernameAlreadyExists()
        {
            var request = new RegisterUserRequest { Username = "ExistingUser", Email = "new@test.com", Password = "Password123" };
            var existingPlayer = new Player { Account = new Account { AccountStatus = (int)AccountStatus.Active } };

            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync(request.Username)).ReturnsAsync(existingPlayer);

            var result = await _authService.RegisterUserAsync(request);

            Assert.Equal(RegistrationResult.UsernameAlreadyExists, result);
        }

        [Fact]
        public async Task RegisterUserAsync_EmailExistsAndActive_ShouldReturnEmailAlreadyExists()
        {
            var request = new RegisterUserRequest { Username = "NewUser", Email = "existing@test.com", Password = "Password123" };
            var existingAccount = new Account { AccountStatus = (int)AccountStatus.Active };

            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync(request.Username)).ReturnsAsync((Player)null);
            _mockRepository.Setup(r => r.GetAccountByEmailAsync(request.Email)).ReturnsAsync(existingAccount);

            var result = await _authService.RegisterUserAsync(request);

            Assert.Equal(RegistrationResult.EmailAlreadyExists, result);
        }

        [Fact]
        public async Task RegisterUserAsync_UsernameExistsButPending_ShouldResendVerification()
        {
            var request = new RegisterUserRequest { Username = "PendingUser", Email = "pending@test.com", Password = "Password123" };
            var pendingPlayer = new Player { Account = new Account { AccountStatus = (int)AccountStatus.Pending, Email = "pending@test.com" } };

            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync(request.Username)).ReturnsAsync(pendingPlayer);

            var result = await _authService.RegisterUserAsync(request);

            Assert.Equal(RegistrationResult.EmailPendingVerification, result);
        }

        // ------------------------------------------------------------------------
        // LOGIN NORMAL (LoginResponseDto usa 'IsSuccess')
        // ------------------------------------------------------------------------

        [Fact]
        public async Task LogInAsync_ValidCredentials_ShouldReturnIsSuccessTrue()
        {
            string password = "ValidPassword";
            string hash = BCrypt.Net.BCrypt.HashPassword(password);

            var player = new Player
            {
                Username = "ValidUser",
                Account = new Account
                {
                    PasswordHash = hash,
                    AccountStatus = (int)AccountStatus.Active,
                    Email = "test@test.com",
                    PreferredLanguage = "es-MX"
                }
            };

            _mockRepository.Setup(r => r.GetPlayerForLoginAsync("ValidUser")).ReturnsAsync(player);

            var result = await _authService.LogInAsync("ValidUser", password);

            Assert.True(result.IsSuccess);
            Assert.Equal("Login exitoso.", result.Message);
        }

        [Fact]
        public async Task LogInAsync_UserNotFound_ShouldReturnIsSuccessFalse()
        {
            _mockRepository.Setup(r => r.GetPlayerForLoginAsync("UnknownUser")).ReturnsAsync((Player)null);

            var result = await _authService.LogInAsync("UnknownUser", "AnyPass");

            Assert.False(result.IsSuccess);
        }

        [Fact]
        public async Task LogInAsync_WrongPassword_ShouldReturnIsSuccessFalse()
        {
            string hash = BCrypt.Net.BCrypt.HashPassword("RealPassword");
            var player = new Player
            {
                Username = "User",
                Account = new Account { PasswordHash = hash, AccountStatus = (int)AccountStatus.Active }
            };
            _mockRepository.Setup(r => r.GetPlayerForLoginAsync("User")).ReturnsAsync(player);

            var result = await _authService.LogInAsync("User", "WrongPassword");

            Assert.False(result.IsSuccess);
        }

        [Fact]
        public async Task LogInAsync_AccountBanned_ShouldReturnIsSuccessFalseAndBannedMessage()
        {
            var player = new Player
            {
                Username = "BannedUser",
                IsBanned = true, // Usuario baneado
                Account = new Account { AccountStatus = (int)AccountStatus.Banned }
            };
            _mockRepository.Setup(r => r.GetPlayerForLoginAsync("BannedUser")).ReturnsAsync(player);

            var result = await _authService.LogInAsync("BannedUser", "AnyPass");

            Assert.False(result.IsSuccess);
            Assert.Contains("baneada permanentemente", result.Message);
        }

        [Fact]
        public async Task LogInAsync_SqlException_ShouldReturnDatabaseError()
        {
            _mockRepository.Setup(r => r.GetPlayerForLoginAsync(It.IsAny<string>())).Throws(CreateSqlException(53));

            var result = await _authService.LogInAsync("User", "Pass");

            Assert.False(result.IsSuccess);
            Assert.Equal("Error de base de datos.", result.Message);
        }



        private SqlException CreateSqlException(int number)
        {
            // Ahora esto funcionará porque 'System.Reflection' está incluido arriba
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