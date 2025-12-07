using GameServer.DTOs.Auth;
using GameServer.Interfaces;
using GameServer.Repositories;
using GameServer.Services.Logic;
using System.Threading.Tasks;

namespace GameServer.Services
{
    public class GameService : IGameService
    {
        private readonly AuthAppService _authLogic;

        public GameService()
        {
            var repository = new AuthRepository();
            _authLogic = new AuthAppService(repository);
        }

        public async Task<RegistrationResult> RegisterUserAsync(RegisterUserRequest request)
        {
            RegistrationResult result;
            result = await _authLogic.RegisterUserAsync(request);
            return result;
        }

        public async Task<bool> LogInAsync(string usernameOrEmail, string password)
        {
            bool result;
            result = await _authLogic.LogInAsync(usernameOrEmail, password);
            return result;
        }

        public void Logout(string username)
        {
            _authLogic.Logout(username);
        }

        public bool VerifyAccount(string email, string code)
        {
            bool result;
            result = _authLogic.VerifyAccount(email, code);
            return result;
        }

        public async Task<bool> RequestPasswordResetAsync(string email)
        {
            bool result;
            result = await _authLogic.RequestPasswordResetAsync(email);
            return result;
        }

        public bool VerifyRecoveryCode(string email, string code)
        {
            bool result;
            result = _authLogic.VerifyRecoveryCode(email, code);
            return result;
        }

        public bool UpdatePassword(string email, string newPassword)
        {
            bool result;
            result = _authLogic.UpdatePassword(email, newPassword);
            return result;
        }

        public async Task<bool> ResendVerificationCodeAsync(string email)
        {
            bool result;
            result = await _authLogic.ResendVerificationCodeAsync(email);
            return result;
        }
    }
}