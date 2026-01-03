using GameServer.DTOs.Auth;
using GameServer.Interfaces;
using GameServer.Repositories;
using GameServer.Repositories.Interfaces;
using GameServer.Services.Logic;
using System.ServiceModel;
using System.Threading.Tasks;

namespace GameServer.Services
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerCall, ConcurrencyMode = ConcurrencyMode.Multiple)]
    public class GameService : IGameService
    {
        private readonly AuthAppService _authLogic;

        public GameService() : this(new AuthRepository())
        {
        }

        public GameService(IAuthRepository repository)
        {
            _authLogic = new AuthAppService(repository);
        }

        public async Task<RegistrationResult> RegisterUserAsync(RegisterUserRequest request)
        {
            return await _authLogic.RegisterUserAsync(request);
        }

        public async Task<LoginResponseDto> LogInAsync(string usernameOrEmail, string password)
        {
            return await _authLogic.LogInAsync(usernameOrEmail, password);
        }

        public async Task<GuestLoginResult> LoginAsGuestAsync()
        {
            return await _authLogic.LoginAsGuestAsync();
        }

        public void Logout(string username)
        {
            AuthAppService.Logout(username);
        }

        public bool VerifyAccount(string email, string code)
        {
            return _authLogic.VerifyAccount(email, code);
        }

        public async Task<bool> RequestPasswordResetAsync(string email)
        {
            return await _authLogic.RequestPasswordResetAsync(email);
        }

        public bool VerifyRecoveryCode(string email, string code)
        {
            return _authLogic.VerifyRecoveryCode(email, code);
        }

        public bool UpdatePassword(string email, string newPassword)
        {
            return _authLogic.UpdatePassword(email, newPassword);
        }

        public async Task<bool> ResendVerificationCodeAsync(string email)
        {
            return await _authLogic.ResendVerificationCodeAsync(email);
        }

        public async Task<bool> ChangeUserPasswordAsync(string username, string currentPassword, string newPassword)
        {
            return await _authLogic.ChangeUserPasswordAsync(username, currentPassword, newPassword);
        }
    }
}