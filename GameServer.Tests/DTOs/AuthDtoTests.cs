using GameServer.DTOs.Auth;
using Xunit;

namespace GameServer.Tests.DTOs
{
    public class AuthDtoTests
    {
        [Theory]
        [InlineData(true, "Guest_123", "Login Successful")]
        [InlineData(false, null, "Server Full")]
        [InlineData(true, "Guest_999", "Welcome")]
        [InlineData(false, "", "Error")]
        [InlineData(true, "Abc_1", "Ok")]
        [InlineData(false, "Err", "Failed")]
        [InlineData(true, "User_X", "Logged")]
        [InlineData(false, null, null)]
        [InlineData(true, "Guest_Final", "Success")]
        [InlineData(false, "Invalid", "Banned")]
        public void GuestLoginResult_Integrity(bool success, string user, string msg)
        {
            var dto = new GuestLoginResult { Success = success, Username = user, Message = msg };
            Assert.Equal(success, dto.Success);
            Assert.Equal(user, dto.Username);
            Assert.Equal(msg, dto.Message);
        }

        [Theory]
        [InlineData(true, "Welcome back", "es-MX")]
        [InlineData(true, "Hello", "en-US")]
        [InlineData(false, "Invalid credentials", "es-MX")]
        [InlineData(false, "Banned account", "en-US")]
        [InlineData(true, "Success", "fr-FR")]
        [InlineData(true, "Bienvenido", "es-ES")]
        [InlineData(false, "Error", null)]
        [InlineData(true, "Re-login", "pt-BR")]
        [InlineData(false, "Locked", "it-IT")]
        [InlineData(true, "Admin access", "en-GB")]
        public void LoginResponseDto_Integrity(bool success, string msg, string lang)
        {
            var dto = new LoginResponseDto { IsSuccess = success, Message = msg, PreferredLanguage = lang };
            Assert.Equal(success, dto.IsSuccess);
            Assert.Equal(msg, dto.Message);
            Assert.Equal(lang, dto.PreferredLanguage);
        }

        [Theory]
        [InlineData("Pablo", "pablo@test.com", "Pass123", "es-MX")]
        [InlineData("Admin", "admin@game.com", "Admin@2024", "en-US")]
        [InlineData("Player1", "p1@mail.com", "root", "pt-BR")]
        [InlineData("TestUser", "test@test.org", "password", "fr-FR")]
        [InlineData("User5", "u5@provider.net", "123456", "it-IT")]
        [InlineData("Alpha", "alpha@game.io", "alpha123", "de-DE")]
        [InlineData("Beta", "beta@game.io", "beta123", "ru-RU")]
        [InlineData("Guest", "guest@guest.com", "guest", "ja-JP")]
        [InlineData("N00b", "n00b@internerd.com", "n00b", "zh-CN")]
        [InlineData("Legend", "legend@pro.com", "L3g3nd!", "ko-KR")]
        public void RegisterUserRequest_Integrity(string user, string email, string pass, string lang)
        {
            var dto = new RegisterUserRequest
            {
                Username = user,
                Email = email,
                Password = pass,
                PreferredLanguage = lang
            };
            Assert.Equal(user, dto.Username);
            Assert.Equal(email, dto.Email);
            Assert.Equal(pass, dto.Password);
            Assert.Equal(lang, dto.PreferredLanguage);
        }

        [Fact]
        public void RegistrationResult_Enum_VerifyValues()
        {
            Assert.Equal(0, (int)RegistrationResult.Success);
            Assert.Equal(1, (int)RegistrationResult.UsernameAlreadyExists);
            Assert.Equal(2, (int)RegistrationResult.EmailAlreadyExists);
            Assert.Equal(3, (int)RegistrationResult.EmailPendingVerification);
            Assert.Equal(4, (int)RegistrationResult.FatalError);
        }
    }
}