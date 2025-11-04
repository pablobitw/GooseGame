using BCrypt.Net;
using GameServer.Helpers;
using System;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;
using log4net;
using System.Data.Entity.Validation;
using System.Data.Entity.Infrastructure;
using System.Data.SqlClient;

namespace GameServer
{
    public class GameService : IGameService
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(GameService));

        private static readonly Random RandomGenerator = new Random();

        public async Task<bool> RegisterUser(string username, string email, string password)
        {
            bool isSuccess = false;
            try
            {
                using (var context = new GameDatabase_Container())
                {
                    if (context.Players.Any(p => p.Username == username) || context.Accounts.Any(a => a.Email == email))
                    {
                        Log.WarnFormat("Registro fallido: Usuario o email ya existen para '{0}'", username);
                    }
                    else
                    {
                        string verifyCode = RandomGenerator.Next(100000, 999999).ToString();
                        string hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);

                        var newAccount = new Account
                        {
                            Email = email,
                            PasswordHash = hashedPassword,
                            RegisterDate = DateTime.Now,
                            AccountStatus = (int)AccountStatus.Pending,
                            VerificationCode = verifyCode
                        };

                        var newStats = new PlayerStat
                        {
                            MatchesPlayed = 0,
                            MatchesWon = 0,
                            MatchesLost = 0,
                            LuckyBoxOpened = 0
                        };

                        var newPlayer = new Player
                        {
                            Username = username,
                            Coins = 0,
                            Avatar = "default_avatar.png",
                            Account = newAccount,
                            PlayerStat = newStats
                        };

                        context.Players.Add(newPlayer);
                        await context.SaveChangesAsync();


                        bool emailSent = await EmailHelper.SendVerificationEmailAsync(email, verifyCode)
                                                          .ConfigureAwait(false);

                        if (emailSent) Log.InfoFormat("Correo de verificación enviado a {0}.", email);
                        isSuccess = emailSent;
                    }
                }
            }
            catch (DbEntityValidationException ex)
            {
                Log.Warn($"Error de validación de entidad para {username}.", ex);
            }
            catch (DbUpdateException ex)
            {
                Log.Error($"Error de actualización de BD para {username}.", ex);
            }
            catch (SqlException ex)
            {
                Log.Fatal($"Error fatal de SQL en RegisterUser. ¡Revisar conexión a BD!", ex);
            }
            catch (Exception ex)
            {
                Log.Error($"Error inesperado en RegisterUser para {username}.", ex);
            }
            return isSuccess;
        }

        public bool VerifyAccount(string email, string code)
        {
            bool isSuccess = false;
            try
            {
                using (var context = new GameDatabase_Container())
                {
                    var account = context.Accounts.FirstOrDefault(a => a.Email == email);

                    if (account != null && account.VerificationCode == code)
                    {
                        account.AccountStatus = (int)AccountStatus.Active;
                        account.VerificationCode = null;

                        
                        context.SaveChanges();

                        Log.InfoFormat("Cuenta para {0} verificada exitosamente", email);
                        isSuccess = true;
                    }
                    else
                    {
                        Log.WarnFormat("Verificación fallida para {0} (código incorrecto).", email);
                    }
                }
            }
            catch (DbEntityValidationException ex)
            {
                Log.Warn($"Error de validación de entidad al verificar {email}.", ex);
            }
            catch (DbUpdateException ex)
            {
                Log.Error($"Error de actualización de BD al verificar {email}.", ex);
            }
            catch (SqlException ex)
            {
                Log.Fatal($"Error fatal de SQL en VerifyAccount. ¡Revisar conexión a BD!", ex);
            }
            catch (Exception ex)
            {
                Log.Error($"Error en VerificarCuenta: {email}", ex);
            }
            return isSuccess;
        }

        public async Task<bool> LogIn(string username, string password)
        {
            bool isSuccess = false;
            try
            {
                using (var context = new GameDatabase_Container())
                {
                    var player = context.Players
                        .FirstOrDefault(p => p.Username == username);

                    if (player != null &&
                        BCrypt.Net.BCrypt.Verify(password, player.Account.PasswordHash))
                    {
                        if (player.Account.AccountStatus == (int)AccountStatus.Active)
                        {
                            Log.InfoFormat("Inicio de sesión exitoso para {0}", username);
                            isSuccess = true;
                        }
                        else
                        {
                            Log.WarnFormat("Intento de inicio con cuenta inactiva: {0}", username);
                        }
                    }
                    else
                    {
                        Log.WarnFormat("Inicio fallido: credenciales incorrectas para {0}", username);
                    }
                }
            }
            catch (SqlException ex)
            {
                Log.Fatal($"Error fatal de SQL en LogIn. ¡Revisar conexión a BD!", ex);
            }
            catch (Exception ex)
            {
                Log.Error($"Error inesperado en LogIn para {username}.", ex);
            }
            return isSuccess;
        }


        public async Task<bool> RequestPasswordReset(string email)
        {
            bool isSuccess = false;
            try
            {
                using (var context = new GameDatabase_Container())
                {
                    var account = context.Accounts.FirstOrDefault(a => a.Email == email);

                    if (account == null)
                    {
                        Log.WarnFormat("Password reset solicitado para email no existente: {0}", email);
                        isSuccess = true;
                    }
                    else
                    {
                        string verifyCode = RandomGenerator.Next(100000, 999999).ToString();
                        account.VerificationCode = verifyCode;

                        await context.SaveChangesAsync();

                        bool emailSent = await EmailHelper.SendRecoveryEmailAsync(email, verifyCode)
                                                          .ConfigureAwait(false);

                        Log.InfoFormat("Correo de reseteo enviado a: {0}", email);
                        isSuccess = emailSent;
                    }
                }
            }
            catch (DbUpdateException ex)
            {
                Log.Error($"Error de actualización de BD en RequestPasswordReset para {email}.", ex);
            }
            catch (SqlException ex)
            {
                Log.Fatal($"Error fatal de SQL en RequestPasswordReset. ¡Revisar conexión a BD!", ex);
            }
            catch (Exception ex)
            {
                Log.Error($"Error en RequestPasswordReset for {email}", ex);
            }
            return isSuccess;
        }

        public bool VerifyRecoveryCode(string email, string code)
        {
            bool isValid = false;
            try
            {
                using (var context = new GameDatabase_Container())
                {
                    isValid = context.Accounts.Any(a =>
                        a.Email == email &&
                        a.VerificationCode == code &&
                        a.VerificationCode != null);

                    if (isValid) Log.InfoFormat("Código de recuperación verificado para: {0}", email);
                    else Log.WarnFormat("Intento fallido de código de recuperación para: {0}", email);
                }
            }
            catch (SqlException ex)
            {
                Log.Fatal($"Error fatal de SQL en VerifyRecoveryCode. ¡Revisar conexión a BD!", ex);
            }
            catch (Exception ex)
            {
                Log.Error($"Error en VerifyRecoveryCode for {email}", ex);
            }
            return isValid;
        }

        public bool UpdatePassword(string email, string newPassword)
        {
            bool isSuccess = false;
            try
            {
                using (var context = new GameDatabase_Container())
                {
                    var account = context.Accounts.FirstOrDefault(a => a.Email == email);
                    if (account == null)
                    {
                        Log.ErrorFormat("Intento de actualizar contraseña para cuenta no existente: {0}", email);
                    }
                    else if (BCrypt.Net.BCrypt.Verify(newPassword, account.PasswordHash))
                    {
                        Log.WarnFormat("Usuario '{0}' intentó reusar su contraseña anterior.", email);
                    }
                    else
                    {
                        string newHashedPassword = BCrypt.Net.BCrypt.HashPassword(newPassword);
                        account.PasswordHash = newHashedPassword;
                        account.VerificationCode = null;

                        context.SaveChanges();

                        Log.InfoFormat("Contraseña reseteada exitosamente para: {0}", email);
                        isSuccess = true;
                    }
                }
            }
            catch (DbUpdateException ex)
            {
                Log.Error($"Error de actualización de BD en UpdatePassword para {email}.", ex);
            }
            catch (SqlException ex)
            {
                Log.Fatal($"Error fatal de SQL en UpdatePassword. ¡Revisar conexión a BD!", ex);
            }
            catch (Exception ex)
            {
                Log.Error($"Error en UpdatePassword for {email}", ex);
            }
            return isSuccess;
        }

    }
}