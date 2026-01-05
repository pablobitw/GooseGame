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
        public void Constructor_NullRepository_ThrowsArgumentNullException()
        {
            // Arrange
            IAuthRepository nullRepo = null;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new AuthAppService(nullRepo));
        }

        [Fact]
        public async Task RegisterUserAsync_NullRequest_ReturnsFatalError()
        {
            // Arrange
            RegisterUserRequest request = null;

            // Act
            var result = await _authService.RegisterUserAsync(request);

            // Assert
            Assert.Equal(RegistrationResult.FatalError, result);
        }

        [Fact]
        public async Task RegisterUserAsync_EmptyStrings_ReturnsFatalError()
        {
            // Arrange
            var request = new RegisterUserRequest { Username = "", Email = "", Password = "" };

            // Act
            var result = await _authService.RegisterUserAsync(request);

            // Assert
            Assert.Equal(RegistrationResult.FatalError, result);
        }



        [Fact]
        public async Task RegisterUserAsync_InvalidEmailFormat_ReturnsFatalError()
        {
            // Arrange
            var request = new RegisterUserRequest { Username = "User", Email = "invalid-email", Password = "123" };

            // Act
            var result = await _authService.RegisterUserAsync(request);

            // Assert
            Assert.Equal(RegistrationResult.FatalError, result);
        }

        [Fact]
        public async Task LogInAsync_WrongPassword_ReturnsFailure()
        {
            // Arrange
            // Generamos un hash real para que BCrypt.Verify funcione
            string correctHash = BCrypt.Net.BCrypt.HashPassword("CorrectPassword");
            var player = new Player
            {
                Username = "User",
                Account = new Account { PasswordHash = correctHash, AccountStatus = (int)AccountStatus.Active }
            };

            _mockRepository.Setup(r => r.GetPlayerForLoginAsync("User")).ReturnsAsync(player);

            // Act
            var result = await _authService.LogInAsync("User", "WrongPassword");

            // Assert
            Assert.False(result.IsSuccess);
        }

        [Fact]
        public async Task ChangeUserPasswordAsync_NewPasswordSameAsCurrent_ReturnsFalse()
        {
            // Arrange
            string password = "SamePassword";
            string hash = BCrypt.Net.BCrypt.HashPassword(password);
            var player = new Player { Account = new Account { PasswordHash = hash } };

            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync("User")).ReturnsAsync(player);

            // Act
            var result = await _authService.ChangeUserPasswordAsync("User", password, password);

            // Assert
            Assert.False(result);
        }



        [Fact]
        public async Task LoginAsGuestAsync_Success_ReturnsSuccessResult()
        {
            // Arrange
            _mockRepository.Setup(r => r.IsUsernameTaken(It.IsAny<string>())).Returns(false);

            // Act
            var result = await _authService.LoginAsGuestAsync();

            // Assert
            Assert.True(result.Success);
        }

        [Fact]
        public async Task RegisterUserAsync_ValidData_ReturnsSuccess()
        {
            // Arrange
            var request = new RegisterUserRequest { Username = "NewUser", Email = "valid@test.com", Password = "Pass" };
            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync(request.Username)).ReturnsAsync((Player)null);
            _mockRepository.Setup(r => r.GetAccountByEmailAsync(request.Email)).ReturnsAsync((Account)null);

            // Act
            var result = await _authService.RegisterUserAsync(request);

            // Assert
            Assert.Equal(RegistrationResult.Success, result);
        }

        [Fact]
        public async Task LogInAsync_ValidCredentials_ReturnsSuccess()
        {
            // Arrange
            string password = "MyPassword";
            string hash = BCrypt.Net.BCrypt.HashPassword(password);
            var player = new Player
            {
                Username = "User",
                Account = new Account { PasswordHash = hash, AccountStatus = (int)AccountStatus.Active, PreferredLanguage = "es-MX", Email = "test@test.com" }
            };

            _mockRepository.Setup(r => r.GetPlayerForLoginAsync("User")).ReturnsAsync(player);

            // Act
            var result = await _authService.LogInAsync("User", password);

            // Assert
            Assert.True(result.IsSuccess);
        }

        [Fact]
        public void VerifyAccount_ValidCodeAndDate_ReturnsTrue()
        {
            // Arrange
            var account = new Account { VerificationCode = "123456", CodeExpiration = DateTime.Now.AddMinutes(10) };
            _mockRepository.Setup(r => r.GetAccountByEmail("test@test.com")).Returns(account);

            // Act
            var result = _authService.VerifyAccount("test@test.com", "123456");

            // Assert
            Assert.True(result);
        }



        [Fact]
        public async Task RegisterUserAsync_UserPending_ReturnsEmailPendingVerification()
        {
            // Arrange
            var request = new RegisterUserRequest { Username = "PendingUser", Email = "pending@test.com", Password = "Pass" };
            var pendingPlayer = new Player
            {
                Account = new Account { AccountStatus = (int)AccountStatus.Pending, Email = "pending@test.com" }
            };

            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync(request.Username)).ReturnsAsync(pendingPlayer);

            // Act
            var result = await _authService.RegisterUserAsync(request);

            // Assert
            Assert.Equal(RegistrationResult.EmailPendingVerification, result);
        }

        [Fact]
        public async Task LogInAsync_UserBanned_ReturnsBannedMessage()
        {
            // Arrange
            var player = new Player { IsBanned = true };
            _mockRepository.Setup(r => r.GetPlayerForLoginAsync("BannedUser")).ReturnsAsync(player);

            // Act
            var result = await _authService.LogInAsync("BannedUser", "AnyPass");

            // Assert
            Assert.Contains("baneada", result.Message);
        }

        [Fact]
        public async Task LogInAsync_UserSanctioned_ReturnsSanctionMessage()
        {
            // Arrange
            var player = new Player { IsBanned = false, Account = new Account { IdAccount = 1 } };
            _mockRepository.Setup(r => r.GetPlayerForLoginAsync("User")).ReturnsAsync(player);
            _mockRepository.Setup(r => r.IsAccountSanctionedAsync(1)).ReturnsAsync(true);

            // Act
            var result = await _authService.LogInAsync("User", "AnyPass");

            // Assert
            Assert.Contains("sanción temporal", result.Message);
        }



        [Fact]
        public async Task LoginAsGuestAsync_SqlException_ReturnsConnectionErrorMessage()
        {
            // Arrange
            _mockRepository.Setup(r => r.IsUsernameTaken(It.IsAny<string>())).Throws(CreateSqlException(53));

            // Act
            var result = await _authService.LoginAsGuestAsync();

            // Assert
            Assert.Equal("Error de conexión.", result.Message);
        }

        [Fact]
        public async Task RegisterUserAsync_TimeoutException_ReturnsFatalError()
        {
            // Arrange
            var request = new RegisterUserRequest { Username = "User", Email = "valid@test.com", Password = "Pass" };
            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync(It.IsAny<string>())).ThrowsAsync(new TimeoutException());

            // Act
            var result = await _authService.RegisterUserAsync(request);

            // Assert
            Assert.Equal(RegistrationResult.FatalError, result);
        }

        [Fact]
        public async Task LogInAsync_EntityException_ReturnsServerError()
        {
            // Arrange
            _mockRepository.Setup(r => r.GetPlayerForLoginAsync(It.IsAny<string>())).ThrowsAsync(new EntityException());

            // Act
            var result = await _authService.LogInAsync("User", "Pass");

            // Assert
            Assert.Equal("Error interno del servidor.", result.Message);
        }

   

        [Fact]
        public async Task RegisterUserAsync_Success_CallsSaveChanges()
        {
            // Arrange
            var request = new RegisterUserRequest { Username = "New", Email = "new@test.com", Password = "Pass" };
            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync(request.Username)).ReturnsAsync((Player)null);
            _mockRepository.Setup(r => r.GetAccountByEmailAsync(request.Email)).ReturnsAsync((Account)null);

            // Act
            await _authService.RegisterUserAsync(request);

            // Assert
            _mockRepository.Verify(r => r.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public void VerifyAccount_ValidCode_CallsSaveChanges()
        {
            // Arrange
            var account = new Account { VerificationCode = "123", CodeExpiration = DateTime.Now.AddHours(1) };
            _mockRepository.Setup(r => r.GetAccountByEmail("email")).Returns(account);

            // Act
            _authService.VerifyAccount("email", "123");

            // Assert
            _mockRepository.Verify(r => r.SaveChanges(), Times.Once);
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