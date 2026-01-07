using GameServer.DTOs.Auth;
using GameServer.Helpers;
using GameServer.Repositories.Interfaces;
using log4net;
using System;
using System.Data.Entity.Core;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Validation;
using System.Data.SqlClient;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace GameServer.Services.Logic
{
    public class AuthAppService
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(AuthAppService));
        private const string DefaultAvatar = "pack://application:,,,/Assets/Avatar/default_avatar.png";
        private const int CodeExpirationMinutes = 15;
        private readonly IAuthRepository _repository;

        public AuthAppService(IAuthRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        private bool CheckInternetConnection()
        {
            bool isConnected = false;
            try
            {
                isConnected = NetworkInterface.GetIsNetworkAvailable();
            }
            catch (NetworkInformationException ex)
            {
                Log.Warn("NetworkInformationException checking internet: " + ex.Message);
                isConnected = true;
            }
            catch (Exception ex)
            {
                Log.Warn("General error checking internet: " + ex.Message);
                isConnected = true;
            }
            return isConnected;
        }

        public async Task<GuestLoginResult> LoginAsGuestAsync()
        {
            var result = new GuestLoginResult { Success = false, Message = "DbError" };
            try
            {
                string guestName;
                bool isTaken;
                do
                {
                    guestName = string.Format("Guest_{0}", Guid.NewGuid().ToString().Substring(0, 6));
                    isTaken = _repository.IsUsernameTaken(guestName);
                } while (isTaken);

                var newGuestPlayer = new Player
                {
                    Username = guestName,
                    Coins = 0,
                    Avatar = DefaultAvatar,
                    UsernameChangeCount = 0,
                    IsGuest = true,
                    TicketCommon = 0,
                    TicketRare = 0,
                    TicketEpic = 0,
                    TicketLegendary = 0,
                    PlayerStat = new PlayerStat
                    {
                        MatchesPlayed = 0,
                        MatchesWon = 0,
                        MatchesLost = 0,
                        LuckyBoxOpened = 0
                    }
                };

                _repository.AddPlayer(newGuestPlayer);
                await _repository.SaveChangesAsync();

                ConnectionManager.AddUser(guestName);
                Log.InfoFormat("Guest created: {0}", guestName);

                result.Success = true;
                result.Username = guestName;
                result.Message = "GuestLoginSuccess";
            }
            catch (DbUpdateException ex)
            {
                Log.Error("DbUpdateException creating guest", ex);
                result.Message = "DbError";
            }
            catch (SqlException ex)
            {
                Log.Fatal("SqlException creating guest", ex);
                result.Message = "DbError";
            }
            catch (EntityException ex)
            {
                Log.Error("EntityException creating guest", ex);
                result.Message = "DbError";
            }
            catch (Exception ex)
            {
                Log.Fatal("General Exception creating guest", ex);
                result.Message = "DbError";
            }
            return result;
        }

        public async Task<RegistrationResult> RegisterUserAsync(RegisterUserRequest request)
        {
            RegistrationResult result = RegistrationResult.FatalError;

            if (request != null &&
                !string.IsNullOrWhiteSpace(request.Username) &&
                !string.IsNullOrWhiteSpace(request.Email) &&
                !string.IsNullOrWhiteSpace(request.Password))
            {
                try
                {
                    result = await CheckUsernameAvailability(request.Username);

                    if (result == RegistrationResult.Success)
                    {
                        result = await CheckEmailAvailability(request.Email);

                        if (result == RegistrationResult.Success)
                        {
                            result = await CreateNewUser(request);
                        }
                    }
                }
                catch (SqlException ex)
                {
                    Log.Fatal("SqlException in RegisterUserAsync", ex);
                    result = RegistrationResult.FatalError;
                }
                catch (EntityException ex)
                {
                    Log.Error("EntityException in RegisterUserAsync", ex);
                    result = RegistrationResult.FatalError;
                }
                catch (TimeoutException ex)
                {
                    Log.Error("TimeoutException in RegisterUserAsync", ex);
                    result = RegistrationResult.FatalError;
                }
                catch (Exception ex)
                {
                    Log.Fatal("General Exception in RegisterUserAsync", ex);
                    result = RegistrationResult.FatalError;
                }
            }

            return result;
        }

        private async Task<RegistrationResult> CheckUsernameAvailability(string username)
        {
            RegistrationResult result = RegistrationResult.Success;
            var existingPlayer = await _repository.GetPlayerByUsernameAsync(username);

            if (existingPlayer != null)
            {
                if (existingPlayer.Account != null && existingPlayer.Account.AccountStatus == (int)AccountStatus.Pending)
                {
                    result = await ResendVerificationForPlayer(existingPlayer);
                }
                else
                {
                    result = RegistrationResult.UsernameAlreadyExists;
                }
            }
            return result;
        }

        private async Task<RegistrationResult> CheckEmailAvailability(string email)
        {
            RegistrationResult result = RegistrationResult.Success;
            var existingAccount = await _repository.GetAccountByEmailAsync(email);

            if (existingAccount != null)
            {
                if (existingAccount.AccountStatus == (int)AccountStatus.Pending)
                {
                    result = await ResendVerificationForAccount(existingAccount);
                }
                else
                {
                    result = RegistrationResult.EmailAlreadyExists;
                }
            }
            return result;
        }

        private async Task<RegistrationResult> ResendVerificationForPlayer(Player player)
        {
            string newCode = GenerateSecureCode();
            player.Account.VerificationCode = newCode;
            player.Account.CodeExpiration = DateTime.Now.AddMinutes(CodeExpirationMinutes);

            await _repository.SaveChangesAsync();

            bool emailSent = await EmailHelper.SendVerificationEmailAsync(player.Account.Email, newCode, player.Account.PreferredLanguage).ConfigureAwait(false);
            if (!emailSent) Log.Warn($"Failed to resend verification email to {player.Username}");

            return RegistrationResult.EmailPendingVerification;
        }

        private async Task<RegistrationResult> ResendVerificationForAccount(Account account)
        {
            string newCode = GenerateSecureCode();
            account.VerificationCode = newCode;
            account.CodeExpiration = DateTime.Now.AddMinutes(CodeExpirationMinutes);

            await _repository.SaveChangesAsync();

            bool emailSent = await EmailHelper.SendVerificationEmailAsync(account.Email, newCode, account.PreferredLanguage).ConfigureAwait(false);
            if (!emailSent) Log.Warn($"Failed to resend verification email to {account.Email}");

            return RegistrationResult.EmailPendingVerification;
        }

        private async Task<RegistrationResult> CreateNewUser(RegisterUserRequest request)
        {
            RegistrationResult result = RegistrationResult.FatalError;
            string verifyCode = GenerateSecureCode();
            string hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password);

            var newAccount = new Account
            {
                Email = request.Email,
                PasswordHash = hashedPassword,
                RegisterDate = DateTime.Now,
                AccountStatus = (int)AccountStatus.Pending,
                VerificationCode = verifyCode,
                CodeExpiration = DateTime.Now.AddMinutes(CodeExpirationMinutes),
                PreferredLanguage = request.PreferredLanguage ?? "es-MX"
            };

            var newPlayer = new Player
            {
                Username = request.Username,
                Coins = 0,
                Avatar = DefaultAvatar,
                Account = newAccount,
                PlayerStat = new PlayerStat(),
                IsGuest = false,
                TicketCommon = 0,
                TicketRare = 0,
                TicketEpic = 0,
                TicketLegendary = 0
            };

            try
            {
                _repository.AddPlayer(newPlayer);
                await _repository.SaveChangesAsync();

                bool emailSent = await EmailHelper.SendVerificationEmailAsync(request.Email, verifyCode, newAccount.PreferredLanguage).ConfigureAwait(false);
                if (!emailSent)
                {
                    Log.Warn($"User {request.Username} registered, but email failed to send.");
                }

                result = RegistrationResult.Success;
            }
            catch (DbUpdateException ex)
            {
                Log.Error("DbUpdateException in CreateNewUser", ex);
                result = RegistrationResult.FatalError;
            }
            catch (SqlException ex)
            {
                Log.Fatal("SqlException in CreateNewUser", ex);
                result = RegistrationResult.FatalError;
            }
            catch (DbEntityValidationException ex)
            {
                Log.Error("DbEntityValidationException in CreateNewUser", ex);
                result = RegistrationResult.FatalError;
            }
            catch (Exception ex)
            {
                Log.Fatal("General Exception in CreateNewUser", ex);
                result = RegistrationResult.FatalError;
            }

            return result;
        }

        public async Task<LoginResponseDto> LogInAsync(string usernameOrEmail, string password)
        {
            var response = new LoginResponseDto
            {
                IsSuccess = false,
                Message = "InvalidCredentials",
                PreferredLanguage = "es-MX"
            };

            try
            {
                var player = await _repository.GetPlayerForLoginAsync(usernameOrEmail);

                if (player != null)
                {
                    bool shouldContinue = true;

                    if (player.IsBanned)
                    {
                        response.Message = "UserBanned";
                        shouldContinue = false;
                    }

                    if (shouldContinue && !IsAccountActive(player))
                    {
                        response.Message = "AccountInactive";
                        shouldContinue = false;
                    }

                    if (shouldContinue && ConnectionManager.IsUserOnline(player.Username))
                    {
                        response.Message = "UserAlreadyOnline";
                        shouldContinue = false;
                    }

                    if (shouldContinue)
                    {
                        response = await ProcessSuccessfulLogin(player, password, response);
                    }
                }
            }
            catch (SqlException ex)
            {
                Log.Fatal("SqlException en Login", ex);
                response.Message = "DatabasebError";
            }
            catch (EntityException ex)
            {
                Log.Error("EntityException in Login", ex);
                response.Message = "DatabasebError";
            }
            catch (TimeoutException ex)
            {
                Log.Error("TimeoutException in Login", ex);
                response.Message = "DatabasebError";
            }
            catch (Exception ex)
            {
                Log.Fatal("General Exception en Login", ex);
                response.Message = "DatabasebError";
            }

            return response;
        }

        private static bool IsAccountActive(Player player)
        {
            bool isActive = false;
            if (player.Account != null)
            {
                isActive = player.Account.AccountStatus != (int)AccountStatus.Inactive &&
                           player.Account.AccountStatus != (int)AccountStatus.Banned;
            }
            return isActive;
        }

        private async Task<LoginResponseDto> ProcessSuccessfulLogin(Player player, string password, LoginResponseDto response)
        {
            if (ValidateLoginCredentials(player, password))
            {
                if (player.GameIdGame != null)
                {
                    player.GameIdGame = null;
                    await _repository.SaveChangesAsync();
                }

                ConnectionManager.AddUser(player.Username);

                _ = Task.Run(() => EmailHelper.SendLoginNotificationAsync(player.Account.Email, player.Username, player.Account.PreferredLanguage));

                response.IsSuccess = true;
                response.Message = "Success";
                response.PreferredLanguage = player.Account.PreferredLanguage ?? "es-MX";
            }
            return response;
        }

        private static bool ValidateLoginCredentials(Player player, string password)
        {
            bool isValid = false;
            if (player.Account != null && !player.IsGuest)
            {
                isValid = BCrypt.Net.BCrypt.Verify(password, player.Account.PasswordHash) &&
                          player.Account.AccountStatus == (int)AccountStatus.Active;
            }
            return isValid;
        }

        public static void Logout(string username)
        {
            ConnectionManager.RemoveUser(username);
        }

        public bool VerifyAccount(string email, string code)
        {
            bool isVerified = false;
            try
            {
                var account = _repository.GetAccountByEmail(email);
                if (account != null && account.VerificationCode == code && account.CodeExpiration >= DateTime.Now)
                {
                    account.AccountStatus = (int)AccountStatus.Active;
                    account.VerificationCode = null;
                    account.CodeExpiration = null;

                    _repository.SaveChanges();
                    isVerified = true;
                }
            }
            catch (SqlException ex)
            {
                Log.Fatal("SqlException en VerifyAccount", ex);
            }
            catch (EntityException ex)
            {
                Log.Error("EntityException en VerifyAccount", ex);
            }
            catch (Exception ex)
            {
                Log.Fatal("General Exception en VerifyAccount", ex);
            }
            return isVerified;
        }

        public async Task<bool> RequestPasswordResetAsync(string email)
        {
            bool result = false;
            try
            {
                var account = await _repository.GetAccountByEmailAsync(email);
                if (account == null)
                {
                    result = true;
                }
                else
                {
                    string verifyCode = GenerateSecureCode();
                    account.VerificationCode = verifyCode;
                    account.CodeExpiration = DateTime.Now.AddMinutes(CodeExpirationMinutes);

                    await _repository.SaveChangesAsync();
                    result = await EmailHelper.SendRecoveryEmailAsync(email, verifyCode, account.PreferredLanguage).ConfigureAwait(false);
                }
            }
            catch (SqlException ex)
            {
                Log.Fatal("SqlException en RequestPasswordReset", ex);
            }
            catch (EntityException ex)
            {
                Log.Error("EntityException en RequestPasswordReset", ex);
            }
            catch (Exception ex)
            {
                Log.Fatal("General Exception en RequestPasswordReset", ex);
            }
            return result;
        }

        public bool VerifyRecoveryCode(string email, string code)
        {
            bool isValid = false;
            try
            {
                isValid = _repository.VerifyRecoveryCode(email, code);
            }
            catch (SqlException ex)
            {
                Log.Fatal("SqlException en VerifyRecoveryCode", ex);
            }
            catch (EntityException ex)
            {
                Log.Error("EntityException en VerifyRecoveryCode", ex);
            }
            catch (Exception ex)
            {
                Log.Fatal("General Exception en VerifyRecoveryCode", ex);
            }
            return isValid;
        }

        public bool UpdatePassword(string email, string newPassword)
        {
            bool isUpdated = false;
            try
            {
                var player = _repository.GetPlayerForLoginAsync(email).Result;
                if (player != null && player.Account != null)
                {
                    var account = player.Account;
                    if (!BCrypt.Net.BCrypt.Verify(newPassword, account.PasswordHash))
                    {
                        string newHashedPassword = BCrypt.Net.BCrypt.HashPassword(newPassword);
                        account.PasswordHash = newHashedPassword;
                        account.VerificationCode = null;
                        account.CodeExpiration = null;

                        _repository.SaveChanges();
                        _ = Task.Run(() => EmailHelper.SendPasswordChangedNotificationAsync(email, player.Username, account.PreferredLanguage));
                        isUpdated = true;
                    }
                }
            }
            catch (SqlException ex)
            {
                Log.Fatal("SqlException en UpdatePassword", ex);
            }
            catch (EntityException ex)
            {
                Log.Error("EntityException en UpdatePassword", ex);
            }
            catch (Exception ex)
            {
                Log.Fatal("General Exception en UpdatePassword", ex);
            }
            return isUpdated;
        }

        public async Task<bool> ResendVerificationCodeAsync(string email)
        {
            bool result = false;
            try
            {
                var account = await _repository.GetAccountByEmailAsync(email);
                if (account != null && account.AccountStatus == (int)AccountStatus.Pending)
                {
                    string newCode = GenerateSecureCode();
                    account.VerificationCode = newCode;
                    account.CodeExpiration = DateTime.Now.AddMinutes(CodeExpirationMinutes);

                    await _repository.SaveChangesAsync();
                    result = await EmailHelper.SendVerificationEmailAsync(email, newCode, account.PreferredLanguage).ConfigureAwait(false);
                }
            }
            catch (SqlException ex)
            {
                Log.Fatal("SqlException en ResendVerificationCode", ex);
            }
            catch (EntityException ex)
            {
                Log.Error("EntityException en ResendVerificationCode", ex);
            }
            catch (Exception ex)
            {
                Log.Fatal("General Exception en ResendVerificationCode", ex);
            }
            return result;
        }

        public async Task<bool> ChangeUserPasswordAsync(string username, string currentPassword, string newPassword)
        {
            bool result = false;
            try
            {
                var player = await _repository.GetPlayerByUsernameAsync(username);
                if (player != null && player.Account != null)
                {
                    if (BCrypt.Net.BCrypt.Verify(currentPassword, player.Account.PasswordHash) &&
                        currentPassword != newPassword)
                    {
                        string newHashedPassword = BCrypt.Net.BCrypt.HashPassword(newPassword);
                        player.Account.PasswordHash = newHashedPassword;

                        await _repository.SaveChangesAsync();
                        _ = Task.Run(() => EmailHelper.SendPasswordChangedNotificationAsync(player.Account.Email, player.Username, player.Account.PreferredLanguage));
                        result = true;
                    }
                }
            }
            catch (SqlException ex)
            {
                Log.Fatal("SqlException en ChangeUserPassword", ex);
            }
            catch (EntityException ex)
            {
                Log.Error("EntityException en ChangeUserPassword", ex);
            }
            catch (Exception ex)
            {
                Log.Fatal("General Exception en ChangeUserPassword", ex);
            }
            return result;
        }

        private static string GenerateSecureCode()
        {
            string code;
            using (var rng = new RNGCryptoServiceProvider())
            {
                byte[] data = new byte[4];
                rng.GetBytes(data);
                int value = BitConverter.ToInt32(data, 0);
                code = Math.Abs(value % 1000000).ToString("D6");
            }
            return code;
        }
    }
}