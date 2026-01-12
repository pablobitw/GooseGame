#nullable disable
using GameServer.DTOs.User;
using GameServer.Faults;
using GameServer.Helpers;
using GameServer.Models;
using GameServer.Repositories.Interfaces;
using GameServer.Services.Common;
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
    public sealed class UserProfileAppServiceTests
    {
        private readonly Mock<IUserProfileRepository> _repoMock;
        private readonly Mock<IEmailService> _emailMock;
        private readonly Mock<ISecurityCodeGenerator> _codeGenMock;
        private readonly Mock<IPasswordHasher> _hasherMock;
        private readonly UserProfileAppService _service;

        private const string USER = "TestUser";
        private const string EMAIL = "test@example.com";
        private const string CODE = "123456";
        private const string HASH = "hashed_pw";

        public UserProfileAppServiceTests()
        {
            _repoMock = new Mock<IUserProfileRepository>();
            _emailMock = new Mock<IEmailService>();
            _codeGenMock = new Mock<ISecurityCodeGenerator>();
            _hasherMock = new Mock<IPasswordHasher>();

            _service = new UserProfileAppService(
                _repoMock.Object,
                _emailMock.Object,
                _codeGenMock.Object,
                _hasherMock.Object
            );
        }

        private SqlException CreateSqlException()
        {
            var exception = FormatterServices.GetUninitializedObject(typeof(SqlException)) as SqlException;
            var errors = FormatterServices.GetUninitializedObject(typeof(SqlErrorCollection)) as SqlErrorCollection;

            var error = FormatterServices.GetUninitializedObject(typeof(SqlError)) as SqlError;
            var errorsListField = typeof(SqlErrorCollection).GetField("errors", BindingFlags.NonPublic | BindingFlags.Instance);
            if (errorsListField != null)
            {
                var list = new System.Collections.ArrayList { error };
                errorsListField.SetValue(errors, list);
            }

            var errorsField = typeof(SqlException).GetField("_errors", BindingFlags.NonPublic | BindingFlags.Instance);
            if (errorsField != null) errorsField.SetValue(exception, errors);

            return exception;
        }

        [Fact]
        public async Task GetUserProfileAsync_UserNotFound_ReturnsNull()
        {
            _repoMock.Setup(r => r.GetPlayerWithDetailsAsync(USER)).ReturnsAsync((Player)null);
            var result = await _service.GetUserProfileAsync(USER);
            Assert.Null(result);
        }

        [Fact]
        public async Task GetUserProfileAsync_GuestUser_ReturnsGuestProfile()
        {
            var player = new Player { Username = USER, IsGuest = true, PlayerStat = new PlayerStat() };
            _repoMock.Setup(r => r.GetPlayerWithDetailsAsync(USER)).ReturnsAsync(player);

            var result = await _service.GetUserProfileAsync(USER);

            Assert.NotNull(result);
            Assert.Equal("Invitado", result.Email);
            Assert.Equal(USER, result.Username);
        }

        [Fact]
        public async Task GetUserProfileAsync_RegisteredUser_ReturnsFullProfile()
        {
            var player = new Player
            {
                Username = USER,
                IsGuest = false,
                Account = new Account { Email = EMAIL, PreferredLanguage = "es-MX" },
                PlayerStat = new PlayerStat { MatchesPlayed = 10, MatchesWon = 5 },
                PlayerSocialLinks = new List<PlayerSocialLink> { new PlayerSocialLink { SocialType = 1, Url = "fb.com" } }
            };
            _repoMock.Setup(r => r.GetPlayerWithDetailsAsync(USER)).ReturnsAsync(player);

            var result = await _service.GetUserProfileAsync(USER);

            Assert.NotNull(result);
            Assert.Equal(EMAIL, result.Email);
            Assert.Equal(10, result.MatchesPlayed);
            Assert.Equal(5, result.MatchesWon);
            Assert.Single(result.SocialLinks);
        }

        [Fact]
        public async Task GetUserProfileAsync_SqlException_ThrowsFault()
        {
            _repoMock.Setup(r => r.GetPlayerWithDetailsAsync(USER)).ThrowsAsync(CreateSqlException());
            await Assert.ThrowsAsync<FaultException<ServiceFault>>(() => _service.GetUserProfileAsync(USER));
        }

        [Fact]
        public async Task GetUserProfileAsync_EntityException_ThrowsFault()
        {
            _repoMock.Setup(r => r.GetPlayerWithDetailsAsync(USER)).ThrowsAsync(new EntityException());
            await Assert.ThrowsAsync<FaultException<ServiceFault>>(() => _service.GetUserProfileAsync(USER));
        }

        [Fact]
        public async Task GetUserProfileAsync_TimeoutException_ThrowsFault()
        {
            _repoMock.Setup(r => r.GetPlayerWithDetailsAsync(USER)).ThrowsAsync(new TimeoutException());
            await Assert.ThrowsAsync<FaultException<ServiceFault>>(() => _service.GetUserProfileAsync(USER));
        }

        [Fact]
        public async Task GetUserProfileAsync_GeneralException_ThrowsFault()
        {
            _repoMock.Setup(r => r.GetPlayerWithDetailsAsync(USER)).ThrowsAsync(new Exception());
            await Assert.ThrowsAsync<FaultException<ServiceFault>>(() => _service.GetUserProfileAsync(USER));
        }

        [Fact]
        public async Task SendUsernameChangeCodeAsync_UserNotFound_ReturnsFalse()
        {
            _repoMock.Setup(r => r.GetPlayerWithDetailsAsync(USER)).ReturnsAsync((Player)null);
            var result = await _service.SendUsernameChangeCodeAsync(USER);
            Assert.False(result);
        }

        [Fact]
        public async Task SendUsernameChangeCodeAsync_GuestUser_ReturnsFalse()
        {
            var player = new Player { IsGuest = true };
            _repoMock.Setup(r => r.GetPlayerWithDetailsAsync(USER)).ReturnsAsync(player);
            var result = await _service.SendUsernameChangeCodeAsync(USER);
            Assert.False(result);
        }

        [Fact]
        public async Task SendUsernameChangeCodeAsync_Success_ReturnsTrue()
        {
            var player = new Player { IsGuest = false, Account = new Account { Email = EMAIL, PreferredLanguage = "en" } };
            _repoMock.Setup(r => r.GetPlayerWithDetailsAsync(USER)).ReturnsAsync(player);
            _codeGenMock.Setup(c => c.Next(100000, 999999)).Returns(123456);
            _emailMock.Setup(e => e.SendVerificationEmailAsync(EMAIL, "123456", "en")).ReturnsAsync(true);

            var result = await _service.SendUsernameChangeCodeAsync(USER);

            Assert.True(result);
            Assert.Equal("123456", player.Account.VerificationCode);
            _repoMock.Verify(r => r.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task SendUsernameChangeCodeAsync_Exception_ThrowsFault()
        {
            _repoMock.Setup(r => r.GetPlayerWithDetailsAsync(USER)).ThrowsAsync(new Exception());
            await Assert.ThrowsAsync<FaultException<ServiceFault>>(() => _service.SendUsernameChangeCodeAsync(USER));
        }

        [Fact]
        public async Task ChangeUsernameAsync_UserNotFound_ReturnsUserNotFound()
        {
            _repoMock.Setup(r => r.GetPlayerWithDetailsAsync(USER)).ReturnsAsync((Player)null);
            var result = await _service.ChangeUsernameAsync(USER, "NewName", CODE);
            Assert.Equal(UsernameChangeResult.UserNotFound, result);
        }



        [Fact]
        public async Task ChangeUsernameAsync_LimitReached_ReturnsLimitReached()
        {
            var player = new Player
            {
                UsernameChangeCount = 3,
                Account = new Account { VerificationCode = CODE, CodeExpiration = DateTime.Now.AddMinutes(5) }
            };
            _repoMock.Setup(r => r.GetPlayerWithDetailsAsync(USER)).ReturnsAsync(player);

            var result = await _service.ChangeUsernameAsync(USER, "NewName", CODE);

            Assert.Equal(UsernameChangeResult.LimitReached, result);
        }

        [Fact]
        public async Task ChangeUsernameAsync_UsernameTaken_ReturnsUsernameAlreadyExists()
        {
            var player = new Player
            {
                UsernameChangeCount = 0,
                Account = new Account { VerificationCode = CODE, CodeExpiration = DateTime.Now.AddMinutes(5) }
            };
            _repoMock.Setup(r => r.GetPlayerWithDetailsAsync(USER)).ReturnsAsync(player);
            _repoMock.Setup(r => r.IsUsernameTakenAsync("TakenName")).ReturnsAsync(true);

            var result = await _service.ChangeUsernameAsync(USER, "TakenName", CODE);

            Assert.Equal(UsernameChangeResult.UsernameAlreadyExists, result);
        }

        [Fact]
        public async Task ChangeUsernameAsync_Success_UpdatesAndResets()
        {
            var player = new Player
            {
                Username = USER,
                UsernameChangeCount = 0,
                Account = new Account { Email = EMAIL, VerificationCode = CODE, CodeExpiration = DateTime.Now.AddMinutes(5), PreferredLanguage = "en" }
            };
            _repoMock.Setup(r => r.GetPlayerWithDetailsAsync(USER)).ReturnsAsync(player);
            _repoMock.Setup(r => r.IsUsernameTakenAsync("NewName")).ReturnsAsync(false);

            var result = await _service.ChangeUsernameAsync(USER, "NewName", CODE);

            Assert.Equal(UsernameChangeResult.Success, result);
            Assert.Equal("NewName", player.Username);
            Assert.Equal(1, player.UsernameChangeCount);
            Assert.Null(player.Account.VerificationCode);
            _emailMock.Verify(e => e.SendUsernameChangedNotificationAsync(EMAIL, USER, "NewName", "en"), Times.Once);
        }

        [Fact]
        public async Task ChangeUsernameAsync_SqlException_ThrowsFault()
        {
            _repoMock.Setup(r => r.GetPlayerWithDetailsAsync(USER)).ThrowsAsync(CreateSqlException());
            await Assert.ThrowsAsync<FaultException<ServiceFault>>(() => _service.ChangeUsernameAsync(USER, "New", CODE));
        }

        [Fact]
        public async Task ChangeUsernameAsync_GeneralException_ThrowsFault()
        {
            _repoMock.Setup(r => r.GetPlayerWithDetailsAsync(USER)).ThrowsAsync(new Exception());
            await Assert.ThrowsAsync<FaultException<ServiceFault>>(() => _service.ChangeUsernameAsync(USER, "New", CODE));
        }

        [Fact]
        public async Task ChangeAvatarAsync_UserNotFound_ReturnsFalse()
        {
            _repoMock.Setup(r => r.GetPlayerWithDetailsAsync(USER)).ReturnsAsync((Player)null);
            var result = await _service.ChangeAvatarAsync(USER, "img.png");
            Assert.False(result);
        }

        [Fact]
        public async Task ChangeAvatarAsync_Success_ReturnsTrue()
        {
            var player = new Player { Avatar = "old.png" };
            _repoMock.Setup(r => r.GetPlayerWithDetailsAsync(USER)).ReturnsAsync(player);

            var result = await _service.ChangeAvatarAsync(USER, "new.png");

            Assert.True(result);
            Assert.Equal("new.png", player.Avatar);
            _repoMock.Verify(r => r.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task ChangeAvatarAsync_Exception_ThrowsFault()
        {
            _repoMock.Setup(r => r.GetPlayerWithDetailsAsync(USER)).ThrowsAsync(new Exception());
            await Assert.ThrowsAsync<FaultException<ServiceFault>>(() => _service.ChangeAvatarAsync(USER, "img.png"));
        }

        [Fact]
        public async Task SendPasswordChangeCodeAsync_UserNotFound_ReturnsFalse()
        {
            _repoMock.Setup(r => r.GetPlayerWithDetailsAsync(USER)).ReturnsAsync((Player)null);
            var result = await _service.SendPasswordChangeCodeAsync(USER);
            Assert.False(result);
        }

        [Fact]
        public async Task SendPasswordChangeCodeAsync_Guest_ReturnsFalse()
        {
            var player = new Player { IsGuest = true };
            _repoMock.Setup(r => r.GetPlayerWithDetailsAsync(USER)).ReturnsAsync(player);
            var result = await _service.SendPasswordChangeCodeAsync(USER);
            Assert.False(result);
        }

        [Fact]
        public async Task SendPasswordChangeCodeAsync_Success_ReturnsTrue()
        {
            var player = new Player { IsGuest = false, Account = new Account { Email = EMAIL, PreferredLanguage = "fr" } };
            _repoMock.Setup(r => r.GetPlayerWithDetailsAsync(USER)).ReturnsAsync(player);
            _codeGenMock.Setup(c => c.Next(100000, 999999)).Returns(999999);
            _emailMock.Setup(e => e.SendVerificationEmailAsync(EMAIL, "999999", "fr")).ReturnsAsync(true);

            var result = await _service.SendPasswordChangeCodeAsync(USER);

            Assert.True(result);
            Assert.Equal("999999", player.Account.VerificationCode);
        }

        [Fact]
        public async Task SendPasswordChangeCodeAsync_Exception_ThrowsFault()
        {
            _repoMock.Setup(r => r.GetPlayerWithDetailsAsync(USER)).ThrowsAsync(new Exception());
            await Assert.ThrowsAsync<FaultException<ServiceFault>>(() => _service.SendPasswordChangeCodeAsync(USER));
        }

        [Fact]
        public async Task ChangePasswordWithCodeAsync_UserNotFound_ReturnsFalse()
        {
            var req = new ChangePasswordRequest { Email = EMAIL };
            _repoMock.Setup(r => r.GetPlayerWithDetailsAsync(EMAIL)).ReturnsAsync((Player)null);
            var result = await _service.ChangePasswordWithCodeAsync(req);
            Assert.False(result);
        }

        [Fact]
        public async Task ChangePasswordWithCodeAsync_InvalidCode_ReturnsFalse()
        {
            var player = new Player { Account = new Account { VerificationCode = "999", CodeExpiration = DateTime.Now.AddMinutes(5) } };
            var req = new ChangePasswordRequest { Email = EMAIL, Code = CODE };
            _repoMock.Setup(r => r.GetPlayerWithDetailsAsync(EMAIL)).ReturnsAsync(player);

            var result = await _service.ChangePasswordWithCodeAsync(req);
            Assert.False(result);
        }

        [Fact]
        public async Task ChangePasswordWithCodeAsync_ReusePassword_ReturnsFalse()
        {
            var player = new Player { Account = new Account { VerificationCode = CODE, CodeExpiration = DateTime.Now.AddMinutes(5), PasswordHash = HASH } };
            var req = new ChangePasswordRequest { Email = EMAIL, Code = CODE, NewPassword = "samePassword" };

            _repoMock.Setup(r => r.GetPlayerWithDetailsAsync(EMAIL)).ReturnsAsync(player);
            _hasherMock.Setup(h => h.Verify("samePassword", HASH)).Returns(true);

            var result = await _service.ChangePasswordWithCodeAsync(req);
            Assert.False(result);
        }

        [Fact]
        public async Task ChangePasswordWithCodeAsync_Success_ReturnsTrue()
        {
            var player = new Player
            {
                Username = USER,
                Account = new Account { VerificationCode = CODE, CodeExpiration = DateTime.Now.AddMinutes(5), PasswordHash = HASH, PreferredLanguage = "en", Email = EMAIL }
            };
            var req = new ChangePasswordRequest { Email = EMAIL, Code = CODE, NewPassword = "newPassword" };

            _repoMock.Setup(r => r.GetPlayerWithDetailsAsync(EMAIL)).ReturnsAsync(player);
            _hasherMock.Setup(h => h.Verify("newPassword", HASH)).Returns(false);
            _hasherMock.Setup(h => h.HashPassword("newPassword")).Returns("new_hash");

            var result = await _service.ChangePasswordWithCodeAsync(req);

            Assert.True(result);
            Assert.Equal("new_hash", player.Account.PasswordHash);
            Assert.Null(player.Account.VerificationCode);
            _emailMock.Verify(e => e.SendPasswordChangedNotificationAsync(EMAIL, USER, "en"), Times.Once);
        }

        [Fact]
        public async Task ChangePasswordWithCodeAsync_Exception_ThrowsFault()
        {
            _repoMock.Setup(r => r.GetPlayerWithDetailsAsync(It.IsAny<string>())).ThrowsAsync(new Exception());
            var req = new ChangePasswordRequest();
            await Assert.ThrowsAsync<FaultException<ServiceFault>>(() => _service.ChangePasswordWithCodeAsync(req));
        }

        [Fact]
        public async Task UpdateLanguageAsync_UserNotFound_ReturnsFalse()
        {
            _repoMock.Setup(r => r.GetPlayerWithDetailsAsync(USER)).ReturnsAsync((Player)null);
            var result = await _service.UpdateLanguageAsync(USER, "en");
            Assert.False(result);
        }

        [Fact]
        public async Task UpdateLanguageAsync_Success_TruncatesCode()
        {
            var player = new Player { Account = new Account() };
            _repoMock.Setup(r => r.GetPlayerWithDetailsAsync(USER)).ReturnsAsync(player);

            var result = await _service.UpdateLanguageAsync(USER, "en-US-Extra");

            Assert.True(result);
            Assert.Equal("en-US", player.Account.PreferredLanguage);
            _repoMock.Verify(r => r.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task UpdateLanguageAsync_Exception_ThrowsFault()
        {
            _repoMock.Setup(r => r.GetPlayerWithDetailsAsync(USER)).ThrowsAsync(new Exception());
            await Assert.ThrowsAsync<FaultException<ServiceFault>>(() => _service.UpdateLanguageAsync(USER, "en"));
        }

        [Fact]
        public async Task DeactivateAccountAsync_UserNotFound_ReturnsFalse()
        {
            var req = new DeactivateAccountRequest { Username = USER };
            _repoMock.Setup(r => r.GetPlayerWithDetailsAsync(USER)).ReturnsAsync((Player)null);
            var result = await _service.DeactivateAccountAsync(req);
            Assert.False(result);
        }

        [Fact]
        public async Task DeactivateAccountAsync_WrongPassword_ReturnsFalse()
        {
            var player = new Player { IsGuest = false, Account = new Account { PasswordHash = HASH } };
            var req = new DeactivateAccountRequest { Username = USER, Password = "wrong" };

            _repoMock.Setup(r => r.GetPlayerWithDetailsAsync(USER)).ReturnsAsync(player);
            _hasherMock.Setup(h => h.Verify("wrong", HASH)).Returns(false);

            var result = await _service.DeactivateAccountAsync(req);
            Assert.False(result);
        }

        [Fact]
        public async Task DeactivateAccountAsync_Success_ReturnsTrue()
        {
            var player = new Player { IsGuest = false, Account = new Account { PasswordHash = HASH, AccountStatus = 1 } };
            var req = new DeactivateAccountRequest { Username = USER, Password = "correct" };

            _repoMock.Setup(r => r.GetPlayerWithDetailsAsync(USER)).ReturnsAsync(player);
            _hasherMock.Setup(h => h.Verify("correct", HASH)).Returns(true);

            var result = await _service.DeactivateAccountAsync(req);

            Assert.True(result);
            Assert.Equal(2, player.Account.AccountStatus);
            _repoMock.Verify(r => r.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task DeactivateAccountAsync_Exception_ThrowsFault()
        {
            _repoMock.Setup(r => r.GetPlayerWithDetailsAsync(USER)).ThrowsAsync(new Exception());
            await Assert.ThrowsAsync<FaultException<ServiceFault>>(() => _service.DeactivateAccountAsync(new DeactivateAccountRequest { Username = USER }));
        }

        [Fact]
        public async Task AddSocialLinkAsync_UserNotFound_ReturnsError()
        {
            _repoMock.Setup(r => r.GetPlayerWithDetailsAsync(USER)).ReturnsAsync((Player)null);
            var result = await _service.AddSocialLinkAsync(USER, "fb.com");
            Assert.Equal("Usuario no encontrado.", result);
        }

        [Fact]
        public async Task AddSocialLinkAsync_Success_ReturnsNull()
        {
            var player = new Player { IdPlayer = 1, PlayerSocialLinks = new List<PlayerSocialLink>() };
            _repoMock.Setup(r => r.GetPlayerWithDetailsAsync(USER)).ReturnsAsync(player);

            var result = await _service.AddSocialLinkAsync(USER, "https://facebook.com/user");

            Assert.Null(result);
            _repoMock.Verify(r => r.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task AddSocialLinkAsync_Exception_ThrowsFault()
        {
            _repoMock.Setup(r => r.GetPlayerWithDetailsAsync(USER)).ThrowsAsync(new Exception());
            await Assert.ThrowsAsync<FaultException<ServiceFault>>(() => _service.AddSocialLinkAsync(USER, "url"));
        }

        [Fact]
        public async Task RemoveSocialLinkAsync_UserNotFound_ReturnsFalse()
        {
            _repoMock.Setup(r => r.GetPlayerWithDetailsAsync(USER)).ReturnsAsync((Player)null);
            var result = await _service.RemoveSocialLinkAsync(USER, "url");
            Assert.False(result);
        }

        [Fact]
        public async Task RemoveSocialLinkAsync_LinkNotFound_ReturnsFalse()
        {
            var player = new Player { PlayerSocialLinks = new List<PlayerSocialLink>() };
            _repoMock.Setup(r => r.GetPlayerWithDetailsAsync(USER)).ReturnsAsync(player);
            var result = await _service.RemoveSocialLinkAsync(USER, "url");
            Assert.False(result);
        }

        [Fact]
        public async Task RemoveSocialLinkAsync_Success_ReturnsTrue()
        {
            var link = new PlayerSocialLink { Url = "url" };
            var player = new Player { PlayerSocialLinks = new List<PlayerSocialLink> { link } };
            _repoMock.Setup(r => r.GetPlayerWithDetailsAsync(USER)).ReturnsAsync(player);

            var result = await _service.RemoveSocialLinkAsync(USER, "url");

            Assert.True(result);
            _repoMock.Verify(r => r.DeleteSocialLink(link), Times.Once);
            _repoMock.Verify(r => r.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task RemoveSocialLinkAsync_Exception_ThrowsFault()
        {
            _repoMock.Setup(r => r.GetPlayerWithDetailsAsync(USER)).ThrowsAsync(new Exception());
            await Assert.ThrowsAsync<FaultException<ServiceFault>>(() => _service.RemoveSocialLinkAsync(USER, "url"));
        }
    }
}