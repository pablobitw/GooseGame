using BCrypt.Net;
using GameServer.Helpers;
using System;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;
using log4net;

namespace GameServer
{
    public class GameService : IGameService
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(GameService));

        public async Task<bool> RegisterUser(string username, string email, string password)
        {
            bool isSuccess = false;
            try
            {
                using (var context = new GameDatabase_Container())
                {
                    if (context.Players.Any(p => p.Username == username) || context.Accounts.Any(a => a.Email == email))
                    {
                        Log.Warn($"Registro fallido: Usuario o email ya existen para '{username}'");
                    }
                    else
                    {
                        string verifyCode = new Random().Next(100000, 999999).ToString();
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
                        context.SaveChanges();

                        bool emailSent = await EmailHelper.EnviarCorreoDeVerificacion(email, verifyCode)
                                                          .ConfigureAwait(false);

                        if (emailSent) Log.Info($"Correo de verificación enviado a {email}.");
                        isSuccess = emailSent;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error en RegistrarUsuario: {username}", ex);
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

                        Log.Info($"Cuenta para {email} verificada exitosamente");
                        isSuccess = true;
                    }
                    else
                    {
                        Log.Warn($"Verificación fallida para {email} (código incorrecto).");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error en VerificarCuenta: {email}", ex);
            }
            return isSuccess;
        }

        public bool LogIn(string username, string password)
        {
            bool isSuccess = false;
            try
            {
                using (var context = new GameDatabase_Container())
                {
                    var player = context.Players.FirstOrDefault(p => p.Username == username);

                    if (player == null)
                    {
                        Log.Warn($"Login fallido: Usuario '{username}' no encontrado.");
                    }
                    else
                    {
                        if (BCrypt.Net.BCrypt.Verify(password, player.Account.PasswordHash))
                        {
                            if (player.Account.AccountStatus == (int)AccountStatus.Active)
                            {
                                Log.Info($"Usuario '{username}' inició sesión exitosamente.");
                                isSuccess = true;
                            }
                            else
                            {
                                Log.Warn($"Login fallido: Cuenta de '{username}' no está activa.");
                            }
                        }
                        else
                        {
                            Log.Warn($"Login fallido: Contraseña incorrecta para '{username}'.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error en IniciarSesion para '{username}'", ex);
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
                        Log.Warn($"Password reset solicitado para email no existente: {email}");
                        isSuccess = true;
                    }
                    else
                    {
                        string verifyCode = new Random().Next(100000, 999999).ToString();
                        account.VerificationCode = verifyCode;
                        context.SaveChanges();

                        bool emailSent = await EmailHelper.EnviarCorreoDeRecuperacion(email, verifyCode)
                                                          .ConfigureAwait(false);

                        Log.Info($"Correo de reseteo enviado a: {email}");
                        isSuccess = emailSent;
                    }
                }
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

                    if (isValid) Log.Info($"Código de recuperación verificado para: {email}");
                    else Log.Warn($"Intento fallido de código de recuperación para: {email}");
                }
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
                        Log.Error($"Intento de actualizar contraseña para cuenta no existente: {email}");
                    }
                    else if (BCrypt.Net.BCrypt.Verify(newPassword, account.PasswordHash))
                    {
                        Log.Warn($"Usuario '{email}' intentó reusar su contraseña anterior.");
                    }
                    else
                    {
                        string newHashedPassword = BCrypt.Net.BCrypt.HashPassword(newPassword);
                        account.PasswordHash = newHashedPassword;
                        account.VerificationCode = null;
                        context.SaveChanges();
                        Log.Info($"Contraseña reseteada exitosamente para: {email}");
                        isSuccess = true;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error en UpdatePassword for {email}", ex);
            }
            return isSuccess;
        }

        public bool CambiarContrasena(string username, string oldPassword, string newPassword)
        {
            throw new NotImplementedException();
        }
    }
}