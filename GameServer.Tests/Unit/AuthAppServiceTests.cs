using GameServer.DTOs.Auth;
using GameServer.Repositories.Interfaces;
using GameServer.Services.Logic;
using Moq;
using System;
using System.Data.Entity.Core;
using System.Data.Entity.Infrastructure;
using System.Data.SqlClient;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
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
        public async Task LoginAsGuestAsync_RepositoryReturnsAvailableUsername_ShouldCreatePlayerAndReturnSuccess()
        {
            // Arrange
            _mockRepository.Setup(r => r.IsUsernameTaken(It.IsAny<string>())).Returns(false);

            // Act
            var result = await _authService.LoginAsGuestAsync();

            // Assert
            Assert.True(result.Success);
        }

        [Fact]
        public async Task LoginAsGuestAsync_RepositoryReturnsAvailableUsername_ShouldCallAddPlayerAndSaveChanges()
        {
            // Arrange
            _mockRepository.Setup(r => r.IsUsernameTaken(It.IsAny<string>())).Returns(false);

            // Act
            await _authService.LoginAsGuestAsync();

            // Assert
            _mockRepository.Verify(r => r.AddPlayer(It.Is<Player>(p => p.IsGuest)), Times.Once);
        }

        [Fact]
        public async Task LoginAsGuestAsync_SqlExceptionOccurs_ShouldReturnFalseWithConnectionError()
        {
            // Arrange
            var sqlException = CreateSqlException(53);
            _mockRepository.Setup(r => r.IsUsernameTaken(It.IsAny<string>())).Throws(sqlException);

            // Act
            var result = await _authService.LoginAsGuestAsync();

            // Assert
            Assert.Equal("Error de conexión.", result.Message);
        }

        [Fact]
        public async Task LoginAsGuestAsync_TimeoutExceptionOccurs_ShouldReturnFalseWithTimeoutMessage()
        {
            // Arrange
            _mockRepository.Setup(r => r.IsUsernameTaken(It.IsAny<string>())).Throws(new TimeoutException());

            // Act
            var result = await _authService.LoginAsGuestAsync();

            // Assert
            Assert.Equal("Tiempo de espera agotado.", result.Message);
        }

        [Fact]
        public async Task RegisterUserAsync_RequestIsNull_ShouldReturnFatalError()
        {
            // Arrange
            RegisterUserRequest request = null;

            // Act
            var result = await _authService.RegisterUserAsync(request);

            // Assert
            Assert.Equal(RegistrationResult.FatalError, result);
        }

        [Fact]
        public async Task RegisterUserAsync_ValidRequestNewUser_ShouldReturnSuccess()
        {
            // Arrange
            var request = new RegisterUserRequest { Username = "NewUser", Email = "new@test.com", Password = "Password123" };
            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync(request.Username)).ReturnsAsync((Player)null);
            _mockRepository.Setup(r => r.GetAccountByEmailAsync(request.Email)).ReturnsAsync((Account)null);

            // Act
            var result = await _authService.RegisterUserAsync(request);

            // Assert
            Assert.Equal(RegistrationResult.Success, result);
        }

        [Fact]
        public async Task RegisterUserAsync_ValidRequestNewUser_ShouldInvokeSaveChanges()
        {
            // Arrange
            var request = new RegisterUserRequest { Username = "NewUser", Email = "new@test.com", Password = "Password123" };
            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync(request.Username)).ReturnsAsync((Player)null);
            _mockRepository.Setup(r => r.GetAccountByEmailAsync(request.Email)).ReturnsAsync((Account)null);

            // Act
            await _authService.RegisterUserAsync(request);

            // Assert
            _mockRepository.Verify(r => r.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task RegisterUserAsync_UsernameExistsAndActive_ShouldReturnUsernameAlreadyExists()
        {
            // Arrange
            var request = new RegisterUserRequest { Username = "ExistingUser", Email = "new@test.com", Password = "Password123" };
            var existingPlayer = new Player { Account = new Account { AccountStatus = (int)AccountStatus.Active } };
            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync(request.Username)).ReturnsAsync(existingPlayer);

            // Act
            var result = await _authService.RegisterUserAsync(request);

            // Assert
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
        public async Task RegisterUserAsync_UsernameExistsButPending_ShouldResendVerificationAndReturnEmailPending()
        {
            var request = new RegisterUserRequest { Username = "PendingUser", Email = "pending@test.com", Password = "Password123" };
            var pendingPlayer = new Player { Account = new Account { AccountStatus = (int)AccountStatus.Pending, Email = "pending@test.com" } };

            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync(request.Username)).ReturnsAsync(pendingPlayer);

            
            var result = await _authService.RegisterUserAsync(request);

            Assert.Equal(RegistrationResult.EmailPendingVerification, result);
        }

        [Fact]
        public async Task RegisterUserAsync_DbUpdateExceptionOccurs_ShouldReturnFatalError()
        {
            var request = new RegisterUserRequest { Username = "NewUser", Email = "new@test.com", Password = "Password123" };
            _mockRepository.Setup(r => r.GetPlayerByUsernameAsync(request.Username)).ReturnsAsync((Player)null);
            _mockRepository.Setup(r => r.GetAccountByEmailAsync(request.Email)).ReturnsAsync((Account)null);
            _mockRepository.Setup(r => r.SaveChangesAsync()).ThrowsAsync(new DbUpdateException());

            var result = await _authService.RegisterUserAsync(request);

            Assert.Equal(RegistrationResult.FatalError, result);
        }

        [Fact]
        public async Task LogInAsync_ValidCredentialsAndOffline_ShouldReturnTrue()
        {
            string password = "ValidPassword";
            string hash = BCrypt.Net.BCrypt.HashPassword(password);
            var player = new Player
            {
                Username = "ValidUser",
                Account = new Account { PasswordHash = hash, AccountStatus = (int)AccountStatus.Active, Email = "test@test.com" }
            };

            _mockRepository.Setup(r => r.GetPlayerForLoginAsync("ValidUser")).ReturnsAsync(player);
          

            // Act
            var result = await _authService.LogInAsync("ValidUser", password);

            Assert.True(result);
        }

        [Fact]
        public async Task LogInAsync_UserNotFound_ShouldReturnFalse()
        {
            _mockRepository.Setup(r => r.GetPlayerForLoginAsync("UnknownUser")).ReturnsAsync((Player)null);

            var result = await _authService.LogInAsync("UnknownUser", "AnyPass");

            Assert.False(result);
        }

        [Fact]
        public async Task LogInAsync_WrongPassword_ShouldReturnFalse()
        {
            string hash = BCrypt.Net.BCrypt.HashPassword("RealPassword");
            var player = new Player
            {
                Username = "User",
                Account = new Account { PasswordHash = hash, AccountStatus = (int)AccountStatus.Active }
            };
            _mockRepository.Setup(r => r.GetPlayerForLoginAsync("User")).ReturnsAsync(player);

            var result = await _authService.LogInAsync("User", "WrongPassword");

            Assert.False(result);
        }

        [Fact]
        public async Task LogInAsync_AccountPending_ShouldReturnFalse()
        {
            string hash = BCrypt.Net.BCrypt.HashPassword("Password");
            var player = new Player
            {
                Username = "User",
                Account = new Account { PasswordHash = hash, AccountStatus = (int)AccountStatus.Pending }
            };
            _mockRepository.Setup(r => r.GetPlayerForLoginAsync("User")).ReturnsAsync(player);

            var result = await _authService.LogInAsync("User", "Password");

            Assert.False(result);
        }

        [Fact]
        public async Task LogInAsync_EntityExceptionOccurs_ShouldReturnFalse()
        {
            _mockRepository.Setup(r => r.GetPlayerForLoginAsync(It.IsAny<string>())).ThrowsAsync(new EntityException());

            var result = await _authService.LogInAsync("User", "Pass");

            Assert.False(result);
        }

        [Fact]
        public void VerifyAccount_ValidCodeAndNotExpired_ShouldReturnTrue()
        {
            var account = new Account { VerificationCode = "123456", CodeExpiration = DateTime.Now.AddMinutes(10), AccountStatus = (int)AccountStatus.Pending };
            _mockRepository.Setup(r => r.GetAccountByEmail("test@test.com")).Returns(account);

            var result = _authService.VerifyAccount("test@test.com", "123456");

            Assert.True(result);
        }

        [Fact]
        public void VerifyAccount_ValidCode_ShouldUpdateStatusToActive()
        {
           
            var account = new Account { VerificationCode = "123456", CodeExpiration = DateTime.Now.AddMinutes(10), AccountStatus = (int)AccountStatus.Pending };
            _mockRepository.Setup(r => r.GetAccountByEmail("test@test.com")).Returns(account);

            
            _authService.VerifyAccount("test@test.com", "123456");

            Assert.Equal((int)AccountStatus.Active, account.AccountStatus);
        }

        [Fact]
        public void VerifyAccount_CodeMismatch_ShouldReturnFalse()
        {
            var account = new Account { VerificationCode = "123456", CodeExpiration = DateTime.Now.AddMinutes(10) };
            _mockRepository.Setup(r => r.GetAccountByEmail("test@test.com")).Returns(account);

            var result = _authService.VerifyAccount("test@test.com", "000000");

            Assert.False(result);
        }

        [Fact]
        public void VerifyAccount_CodeExpired_ShouldReturnFalse()
        {
            var account = new Account { VerificationCode = "123456", CodeExpiration = DateTime.Now.AddMinutes(-5) };
            _mockRepository.Setup(r => r.GetAccountByEmail("test@test.com")).Returns(account);

            var result = _authService.VerifyAccount("test@test.com", "123456");

            Assert.False(result);
        }

        [Fact]
        public void VerifyRecoveryCode_RepoReturnsTrue_ShouldReturnTrue()
        {
            _mockRepository.Setup(r => r.VerifyRecoveryCode("test@test.com", "123456")).Returns(true);

            var result = _authService.VerifyRecoveryCode("test@test.com", "123456");

            Assert.True(result);
        }

        [Fact]
        public void VerifyRecoveryCode_SqlExceptionOccurs_ShouldReturnFalse()
        {
            var sqlEx = CreateSqlException(53);
            _mockRepository.Setup(r => r.VerifyRecoveryCode(It.IsAny<string>(), It.IsAny<string>())).Throws(sqlEx);

            var result = _authService.VerifyRecoveryCode("test@test.com", "123456");

            Assert.False(result);
        }

        [Fact]
        public void UpdatePassword_UserExistsAndNewPassword_ShouldReturnTrue()
        {
            string oldHash = BCrypt.Net.BCrypt.HashPassword("OldPass");
            var account = new Account { PasswordHash = oldHash };
            _mockRepository.Setup(r => r.GetAccountByEmail("test@test.com")).Returns(account);

            var result = _authService.UpdatePassword("test@test.com", "NewPass");

            Assert.True(result);
        }

        [Fact]
        public void UpdatePassword_UserExistsAndSamePassword_ShouldReturnFalse()
        {
            string pass = "SamePass";
            string hash = BCrypt.Net.BCrypt.HashPassword(pass);
            var account = new Account { PasswordHash = hash };
            _mockRepository.Setup(r => r.GetAccountByEmail("test@test.com")).Returns(account);

            var result = _authService.UpdatePassword("test@test.com", pass);

            Assert.False(result);
        }

        [Fact]
        public void RegisterUserRequest_DataContractSerialization_ShouldSerializeCorrectly()
        {
            var request = new RegisterUserRequest { Username = "Test", Password = "123", Email = "a@a.com" };

            var serialized = SerializeDto(request);
            var deserialized = DeserializeDto<RegisterUserRequest>(serialized);

            Assert.Equal(request.Username, deserialized.Username);
        }

        [Fact]
        public void GuestLoginResult_DataContractSerialization_ShouldSerializeCorrectly()
        {
            
            var dto = new GuestLoginResult { Success = true, Message = "OK", Username = "Guest" };

            var serialized = SerializeDto(dto);
            var deserialized = DeserializeDto<GuestLoginResult>(serialized);

            Assert.Equal(dto.Message, deserialized.Message);
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
        [Theory]
        [InlineData(null, "email@test.com", "pass")]
        [InlineData("", "email@test.com", "pass")]
        [InlineData("   ", "email@test.com", "pass")]
        public async Task RegisterUserAsync_InvalidUsername_ShouldReturnFatalError(string username, string email, string password)
        {
            var request = new RegisterUserRequest { Username = username, Email = email, Password = password };

            var result = await _authService.RegisterUserAsync(request);

           
            Assert.Equal(RegistrationResult.FatalError, result);
        }

        [Theory]
        [InlineData("User", null, "pass")]
        [InlineData("User", "", "pass")]
        [InlineData("User", "  ", "pass")]
        [InlineData("User", "email-invalido", "pass")] 
        public async Task RegisterUserAsync_InvalidEmail_ShouldReturnFatalError(string username, string email, string password)
        {
            var request = new RegisterUserRequest { Username = username, Email = email, Password = password };

            var result = await _authService.RegisterUserAsync(request);

            Assert.Equal(RegistrationResult.FatalError, result);
        }

        [Fact]
        public void Constructor_NullRepository_ShouldThrowException()
        {
            Assert.Throws<ArgumentNullException>(() => new AuthAppService(null));
        }
    }
}