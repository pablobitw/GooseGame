#nullable disable
using GameServer;
using GameServer.DTOs.Auth;
using GameServer.Faults;
using GameServer.Helpers;
using GameServer.Repositories.Interfaces;
using GameServer.Services.Logic;
using Moq;
using System;
using System.Collections;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Threading.Tasks;
using Xunit;

namespace GameServer.Tests.Unit
{
    public sealed class AuthAppServiceTests : IDisposable
    {
        private readonly Mock<IAuthRepository> _repositoryMock;
        private readonly Mock<IEmailService> _emailMock;
        private readonly Mock<IConnectionManagerWrapper> _connectionMock;
        private readonly AuthAppService _service;

        private const string USER = "PlayerOne";
        private const string EMAIL = "test@test.com";
        private const string PASS = "Pass123";

        public AuthAppServiceTests()
        {
            _repositoryMock = new Mock<IAuthRepository>(MockBehavior.Strict);
            _emailMock = new Mock<IEmailService>();
            _connectionMock = new Mock<IConnectionManagerWrapper>();

            _emailMock.Setup(e => e.SendVerificationEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                      .ReturnsAsync(true);
            _emailMock.Setup(e => e.SendLoginNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                      .Returns(Task.CompletedTask);
            _emailMock.Setup(e => e.SendPasswordChangedNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                      .Returns(Task.CompletedTask);

            _service = new AuthAppService(_repositoryMock.Object, _emailMock.Object, _connectionMock.Object);
        }

        public void Dispose()
        {
            _repositoryMock.VerifyAll();
        }

        [Fact]
        public void Constructor_RepositoryNull_ThrowsException()
        {
            Assert.Throws<ArgumentNullException>(() => new AuthAppService(null, _emailMock.Object, _connectionMock.Object));
        }

        [Fact]
        public void Constructor_Succeeds_WithNullOptionals()
        {
            var service = new AuthAppService(_repositoryMock.Object, null, null);
            Assert.NotNull(service);
        }

        [Theory]
        [InlineData(null, "e@e.com", "123")]
        [InlineData("", "e@e.com", "123")]
        [InlineData("   ", "e@e.com", "123")]
        [InlineData("User", null, "123")]
        [InlineData("User", "", "123")]
        [InlineData("User", "  ", "123")]
        [InlineData("User", "e@e.com", null)]
        [InlineData("User", "e@e.com", "")]
        [InlineData("User", "e@e.com", "   ")]
        public async Task Register_InvalidData_ReturnsFatalError(string u, string e, string p)
        {
            var req = new RegisterUserRequest { Username = u, Email = e, Password = p };
            var res = await _service.RegisterUserAsync(req);
            Assert.Equal(RegistrationResult.FatalError, res);
        }

        [Fact]
        public async Task Register_RequestNull_ReturnsFatalError()
        {
            var res = await _service.RegisterUserAsync(null);
            Assert.Equal(RegistrationResult.FatalError, res);
        }

        [Fact]
        public async Task Register_UserExists_ReturnsUsernameTaken()
        {
            var req = new RegisterUserRequest { Username = USER, Email = EMAIL, Password = PASS };
            var existing = new Player { Username = USER, Account = new Account { AccountStatus = (int)AccountStatus.Active } };

            _repositoryMock.Setup(r => r.GetPlayerByUsernameAsync(USER)).ReturnsAsync(existing);

            var res = await _service.RegisterUserAsync(req);
            Assert.Equal(RegistrationResult.UsernameAlreadyExists, res);
        }

        [Fact]
        public async Task Register_EmailExists_ReturnsEmailTaken()
        {
            var req = new RegisterUserRequest { Username = USER, Email = EMAIL, Password = PASS };
            var account = new Account { Email = EMAIL, AccountStatus = (int)AccountStatus.Active };

            _repositoryMock.Setup(r => r.GetPlayerByUsernameAsync(USER)).ReturnsAsync((Player)null);
            _repositoryMock.Setup(r => r.GetAccountByEmailAsync(EMAIL)).ReturnsAsync(account);

            var res = await _service.RegisterUserAsync(req);
            Assert.Equal(RegistrationResult.EmailAlreadyExists, res);
        }

        [Fact]
        public async Task Register_PendingUser_ResendsVerification()
        {
            var req = new RegisterUserRequest { Username = USER, Email = EMAIL, Password = PASS };
            var pendingPlayer = new Player
            {
                Username = USER,
                Account = new Account { AccountStatus = (int)AccountStatus.Pending, Email = EMAIL }
            };

            _repositoryMock.Setup(r => r.GetPlayerByUsernameAsync(USER)).ReturnsAsync(pendingPlayer);
            _repositoryMock.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

            var res = await _service.RegisterUserAsync(req);
            Assert.Equal(RegistrationResult.EmailPendingVerification, res);
            _emailMock.Verify(e => e.SendVerificationEmailAsync(EMAIL, It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task Register_Success_SavesAndSendsEmail()
        {
            var req = new RegisterUserRequest { Username = USER, Email = EMAIL, Password = PASS };

            _repositoryMock.Setup(r => r.GetPlayerByUsernameAsync(USER)).ReturnsAsync((Player)null);
            _repositoryMock.Setup(r => r.GetAccountByEmailAsync(EMAIL)).ReturnsAsync((Account)null);
            _repositoryMock.Setup(r => r.AddPlayer(It.IsAny<Player>()));
            _repositoryMock.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

            var res = await _service.RegisterUserAsync(req);
            Assert.Equal(RegistrationResult.Success, res);
            _repositoryMock.Verify(r => r.AddPlayer(It.Is<Player>(p => p.Username == USER)), Times.Once);
        }

        [Fact]
        public async Task Register_NetworkTimeout_ReturnsFatalError()
        {
            var req = new RegisterUserRequest { Username = USER, Email = EMAIL, Password = PASS };
            _repositoryMock.Setup(r => r.GetPlayerByUsernameAsync(USER)).ThrowsAsync(new TimeoutException());

            await Assert.ThrowsAsync<FaultException<ServiceFault>>(() => _service.RegisterUserAsync(req));
        }

        [Theory]
        [InlineData(null, "pass")]
        [InlineData("", "pass")]
        [InlineData("  ", "pass")]
        [InlineData("user", null)]
        [InlineData("user", "")]
        [InlineData("user", "  ")]
        public async Task Login_InvalidInput_ReturnsInvalidCredentials(string u, string p)
        {
            _repositoryMock.Setup(r => r.GetPlayerForLoginAsync(u)).ReturnsAsync((Player)null);

            var res = await _service.LogInAsync(u, p);
            Assert.False(res.IsSuccess);
        }

        [Fact]
        public async Task Login_UserNotFound_ReturnsInvalidCredentials()
        {
            _repositoryMock.Setup(r => r.GetPlayerForLoginAsync(USER)).ReturnsAsync((Player)null);
            var res = await _service.LogInAsync(USER, PASS);
            Assert.False(res.IsSuccess);
            Assert.Equal("InvalidCredentials", res.Message);
        }

        [Fact]
        public async Task Login_Banned_ReturnsUserBanned()
        {
            var p = new Player { IsBanned = true, Account = new Account { AccountStatus = (int)AccountStatus.Banned } };
            _repositoryMock.Setup(r => r.GetPlayerForLoginAsync(USER)).ReturnsAsync(p);

            var res = await _service.LogInAsync(USER, PASS);
            Assert.Equal("UserBanned", res.Message);
        }

        [Fact]
        public async Task Login_Inactive_ReturnsAccountInactive()
        {
            var p = new Player { Account = new Account { AccountStatus = (int)AccountStatus.Inactive } };
            _repositoryMock.Setup(r => r.GetPlayerForLoginAsync(USER)).ReturnsAsync(p);

            var res = await _service.LogInAsync(USER, PASS);
            Assert.Equal("AccountInactive", res.Message);
        }

        [Fact]
        public async Task Login_WrongPassword_ReturnsInvalidCredentials()
        {
            string realHash = BCrypt.Net.BCrypt.HashPassword("RealPassword");
            var p = new Player { Username = USER, Account = new Account { PasswordHash = realHash, AccountStatus = (int)AccountStatus.Active } };

            _repositoryMock.Setup(r => r.GetPlayerForLoginAsync(USER)).ReturnsAsync(p);

            var res = await _service.LogInAsync(USER, "WrongPass");
            Assert.False(res.IsSuccess);
        }

        [Fact]
        public async Task Login_Success_ReturnsTokenAndNotifies()
        {
            string realHash = BCrypt.Net.BCrypt.HashPassword(PASS);
            var p = new Player { Username = USER, Account = new Account { PasswordHash = realHash, AccountStatus = (int)AccountStatus.Active, Email = EMAIL } };

            _repositoryMock.Setup(r => r.GetPlayerForLoginAsync(USER)).ReturnsAsync(p);
            _connectionMock.Setup(c => c.IsUserOnline(USER)).Returns(false);
            _connectionMock.Setup(c => c.AddUser(USER));

            var res = await _service.LogInAsync(USER, PASS);

            Assert.True(res.IsSuccess);
            _connectionMock.Verify(c => c.AddUser(USER), Times.Once);
        }

        [Fact]
        public async Task Login_AlreadyOnline_ReturnsUserAlreadyOnline()
        {
            var p = new Player
            {
                Username = USER,
                Account = new Account { AccountStatus = (int)AccountStatus.Active }
            };

            _repositoryMock.Setup(r => r.GetPlayerForLoginAsync(USER)).ReturnsAsync(p);
            _connectionMock.Setup(c => c.IsUserOnline(USER)).Returns(true);

            var res = await _service.LogInAsync(USER, PASS);

            Assert.Equal("UserAlreadyOnline", res.Message);
        }

        [Fact]
        public async Task Login_TimeoutException_ReturnsDatabaseError()
        {
            _repositoryMock.Setup(r => r.GetPlayerForLoginAsync(USER)).ThrowsAsync(new TimeoutException());
            await Assert.ThrowsAsync<FaultException<ServiceFault>>(() => _service.LogInAsync(USER, PASS));
        }

        [Fact]
        public async Task Guest_Success_CreatesUser()
        {
            _repositoryMock.Setup(r => r.IsUsernameTaken(It.IsAny<string>())).Returns(false);
            _repositoryMock.Setup(r => r.AddPlayer(It.IsAny<Player>()));
            _repositoryMock.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
            _connectionMock.Setup(c => c.AddUser(It.IsAny<string>()));

            var res = await _service.LoginAsGuestAsync();
            Assert.True(res.Success);
            Assert.StartsWith("Guest_", res.Username);
        }

        [Fact]
        public async Task Guest_DbError_Handled()
        {
            _repositoryMock.Setup(r => r.IsUsernameTaken(It.IsAny<string>())).Returns(false);
            _repositoryMock.Setup(r => r.AddPlayer(It.IsAny<Player>()));
            _repositoryMock.Setup(r => r.SaveChangesAsync()).ThrowsAsync(new Exception());

            await Assert.ThrowsAsync<FaultException<ServiceFault>>(() => _service.LoginAsGuestAsync());
        }

        [Theory]
        [InlineData("Code", "Wrong")]
        [InlineData("Code", null)]
        [InlineData("Code", "")]
        public void VerifyAccount_InvalidCode_ReturnsFalse(string actual, string attempt)
        {
            var acc = new Account { VerificationCode = actual, CodeExpiration = DateTime.Now.AddHours(1) };
            _repositoryMock.Setup(r => r.GetAccountByEmail(EMAIL)).Returns(acc);

            var res = _service.VerifyAccount(EMAIL, attempt);
            Assert.False(res);
        }

        [Fact]
        public void VerifyAccount_Expired_ReturnsFalse()
        {
            var acc = new Account { VerificationCode = "123", CodeExpiration = DateTime.Now.AddHours(-1) };
            _repositoryMock.Setup(r => r.GetAccountByEmail(EMAIL)).Returns(acc);

            var res = _service.VerifyAccount(EMAIL, "123");
            Assert.False(res);
        }

        [Fact]
        public void VerifyAccount_Success_ActivatesAccount()
        {
            var acc = new Account { VerificationCode = "123", CodeExpiration = DateTime.Now.AddHours(1), AccountStatus = (int)AccountStatus.Pending };
            _repositoryMock.Setup(r => r.GetAccountByEmail(EMAIL)).Returns(acc);
            _repositoryMock.Setup(r => r.SaveChanges());

            var res = _service.VerifyAccount(EMAIL, "123");
            Assert.True(res);
            Assert.Equal((int)AccountStatus.Active, acc.AccountStatus);
            Assert.Null(acc.VerificationCode);
        }

        [Fact]
        public void VerifyAccount_AccountNotFound_ReturnsFalse()
        {
            _repositoryMock.Setup(r => r.GetAccountByEmail(It.IsAny<string>())).Returns((Account)null);
            var res = _service.VerifyAccount("ghost@mail.com", "123");
            Assert.False(res);
        }

        [Fact]
        public async Task ResendVerification_AccountNotFound_ReturnsFalse()
        {
            _repositoryMock.Setup(r => r.GetAccountByEmailAsync(EMAIL)).ReturnsAsync((Account)null);
            var res = await _service.ResendVerificationCodeAsync(EMAIL);
            Assert.False(res);
        }

        [Fact]
        public async Task ResendVerification_NotPending_ReturnsFalse()
        {
            var acc = new Account { AccountStatus = (int)AccountStatus.Active };
            _repositoryMock.Setup(r => r.GetAccountByEmailAsync(EMAIL)).ReturnsAsync(acc);
            var res = await _service.ResendVerificationCodeAsync(EMAIL);
            Assert.False(res);
        }

        [Fact]
        public async Task ResendVerification_Success_SendsEmail()
        {
            var acc = new Account { AccountStatus = (int)AccountStatus.Pending };
            _repositoryMock.Setup(r => r.GetAccountByEmailAsync(EMAIL)).ReturnsAsync(acc);
            _repositoryMock.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
            _emailMock.Setup(e => e.SendVerificationEmailAsync(EMAIL, It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);

            var res = await _service.ResendVerificationCodeAsync(EMAIL);
            Assert.True(res);
            Assert.NotNull(acc.VerificationCode);
        }

        [Fact]
        public async Task ResetReq_EmailNotFound_ReturnsTrueSecurity()
        {
            _repositoryMock.Setup(r => r.GetAccountByEmailAsync(EMAIL)).ReturnsAsync((Account)null);
            var res = await _service.RequestPasswordResetAsync(EMAIL);
            Assert.True(res);
        }

        [Fact]
        public async Task ResetReq_Success_GeneratesCodeAndSendsEmail()
        {
            var acc = new Account { Email = EMAIL };
            _repositoryMock.Setup(r => r.GetAccountByEmailAsync(EMAIL)).ReturnsAsync(acc);
            _repositoryMock.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
            _emailMock.Setup(e => e.SendRecoveryEmailAsync(EMAIL, It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);

            var res = await _service.RequestPasswordResetAsync(EMAIL);

            Assert.True(res);
            Assert.NotNull(acc.VerificationCode);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void VerifyRecoveryCode_DelegatesToRepository(bool expected)
        {
            _repositoryMock.Setup(r => r.VerifyRecoveryCode(EMAIL, "123")).Returns(expected);
            var res = _service.VerifyRecoveryCode(EMAIL, "123");
            Assert.Equal(expected, res);
        }

        [Fact]
        public async Task ChangePass_UserNotFound_ReturnsFalse()
        {
            _repositoryMock.Setup(r => r.GetPlayerByUsernameAsync(USER)).ReturnsAsync((Player)null);
            var res = await _service.ChangeUserPasswordAsync(USER, "Old", "New");
            Assert.False(res);
        }

        [Fact]
        public async Task ChangePass_WrongOldPass_ReturnsFalse()
        {
            var p = new Player { Account = new Account { PasswordHash = BCrypt.Net.BCrypt.HashPassword("RealPass") } };
            _repositoryMock.Setup(r => r.GetPlayerByUsernameAsync(USER)).ReturnsAsync(p);

            var res = await _service.ChangeUserPasswordAsync(USER, "WrongPass", "New");
            Assert.False(res);
        }

        [Fact]
        public async Task ChangePass_SameAsOld_ReturnsFalse()
        {
            var p = new Player { Account = new Account { PasswordHash = BCrypt.Net.BCrypt.HashPassword(PASS) } };
            _repositoryMock.Setup(r => r.GetPlayerByUsernameAsync(USER)).ReturnsAsync(p);

            var res = await _service.ChangeUserPasswordAsync(USER, PASS, PASS);
            Assert.False(res);
        }

        [Fact]
        public async Task ChangePass_Success_UpdatesHash()
        {
            var acc = new Account { PasswordHash = BCrypt.Net.BCrypt.HashPassword("OldPass") };
            var p = new Player { Username = USER, Account = acc };

            _repositoryMock.Setup(r => r.GetPlayerByUsernameAsync(USER)).ReturnsAsync(p);
            _repositoryMock.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

            var res = await _service.ChangeUserPasswordAsync(USER, "OldPass", "NewPassSecure");

            Assert.True(res);
            Assert.False(BCrypt.Net.BCrypt.Verify("OldPass", acc.PasswordHash));
            Assert.True(BCrypt.Net.BCrypt.Verify("NewPassSecure", acc.PasswordHash));
        }

        [Fact]
        public void UpdatePassword_UserNotFound_ReturnsFalse()
        {
            _repositoryMock.Setup(r => r.GetPlayerForLoginAsync(EMAIL)).ReturnsAsync((Player)null);
            var res = _service.UpdatePassword(EMAIL, "NewPass");
            Assert.False(res);
        }

        [Fact]
        public void UpdatePassword_SameAsOld_ReturnsFalse()
        {
            var p = new Player { Account = new Account { PasswordHash = BCrypt.Net.BCrypt.HashPassword("Pass") } };
            _repositoryMock.Setup(r => r.GetPlayerForLoginAsync(EMAIL)).ReturnsAsync(p);

            var res = _service.UpdatePassword(EMAIL, "Pass");
            Assert.False(res);
        }

        [Fact]
        public void UpdatePassword_Success_UpdatesAndNotifies()
        {
            var acc = new Account
            {
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Old"),
                PreferredLanguage = "es-MX"
            };
            var p = new Player { Username = USER, Account = acc };

            _repositoryMock.Setup(r => r.GetPlayerForLoginAsync(It.IsAny<string>())).ReturnsAsync(p);

            _repositoryMock.Setup(r => r.SaveChanges());

            var res = _service.UpdatePassword(EMAIL, "New");

            Assert.True(res);
            Assert.True(BCrypt.Net.BCrypt.Verify("New", acc.PasswordHash));
        }
    }
}