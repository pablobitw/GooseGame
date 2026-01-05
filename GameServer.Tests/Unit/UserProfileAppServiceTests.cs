using GameServer.Repositories.Interfaces;
using global::GameServer.DTOs.User;
using global::GameServer.Models;
using global::GameServer.Repositories;
using global::GameServer.Services.Logic;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace GameServer.Tests.Services
{
    public class UserProfileAppServiceTests
    {
        private readonly Mock<IUserProfileRepository> _mockRepo;
        private readonly UserProfileAppService _service;

        public UserProfileAppServiceTests()
        {
            _mockRepo = new Mock<IUserProfileRepository>();
            _service = new UserProfileAppService(_mockRepo.Object);
        }

        #region Pruebas de Perfil
        [Fact]
        public async Task GetUserProfile_ExistingPlayer_ReturnsProfile()
        {
            var player = new Player { Username = "User1", Coins = 100, IsGuest = false, Account = new Account { Email = "test@test.com" } };
            _mockRepo.Setup(r => r.GetPlayerWithDetailsAsync("User1")).ReturnsAsync(player);

            var result = await _service.GetUserProfileAsync("User1");

            Assert.NotNull(result);
            Assert.Equal("User1", result.Username);
            Assert.Equal("test@test.com", result.Email);
        }

        [Fact]
        public async Task GetUserProfile_GuestPlayer_ReturnsEmailAsInvitado()
        {
            var player = new Player { Username = "Guest1", IsGuest = true };
            _mockRepo.Setup(r => r.GetPlayerWithDetailsAsync("Guest1")).ReturnsAsync(player);

            var result = await _service.GetUserProfileAsync("Guest1");

            Assert.Equal("Invitado", result.Email);
        }
        #endregion

        #region Pruebas de Cambio de Username
        [Fact]
        public async Task ChangeUsername_Success_ReturnsSuccess()
        {
            var player = new Player
            {
                Username = "Old",
                UsernameChangeCount = 0,
                Account = new Account { VerificationCode = "123", CodeExpiration = DateTime.Now.AddMinutes(10), Email = "e@e.com" }
            };
            _mockRepo.Setup(r => r.GetPlayerWithDetailsAsync("Old")).ReturnsAsync(player);
            _mockRepo.Setup(r => r.IsUsernameTakenAsync("New")).ReturnsAsync(false);

            var result = await _service.ChangeUsernameAsync("Old", "New", "123");

            Assert.Equal(UsernameChangeResult.Success, result);
            Assert.Equal("New", player.Username);
            Assert.Equal(1, player.UsernameChangeCount);
        }

        [Fact]
        public async Task ChangeUsername_LimitReached_ReturnsLimitReached()
        {
            var player = new Player
            {
                UsernameChangeCount = 3,
                Account = new Account
                {
                    VerificationCode = "123",
                    CodeExpiration = DateTime.Now.AddMinutes(10)
                }
            };
            _mockRepo.Setup(r => r.GetPlayerWithDetailsAsync("Any")).ReturnsAsync(player);

            var result = await _service.ChangeUsernameAsync("Any", "New", "123");

            Assert.Equal(UsernameChangeResult.LimitReached, result);
        }

        [Fact]
        public async Task ChangeUsername_WrongCode_ReturnsFatalError()
        {
            var player = new Player { Account = new Account { VerificationCode = "Correct", CodeExpiration = DateTime.Now.AddMinutes(10) } };
            _mockRepo.Setup(r => r.GetPlayerWithDetailsAsync("Any")).ReturnsAsync(player);

            var result = await _service.ChangeUsernameAsync("Any", "New", "Wrong");

            Assert.Equal(UsernameChangeResult.FatalError, result);
        }
        #endregion

        #region Pruebas de Redes Sociales
        [Fact]
        public async Task AddSocialLink_ValidFacebook_ReturnsNullError()
        {
            var player = new Player { IdPlayer = 1, PlayerSocialLinks = new List<PlayerSocialLink>() };
            _mockRepo.Setup(r => r.GetPlayerWithDetailsAsync("User1")).ReturnsAsync(player);

            var result = await _service.AddSocialLinkAsync("User1", "https://facebook.com/user");

            Assert.Null(result);
            _mockRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task RemoveSocialLink_ExistingLink_ReturnsTrue()
        {
            var link = new PlayerSocialLink { Url = "site.com" };
            var player = new Player { PlayerSocialLinks = new List<PlayerSocialLink> { link } };
            _mockRepo.Setup(r => r.GetPlayerWithDetailsAsync("User1")).ReturnsAsync(player);

            var result = await _service.RemoveSocialLinkAsync("User1", "site.com");

            Assert.True(result);
            _mockRepo.Verify(r => r.DeleteSocialLink(link), Times.Once);
        }
        #endregion

        #region Pruebas de Seguridad y Cuenta
        [Fact]
        public async Task DeactivateAccount_CorrectPassword_ReturnsTrue()
        {
            string pass = "12345";
            string hash = BCrypt.Net.BCrypt.HashPassword(pass);
            var player = new Player { Account = new Account { PasswordHash = hash, AccountStatus = 1 }, IsGuest = false };
            _mockRepo.Setup(r => r.GetPlayerWithDetailsAsync("User1")).ReturnsAsync(player);

            var result = await _service.DeactivateAccountAsync(new DeactivateAccountRequest { Username = "User1", Password = pass });

            Assert.True(result);
            Assert.Equal(2, player.Account.AccountStatus);
        }

        [Fact]
        public async Task UpdateLanguage_ValidCode_TruncatesAndSaves()
        {
            var player = new Player { Account = new Account() };
            _mockRepo.Setup(r => r.GetPlayerWithDetailsAsync("User1")).ReturnsAsync(player);

            var result = await _service.UpdateLanguageAsync("User1", "es-MX-Extra");

            Assert.True(result);
            Assert.Equal("es-MX", player.Account.PreferredLanguage);
        }
        #endregion
    }
}