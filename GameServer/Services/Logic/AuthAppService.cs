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
            try
            {
                if (!NetworkInterface.GetIsNetworkAvailable())
                {
                    return false;
                }

                using (var ping = new Ping())
                {
                    var buffer = new byte[32];
                    var timeout = 2000; 
                    var options = new PingOptions();

                    var reply = ping.Send("8.8.8.8", timeout, buffer, options);
                    return reply != null && reply.Status == IPStatus.Success;
                }
            }
            catch (Exception ex)
            {
                Log.Warn("Falló la verificación de internet (Ping): " + ex.Message);
                return false;
            }
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

            if (!CheckInternetConnection())
            {
                Log.WarnFormat("Intento de registro fallido para usuario '{0}': No hay conexión a internet en el servidor.", request.Username);
                return RegistrationResult.FatalError;
            }

            if (string.IsNullOrWhiteSpace(request.Username) ||
                string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.Password))
            {
                return RegistrationResult.FatalError;
            }

            if (!IsValidEmail(request.Email))
            {
                return RegistrationResult.FatalError;
            }

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

        private static bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
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
            if (!CheckInternetConnection()) return RegistrationResult.FatalError;

            string newCode = GenerateSecureCode();
            player.Account.VerificationCode = newCode;
            player.Account.CodeExpiration = DateTime.Now.AddMinutes(CodeExpirationMinutes);

            await _repository.SaveChangesAsync();
            await EmailHelper.SendVerificationEmailAsync(player.Account.Email, newCode, player.Account.PreferredLanguage).ConfigureAwait(false);

            return RegistrationResult.EmailPendingVerification;
        }

        private async Task<RegistrationResult> ResendVerificationForAccount(Account account)
        {
            if (!CheckInternetConnection()) return RegistrationResult.FatalError;

            string newCode = GenerateSecureCode();
            account.VerificationCode = newCode;
            account.CodeExpiration = DateTime.Now.AddMinutes(CodeExpirationMinutes);

            await _repository.SaveChangesAsync();
            await EmailHelper.SendVerificationEmailAsync(account.Email, newCode, account.PreferredLanguage).ConfigureAwait(false);

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

                await EmailHelper.SendVerificationEmailAsync(request.Email, verifyCode, newAccount.PreferredLanguage).ConfigureAwait(false);

                return RegistrationResult.Success;
            }
            catch (DbUpdateException ex)
            {
                if (ex.InnerException?.InnerException is SqlException sqlEx && (sqlEx.Number == 2601 || sqlEx.Number == 2627))
                {
                    if (_repository.IsUsernameTaken(request.Username))
                    {
                        return RegistrationResult.UsernameAlreadyExists;
                    }
                    return RegistrationResult.EmailAlreadyExists;
                }
                Log.Error("Error al guardar usuario en base de datos.", ex);
                return RegistrationResult.FatalError;
            }
            catch (Exception ex)
            {
                Log.Error("Error general al crear usuario o enviar correo.", ex);
                return RegistrationResult.Success;
            }
        }

        public async Task<LoginResponseDto> LogInAsync(string usernameOrEmail, string password)
        {
            var response = new LoginResponseDto
            {
                IsSuccess = false,
                Message = "Credenciales inválidas o error de servidor.",
                PreferredLanguage = "es-MX"
            };

            try
            {
                var player = await _repository.GetPlayerForLoginAsync(usernameOrEmail);

                if (player == null)
                {
                    return response;
                }

                if (player.IsBanned)
                {
                    response.Message = "Tu cuenta ha sido baneada permanentemente por acumulación de faltas.";
                    return response;
                }

                if (await _repository.IsAccountSanctionedAsync(player.Account.IdAccount))
                {
                    response.Message = "Tu cuenta tiene una sanción temporal activa.";
                    return response;
                }

                if (!IsAccountActive(player))
                {
                    Log.WarnFormat("Intento de login en cuenta no activa (Estado: {0}): {1}", player.Account.AccountStatus, player.Username);
                    response.Message = "Cuenta inactiva o suspendida administrativamente.";
                    return response;
                }

                if (ConnectionManager.IsUserOnline(player.Username))
                {
                    Log.WarnFormat("Intento de doble login rechazado para: {0}", player.Username);
                    response.Message = "El usuario ya está conectado.";
                    return response;
                }

                return await ProcessSuccessfulLogin(player, password, response);
            }
            catch (SqlException ex)
            {
                Log.Fatal("Error SQL en Login.", ex);
                response.Message = "Error de base de datos.";
            }
            catch (EntityException ex)
            {
                Log.Error("Error de Entity Framework en Login.", ex);
                response.Message = "El servidor esta apagado, intentalo mas tarde.";
            }
            catch (DbUpdateException ex)
            {
                Log.Error("Error al actualizar datos en Login.", ex);
                response.Message = "Error al procesar la solicitud.";
            }
            catch (TimeoutException ex)
            {
                Log.Error("Timeout en Login.", ex);
                response.Message = "El servidor tardó demasiado en responder.";
            }

            return response;
        }

        private static bool IsAccountActive(Player player)
        {
            return player.Account.AccountStatus != (int)AccountStatus.Inactive &&
                   player.Account.AccountStatus != (int)AccountStatus.Banned;
        }

        private async Task<LoginResponseDto> ProcessSuccessfulLogin(Player player, string password, LoginResponseDto response)
        {
            if (!ValidateLoginCredentials(player, password))
            {
                return response;
            }

            if (player.GameIdGame != null)
            {
                player.GameIdGame = null;
                await _repository.SaveChangesAsync();
            }

            ConnectionManager.AddUser(player.Username);

            _ = Task.Run(() => EmailHelper.SendLoginNotificationAsync(player.Account.Email, player.Username, player.Account.PreferredLanguage));

            response.IsSuccess = true;
            response.Message = "Login exitoso.";
            response.PreferredLanguage = player.Account.PreferredLanguage ?? "es-MX";

            return response;
        }

        private static bool ValidateLoginCredentials(Player player, string password)
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
            if (!CheckInternetConnection()) return false; 

            try
            {
                var account = await _repository.GetAccountByEmailAsync(email);
                if (account == null) return true;

                string verifyCode = GenerateSecureCode();
                account.VerificationCode = verifyCode;
                account.CodeExpiration = DateTime.Now.AddMinutes(CodeExpirationMinutes);

                await _repository.SaveChangesAsync();
                return await EmailHelper.SendRecoveryEmailAsync(email, verifyCode, account.PreferredLanguage).ConfigureAwait(false);
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
                var player = _repository.GetPlayerForLoginAsync(email).Result;

                if (player == null || player.Account == null)
                {
                    Log.ErrorFormat("Attempt to update password for non-existent user/account: {0}", email);
                    return false;
                }

                var account = player.Account;

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

                _ = Task.Run(() => EmailHelper.SendPasswordChangedNotificationAsync(email, player.Username, account.PreferredLanguage));

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
            catch (Exception ex)
            {
                Log.Error("Error general en Actualizar Password.", ex);
                return false;
            }
        }

        public async Task<bool> ResendVerificationCodeAsync(string email)
        {
            if (!CheckInternetConnection()) return false; 

            try
            {
                var account = await _repository.GetAccountByEmailAsync(email);
                if (account != null && account.AccountStatus == (int)AccountStatus.Pending)
                {
                    string newCode = GenerateSecureCode();
                    account.VerificationCode = newCode;
                    account.CodeExpiration = DateTime.Now.AddMinutes(CodeExpirationMinutes);

                    await _repository.SaveChangesAsync();
                    bool emailSent = await EmailHelper.SendVerificationEmailAsync(email, newCode, account.PreferredLanguage).ConfigureAwait(false);
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

        public async Task<bool> ChangeUserPasswordAsync(string username, string currentPassword, string newPassword)
        {
            bool result = false;
            try
            {
                var player = await _repository.GetPlayerByUsernameAsync(username);

                if (player == null || player.Account == null)
                {
                    Log.WarnFormat("Intento de cambio de pass para usuario inexistente: {0}", username);
                    return false;
                }

                if (!BCrypt.Net.BCrypt.Verify(currentPassword, player.Account.PasswordHash))
                {
                    Log.WarnFormat("Usuario {0} intentó cambiar contraseña pero falló la actual.", username);
                    return false;
                }

                if (currentPassword == newPassword)
                {
                    Log.WarnFormat("Usuario {0} intentó usar la misma contraseña nueva que la actual.", username);
                    return false;
                }

                result = await UpdatePlayerPassword(player, newPassword, username);
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
                LogValidationErrors(ex);
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

        private async Task<bool> UpdatePlayerPassword(Player player, string newPassword, string username)
        {
            string newHashedPassword = BCrypt.Net.BCrypt.HashPassword(newPassword);
            player.Account.PasswordHash = newHashedPassword;

            await _repository.SaveChangesAsync();

            Log.InfoFormat("Usuario {0} cambió su contraseña exitosamente desde el cliente.", username);

            _ = Task.Run(() => EmailHelper.SendPasswordChangedNotificationAsync(player.Account.Email, player.Username, player.Account.PreferredLanguage));

            return true;
        }

        private static void LogValidationErrors(DbEntityValidationException ex)
        {
            foreach (var validationErrors in ex.EntityValidationErrors)
            {
                foreach (var validationError in validationErrors.ValidationErrors)
                {
                    Log.WarnFormat("Error de validación en entidad: Propiedad: {0} Error: {1}", validationError.PropertyName, validationError.ErrorMessage);
                }
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
    }
}