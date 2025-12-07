using GameServer.DTOs.Auth;
using GameServer.Helpers;
using GameServer.Managers;
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
        private const string DefaultAvatar = "default_avatar.png";
        private const int CodeExpirationMinutes = 15;
        private readonly AuthRepository _repository;

        public AuthAppService(AuthRepository repository)
        {
            _repository = repository;
        }

        public async Task<RegistrationResult> RegisterUserAsync(RegisterUserRequest request)
        {
            RegistrationResult result = RegistrationResult.FatalError;

            try
            {
                if (request != null)
                {
                    if (_repository.IsUsernameTaken(request.Username))
                    {
                        Log.WarnFormat("Registro fallido: Usuario '{0}' ya existe.", request.Username);
                        result = RegistrationResult.UsernameAlreadyExists;
                    }
                    else
                    {
                        var existingAccount = await _repository.GetAccountByEmailAsync(request.Email);
                        if (existingAccount != null)
                        {
                            if (existingAccount.AccountStatus == (int)AccountStatus.Pending)
                            {
                                string newCode = GenerateSecureCode();
                                existingAccount.VerificationCode = newCode;
                                existingAccount.CodeExpiration = DateTime.Now.AddMinutes(CodeExpirationMinutes);

                                await _repository.SaveChangesAsync();
                                await EmailHelper.SendVerificationEmailAsync(request.Email, newCode).ConfigureAwait(false);

                                result = RegistrationResult.EmailPendingVerification;
                            }
                            else
                            {
                                Log.WarnFormat("Registro fallido: Email '{0}' en uso.", request.Email);
                                result = RegistrationResult.EmailAlreadyExists;
                            }
                        }
                        else
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
                                PlayerStat = new PlayerStat()
                            };

                            _repository.AddPlayer(newPlayer);
                            await _repository.SaveChangesAsync();

                            await EmailHelper.SendVerificationEmailAsync(request.Email, verifyCode).ConfigureAwait(false);

                            result = RegistrationResult.Success;
                        }
                    }
                }
            }
            catch (DbEntityValidationException ex)
            {
                foreach (var err in ex.EntityValidationErrors)
                {
                    Log.Warn($"Error validación entidad: {err.Entry.Entity.GetType().Name}");
                }
                result = RegistrationResult.FatalError;
            }
            catch (DbUpdateException ex)
            {
                Log.Error($"Error actualizando base de datos para {request.Username}.", ex);
                result = RegistrationResult.FatalError;
            }
            catch (SqlException ex)
            {
                Log.Fatal($"Error SQL crítico en registro.", ex);
                result = RegistrationResult.FatalError;
            }
            catch (EntityCommandExecutionException ex)
            {
                Log.Error("Error ejecutando comando de entidad en registro.", ex);
                result = RegistrationResult.FatalError;
            }
            catch (TimeoutException ex)
            {
                Log.Error("Tiempo de espera agotado en registro.", ex);
                result = RegistrationResult.FatalError;
            }

            return result;
        }

        public async Task<bool> LogInAsync(string usernameOrEmail, string password)
        {
            bool isSuccess = false;
            try
            {
                if (!ConnectionManager.IsUserOnline(usernameOrEmail))
                {
                    var player = await _repository.GetPlayerForLoginAsync(usernameOrEmail);

                    if (player != null)
                    {
                        if (BCrypt.Net.BCrypt.Verify(password, player.Account.PasswordHash))
                        {
                            if (player.Account.AccountStatus == (int)AccountStatus.Active)
                            {
                                if (player.GameIdGame != null)
                                {
                                    player.GameIdGame = null;
                                    await _repository.SaveChangesAsync();
                                }
                                ConnectionManager.AddUser(player.Username);
                                isSuccess = true;
                            }
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                Log.Fatal("Error SQL en Login.", ex);
                isSuccess = false;
            }
            catch (EntityException ex)
            {
                Log.Error("Error de Entity Framework en Login.", ex);
                isSuccess = false;
            }
            catch (TimeoutException ex)
            {
                Log.Error("Timeout en Login.", ex);
                isSuccess = false;
            }

            return isSuccess;
        }

        public void Logout(string username)
        {
            ConnectionManager.RemoveUser(username);
            Log.Info($"Usuario {username} cerró sesión.");
        }

        public bool VerifyAccount(string email, string code)
        {
            bool isVerified = false;
            try
            {
                var account = _repository.GetAccountByEmail(email);
                if (account != null)
                {
                    if (account.VerificationCode == code)
                    {
                        if (account.CodeExpiration >= DateTime.Now)
                        {
                            account.AccountStatus = (int)AccountStatus.Active;
                            account.VerificationCode = null;
                            account.CodeExpiration = null;

                            _repository.SaveChanges();
                            Log.InfoFormat("Cuenta verificada: {0}", email);
                            isVerified = true;
                        }
                        else
                        {
                            Log.WarnFormat("Código expirado para: {0}", email);
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                Log.Fatal("Error SQL en Verificación.", ex);
                isVerified = false;
            }
            catch (EntityException ex)
            {
                Log.Error("Error Entity Framework en Verificación.", ex);
                isVerified = false;
            }
            catch (TimeoutException ex)
            {
                Log.Error("Timeout en Verificación.", ex);
                isVerified = false;
            }

            return isVerified;
        }

        public async Task<bool> RequestPasswordResetAsync(string email)
        {
            bool isEmailSent = false;
            try
            {
                var account = await _repository.GetAccountByEmailAsync(email);
                if (account == null)
                {
                    isEmailSent = true;
                }
                else
                {
                    string verifyCode = GenerateSecureCode();
                    account.VerificationCode = verifyCode;
                    account.CodeExpiration = DateTime.Now.AddMinutes(CodeExpirationMinutes);

                    await _repository.SaveChangesAsync();

                    isEmailSent = await EmailHelper.SendRecoveryEmailAsync(email, verifyCode).ConfigureAwait(false);
                }
            }
            catch (SqlException ex)
            {
                Log.Fatal("Error SQL en Reset Password.", ex);
                isEmailSent = false;
            }
            catch (EntityException ex)
            {
                Log.Error("Error Entity Framework en Reset Password.", ex);
                isEmailSent = false;
            }
            catch (TimeoutException ex)
            {
                Log.Error("Timeout en Reset Password.", ex);
                isEmailSent = false;
            }

            return isEmailSent;
        }

        public bool VerifyRecoveryCode(string email, string code)
        {
            bool isValid = false;
            try
            {
                isValid = _repository.VerifyRecoveryCode(email, code);
                if (isValid) Log.InfoFormat("Recovery code verified for: {0}", email);
                else Log.WarnFormat("Failed recovery code attempt for: {0}", email);
            }
            catch (SqlException ex)
            {
                Log.Fatal("Error SQL en Verificar Código Recuperación.", ex);
                isValid = false;
            }
            catch (EntityException ex)
            {
                Log.Error("Error EF en Verificar Código Recuperación.", ex);
                isValid = false;
            }
            catch (TimeoutException ex)
            {
                Log.Error("Timeout en Verificar Código Recuperación.", ex);
                isValid = false;
            }

            return isValid;
        }

        public bool UpdatePassword(string email, string newPassword)
        {
            bool isUpdated = false;
            try
            {
                var account = _repository.GetAccountByEmail(email);
                if (account != null)
                {
                    if (!BCrypt.Net.BCrypt.Verify(newPassword, account.PasswordHash))
                    {
                        string newHashedPassword = BCrypt.Net.BCrypt.HashPassword(newPassword);
                        account.PasswordHash = newHashedPassword;
                        account.VerificationCode = null;
                        account.CodeExpiration = null;

                        _repository.SaveChanges();
                        Log.InfoFormat("Contraseña actualizada para: {0}", email);
                        isUpdated = true;
                    }
                    else
                    {
                        Log.WarnFormat("Intento de reusar contraseña para: {0}", email);
                    }
                }
                else
                {
                    Log.ErrorFormat("Attempt to update password for non-existent account: {0}", email);
                }
            }
            catch (SqlException ex)
            {
                Log.Fatal("Error SQL en Actualizar Password.", ex);
                isUpdated = false;
            }
            catch (EntityException ex)
            {
                Log.Error("Error EF en Actualizar Password.", ex);
                isUpdated = false;
            }
            catch (TimeoutException ex)
            {
                Log.Error("Timeout en Actualizar Password.", ex);
                isUpdated = false;
            }

            return isUpdated;
        }

        public async Task<bool> ResendVerificationCodeAsync(string email)
        {
            bool isSuccess = false;
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
                        isSuccess = true;
                    }
                }
                else
                {
                    Log.WarnFormat("Resend code requested for invalid account: {0}", email);
                }
            }
            catch (SqlException ex)
            {
                Log.Fatal("Error SQL en Reenvío Código.", ex);
                isSuccess = false;
            }
            catch (EntityException ex)
            {
                Log.Error("Error EF en Reenvío Código.", ex);
                isSuccess = false;
            }
            catch (TimeoutException ex)
            {
                Log.Error("Timeout en Reenvío Código.", ex);
                isSuccess = false;
            }

            return isSuccess;
        }

        private string GenerateSecureCode()
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