using GameServer.DTOs.Auth;
using GameServer.Helpers;
using GameServer.Repositories;
using log4net;
using System;
using System.Data.Entity.Core;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Validation;
using System.Data.SqlClient;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace GameServer.Services.Logic
{
    public class AuthAppService
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(AuthAppService));
        private const string DefaultAvatar = "pack://application:,,,/Assets/Avatar/default_avatar.png";
        private const int CodeExpirationMinutes = 15;
        private readonly AuthRepository _repository;

        public AuthAppService(AuthRepository repository)
        {
            _repository = repository;
        }

        public async Task<GuestLoginResult> LoginAsGuestAsync()
        {
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
                Log.InfoFormat("Invitado creado: {0}", guestName);

                return new GuestLoginResult
                {
                    Success = true,
                    Username = guestName,
                    Message = "Bienvenido Invitado"
                };
            }
            catch (DbUpdateException ex)
            {
                Log.Error("Error actualizando BD al crear invitado.", ex);
                return new GuestLoginResult { Success = false, Message = "Error de base de datos." };
            }
            catch (SqlException ex)
            {
                Log.Fatal("Error SQL crítico al crear invitado.", ex);
                return new GuestLoginResult { Success = false, Message = "Error de conexión." };
            }
            catch (EntityException ex)
            {
                Log.Error("Error de Entity Framework al crear invitado.", ex);
                return new GuestLoginResult { Success = false, Message = "Error interno." };
            }
            catch (TimeoutException ex)
            {
                Log.Error("Timeout al crear invitado.", ex);
                return new GuestLoginResult { Success = false, Message = "Tiempo de espera agotado." };
            }
        }

        public async Task<RegistrationResult> RegisterUserAsync(RegisterUserRequest request)
        {
            if (request == null) return RegistrationResult.FatalError;

            try
            {
                var usernameCheck = await CheckUsernameAvailability(request.Username);
                if (usernameCheck != RegistrationResult.Success)
                {
                    return usernameCheck;
                }

                var emailCheck = await CheckEmailAvailability(request.Email);
                if (emailCheck != RegistrationResult.Success)
                {
                    return emailCheck;
                }

                return await CreateNewUser(request);
            }
            catch (DbEntityValidationException ex)
            {
                foreach (var err in ex.EntityValidationErrors)
                {
                    Log.WarnFormat("Error validación entidad: {0}", err.Entry.Entity.GetType().Name);
                }
                return RegistrationResult.FatalError;
            }
            catch (DbUpdateException ex)
            {
                Log.ErrorFormat("Error actualizando base de datos para {0}. Excepcion: {1}", request.Username, ex);
                return RegistrationResult.FatalError;
            }
            catch (SqlException ex)
            {
                Log.Fatal("Error SQL crítico en registro.", ex);
                return RegistrationResult.FatalError;
            }
            catch (EntityCommandExecutionException ex)
            {
                Log.Error("Error ejecutando comando de entidad en registro.", ex);
                return RegistrationResult.FatalError;
            }
            catch (TimeoutException ex)
            {
                Log.Error("Tiempo de espera agotado en registro.", ex);
                return RegistrationResult.FatalError;
            }
        }

        private async Task<RegistrationResult> CheckUsernameAvailability(string username)
        {
            var existingPlayer = await _repository.GetPlayerByUsernameAsync(username);
            if (existingPlayer == null) return RegistrationResult.Success;

            if (existingPlayer.Account != null && existingPlayer.Account.AccountStatus == (int)AccountStatus.Pending)
            {
                return await ResendVerificationForPlayer(existingPlayer);
            }

            Log.WarnFormat("Registro fallido: Usuario '{0}' ya existe.", username);
            return RegistrationResult.UsernameAlreadyExists;
        }

        private async Task<RegistrationResult> CheckEmailAvailability(string email)
        {
            var existingAccount = await _repository.GetAccountByEmailAsync(email);
            if (existingAccount == null) return RegistrationResult.Success;

            if (existingAccount.AccountStatus == (int)AccountStatus.Pending)
            {
                return await ResendVerificationForAccount(existingAccount);
            }

            Log.WarnFormat("Registro fallido: Email '{0}' en uso.", email);
            return RegistrationResult.EmailAlreadyExists;
        }

        private async Task<RegistrationResult> ResendVerificationForPlayer(Player player)
        {
            string newCode = GenerateSecureCode();
            player.Account.VerificationCode = newCode;
            player.Account.CodeExpiration = DateTime.Now.AddMinutes(CodeExpirationMinutes);
            await _repository.SaveChangesAsync();
            await EmailHelper.SendVerificationEmailAsync(player.Account.Email, newCode).ConfigureAwait(false);
            return RegistrationResult.EmailPendingVerification;
        }

        private async Task<RegistrationResult> ResendVerificationForAccount(Account account)
        {
            string newCode = GenerateSecureCode();
            account.VerificationCode = newCode;
            account.CodeExpiration = DateTime.Now.AddMinutes(CodeExpirationMinutes);
            await _repository.SaveChangesAsync();
            await EmailHelper.SendVerificationEmailAsync(account.Email, newCode).ConfigureAwait(false);
            return RegistrationResult.EmailPendingVerification;
        }

        private async Task<RegistrationResult> CreateNewUser(RegisterUserRequest request)
        {
            string verifyCode = GenerateSecureCode();
            string hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password);

            var newAccount = new Account
            {
                Email = request.Email,
                PasswordHash = hashedPassword,
                RegisterDate = DateTime.Now,
                AccountStatus = (int)AccountStatus.Pending,
                VerificationCode = verifyCode,
                CodeExpiration = DateTime.Now.AddMinutes(CodeExpirationMinutes)
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

            _repository.AddPlayer(newPlayer);
            await _repository.SaveChangesAsync();
            await EmailHelper.SendVerificationEmailAsync(request.Email, verifyCode).ConfigureAwait(false);

            return RegistrationResult.Success;
        }

        public async Task<bool> LogInAsync(string usernameOrEmail, string password)
        {
            bool result = false;

            try
            {
                var player = await _repository.GetPlayerForLoginAsync(usernameOrEmail);

                if (player != null)
                {
                    if (!ConnectionManager.IsUserOnline(player.Username))
                    {
                        if (ValidateLoginCredentials(player, password))
                        {
                            if (player.GameIdGame != null)
                            {
                                player.GameIdGame = null;
                                await _repository.SaveChangesAsync();
                            }

                            ConnectionManager.AddUser(player.Username);

                            _ = Task.Run(() => EmailHelper.SendLoginNotificationAsync(player.Account.Email, player.Username));

                            result = true;
                        }
                    }
                    else
                    {
                        Log.WarnFormat("Intento de doble login rechazado para: {0}", player.Username);
                    }
                }
            }
            catch (SqlException ex)
            {
                Log.Fatal("Error SQL en Login.", ex);
            }
            catch (EntityException ex)
            {
                Log.Error("Error de Entity Framework en Login.", ex);
            }
            catch (DbUpdateException ex)
            {
                Log.Error("Error al actualizar el estado del jugador en Login.", ex);
            }
            catch (TimeoutException ex)
            {
                Log.Error("Timeout en Login.", ex);
            }

            return result;
        }

        private bool ValidateLoginCredentials(Player player, string password)
        {
            if (player.Account == null || player.IsGuest) return false;

            return BCrypt.Net.BCrypt.Verify(password, player.Account.PasswordHash) &&
                   player.Account.AccountStatus == (int)AccountStatus.Active;
        }

        public static void Logout(string username)
        {
            ConnectionManager.RemoveUser(username);
            Log.InfoFormat("Usuario {0} cerró sesión.", username);
        }

        public bool VerifyAccount(string email, string code)
        {
            try
            {
                var account = _repository.GetAccountByEmail(email);
                if (account != null && account.VerificationCode == code && account.CodeExpiration >= DateTime.Now)
                {
                    account.AccountStatus = (int)AccountStatus.Active;
                    account.VerificationCode = null;
                    account.CodeExpiration = null;

                    _repository.SaveChanges();
                    Log.InfoFormat("Cuenta verificada: {0}", email);
                    return true;
                }

                Log.WarnFormat("Fallo verificación para: {0}", email);
                return false;
            }
            catch (SqlException ex)
            {
                Log.Fatal("Error SQL en Verificación.", ex);
                return false;
            }
            catch (EntityException ex)
            {
                Log.Error("Error Entity Framework en Verificación.", ex);
                return false;
            }
            catch (TimeoutException ex)
            {
                Log.Error("Timeout en Verificación.", ex);
                return false;
            }
        }

        public async Task<bool> RequestPasswordResetAsync(string email)
        {
            try
            {
                var account = await _repository.GetAccountByEmailAsync(email);
                if (account == null) return true;

                string verifyCode = GenerateSecureCode();
                account.VerificationCode = verifyCode;
                account.CodeExpiration = DateTime.Now.AddMinutes(CodeExpirationMinutes);

                await _repository.SaveChangesAsync();
                return await EmailHelper.SendRecoveryEmailAsync(email, verifyCode).ConfigureAwait(false);
            }
            catch (SqlException ex)
            {
                Log.Fatal("Error SQL en Reset Password.", ex);
                return false;
            }
            catch (EntityException ex)
            {
                Log.Error("Error Entity Framework en Reset Password.", ex);
                return false;
            }
            catch (TimeoutException ex)
            {
                Log.Error("Timeout en Reset Password.", ex);
                return false;
            }
        }

        public bool VerifyRecoveryCode(string email, string code)
        {
            try
            {
                bool isValid = _repository.VerifyRecoveryCode(email, code);
                if (isValid) Log.InfoFormat("Recovery code verified for: {0}", email);
                else Log.WarnFormat("Failed recovery code attempt for: {0}", email);
                return isValid;
            }
            catch (SqlException ex)
            {
                Log.Fatal("Error SQL en Verificar Código Recuperación.", ex);
                return false;
            }
            catch (EntityException ex)
            {
                Log.Error("Error EF en Verificar Código Recuperación.", ex);
                return false;
            }
            catch (TimeoutException ex)
            {
                Log.Error("Timeout en Verificar Código Recuperación.", ex);
                return false;
            }
        }

        public bool UpdatePassword(string email, string newPassword)
        {
            try
            {
                var account = _repository.GetAccountByEmail(email);
                if (account == null)
                {
                    Log.ErrorFormat("Attempt to update password for non-existent account: {0}", email);
                    return false;
                }

                if (BCrypt.Net.BCrypt.Verify(newPassword, account.PasswordHash))
                {
                    Log.WarnFormat("Intento de reusar contraseña para: {0}", email);
                    return false;
                }

                string newHashedPassword = BCrypt.Net.BCrypt.HashPassword(newPassword);
                account.PasswordHash = newHashedPassword;
                account.VerificationCode = null;
                account.CodeExpiration = null;

                _repository.SaveChanges();
                Log.InfoFormat("Contraseña actualizada para: {0}", email);
                return true;
            }
            catch (SqlException ex)
            {
                Log.Fatal("Error SQL en Actualizar Password.", ex);
                return false;
            }
            catch (EntityException ex)
            {
                Log.Error("Error EF en Actualizar Password.", ex);
                return false;
            }
            catch (TimeoutException ex)
            {
                Log.Error("Timeout en Actualizar Password.", ex);
                return false;
            }
        }

        public async Task<bool> ResendVerificationCodeAsync(string email)
        {
            try
            {
                var account = await _repository.GetAccountByEmailAsync(email);
                if (account != null && account.AccountStatus == (int)AccountStatus.Pending)
                {
                    string newCode = GenerateSecureCode();
                    account.VerificationCode = newCode;
                    account.CodeExpiration = DateTime.Now.AddMinutes(CodeExpirationMinutes);

                    await _repository.SaveChangesAsync();
                    bool emailSent = await EmailHelper.SendVerificationEmailAsync(email, newCode).ConfigureAwait(false);
                    if (emailSent)
                    {
                        Log.InfoFormat("Verification code resent to {0}.", email);
                        return true;
                    }
                }
                else
                {
                    Log.WarnFormat("Resend code requested for invalid account: {0}", email);
                }
                return false;
            }
            catch (SqlException ex)
            {
                Log.Fatal("Error SQL en Reenvío Código.", ex);
                return false;
            }
            catch (EntityException ex)
            {
                Log.Error("Error EF en Reenvío Código.", ex);
                return false;
            }
            catch (TimeoutException ex)
            {
                Log.Error("Timeout en Reenvío Código.", ex);
                return false;
            }
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
        public async Task<bool> ChangeUserPasswordAsync(string username, string currentPassword, string newPassword)
        {
            bool result = false;

            try
            {
                var player = await _repository.GetPlayerByUsernameAsync(username);

                if (player != null && player.Account != null)
                {
                    if (BCrypt.Net.BCrypt.Verify(currentPassword, player.Account.PasswordHash))
                    {
                        if (currentPassword != newPassword)
                        {
                            string newHashedPassword = BCrypt.Net.BCrypt.HashPassword(newPassword);
                            player.Account.PasswordHash = newHashedPassword;

                            await _repository.SaveChangesAsync();

                            Log.InfoFormat("Usuario {0} cambió su contraseña exitosamente desde el cliente.", username);

                            _ = Task.Run(() => EmailHelper.SendPasswordChangedNotificationAsync(player.Account.Email, player.Username));

                            result = true;
                        }
                        else
                        {
                            Log.WarnFormat("Usuario {0} intentó usar la misma contraseña nueva que la actual.", username);
                        }
                    }
                    else
                    {
                        Log.WarnFormat("Usuario {0} intentó cambiar contraseña pero falló la actual.", username);
                    }
                }
                else
                {
                    Log.WarnFormat("Intento de cambio de pass para usuario inexistente: {0}", username);
                }
            }
            catch (SqlException ex)
            {
                Log.Fatal("Error SQL crítico al cambiar contraseña.", ex);
            }
            catch (EntityException ex)
            {
                Log.Error("Error de infraestructura de Entity Framework al cambiar contraseña.", ex);
            }
            catch (DbUpdateException ex)
            {
                Log.Error("Error al guardar los cambios de contraseña en la base de datos.", ex);
            }
            catch (DbEntityValidationException ex)
            {
                foreach (var validationErrors in ex.EntityValidationErrors)
                {
                    foreach (var validationError in validationErrors.ValidationErrors)
                    {
                        Log.WarnFormat("Error de validación en entidad: Propiedad: {0} Error: {1}", validationError.PropertyName, validationError.ErrorMessage);
                    }
                }
            }
            catch (TimeoutException ex)
            {
                Log.Error("Tiempo de espera agotado al cambiar contraseña.", ex);
            }
            catch (ArgumentException ex)
            {
                Log.Error("Error de argumento inválido durante el proceso de hashing.", ex);
            }

            return result;
        }
    }
}