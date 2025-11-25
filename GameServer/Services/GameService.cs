using BCrypt.Net;
using GameServer.Contracts;
using GameServer.Helpers;
using GameServer.Managers;
using log4net;
using System;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Validation;
using System.Data.SqlClient;
using System.Linq;
using System.Security.Cryptography;
using System.ServiceModel;
using System.Threading.Tasks;

namespace GameServer
{
    public class GameService : IGameService
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(GameService));
        private const string DefaultAvatar = "default_avatar.png";
        private const int CodeExpirationMinutes = 15;

        public async Task<RegistrationResult> RegisterUserAsync(string username, string email, string password)
        {
            try
            {
                using (var context = new GameDatabase_Container())
                {
                    if (context.Players.Any(p => p.Username == username))
                    {
                        Log.WarnFormat("Registration failed: Username '{0}' already exists.", username);
                        return RegistrationResult.UsernameAlreadyExists;
                    }

                    var existingAccount = context.Accounts.FirstOrDefault(a => a.Email == email);
                    if (existingAccount != null)
                    {
                        if (existingAccount.AccountStatus == (int)AccountStatus.Pending)
                        {
                            string newCode = GenerateSecureCode();
                            existingAccount.VerificationCode = newCode;
                            existingAccount.CodeExpiration = DateTime.Now.AddMinutes(CodeExpirationMinutes);

                            await context.SaveChangesAsync();

                            await EmailHelper.SendVerificationEmailAsync(email, newCode).ConfigureAwait(false);

                            Log.InfoFormat("User {0} tried to register with a pending email. New code sent.", email);
                            return RegistrationResult.EmailPendingVerification;
                        }
                        else
                        {
                            Log.WarnFormat("Registration failed: Email '{0}' is already in use.", email);
                            return RegistrationResult.EmailAlreadyExists;
                        }
                    }

                    string verifyCode = GenerateSecureCode();
                    string hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);

                    var newAccount = new Account
                    {
                        Email = email,
                        PasswordHash = hashedPassword,
                        RegisterDate = DateTime.Now,
                        AccountStatus = (int)AccountStatus.Pending,
                        VerificationCode = verifyCode,
                        CodeExpiration = DateTime.Now.AddMinutes(CodeExpirationMinutes)
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
                        Avatar = DefaultAvatar,
                        Account = newAccount,
                        PlayerStat = newStats
                    };

                    context.Players.Add(newPlayer);
                    await context.SaveChangesAsync();

                    bool emailSent = await EmailHelper.SendVerificationEmailAsync(email, verifyCode)
                                                      .ConfigureAwait(false);

                    if (emailSent)
                    {
                        Log.InfoFormat("Verification email sent to {0}.", email);
                    }

                    return RegistrationResult.Success;
                }
            }
            catch (DbEntityValidationException ex)
            {
                foreach (var validationErrors in ex.EntityValidationErrors)
                {
                    foreach (var validationError in validationErrors.ValidationErrors)
                    {
                        Log.Warn($"Property: {validationError.PropertyName} Error: {validationError.ErrorMessage}");
                    }
                }
                return RegistrationResult.FatalError;
            }
            catch (DbUpdateException ex)
            {
                Log.Error($"DB update error for {username}.", ex);
                return RegistrationResult.FatalError;
            }
            catch (SqlException ex)
            {
                Log.Fatal($"Fatal SQL error in RegisterUser. Check DB connection!", ex);
                return RegistrationResult.FatalError;
            }
            catch (Exception ex)
            {
                Log.Error($"Unexpected error in RegisterUser for {username}.", ex);
                return RegistrationResult.FatalError;
            }
        }

        public bool VerifyAccount(string email, string code)
        {
            bool isSuccess = false;
            try
            {
                using (var context = new GameDatabase_Container())
                {
                    var account = context.Accounts.FirstOrDefault(a => a.Email == email);

                    if (account != null)
                    {
                        if (account.VerificationCode == code)
                        {
                            if (account.CodeExpiration >= DateTime.Now)
                            {
                                account.AccountStatus = (int)AccountStatus.Active;
                                account.VerificationCode = null;
                                account.CodeExpiration = null;

                                context.SaveChanges();

                                Log.InfoFormat("Account for {0} verified successfully.", email);
                                isSuccess = true;
                            }
                            else
                            {
                                Log.WarnFormat("Verification failed for {0} (code expired).", email);
                            }
                        }
                        else
                        {
                            Log.WarnFormat("Verification failed for {0} (wrong code).", email);
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                Log.Fatal($"Fatal SQL error in VerifyAccount.", ex);
            }
            catch (Exception ex)
            {
                Log.Error($"Error in VerifyAccount: {email}", ex);
            }
            return isSuccess;
        }

        public async Task<bool> LogInAsync(string usernameOrEmail, string password)
        {
            bool isSuccess = false;
            try
            {
                if (ConnectionManager.IsUserOnline(usernameOrEmail))
                {
                    Log.Warn($"Login blocked: User {usernameOrEmail} is already connected.");
                    return false;
                }

                using (var context = new GameDatabase_Container())
                {
                    var player = await context.Players
                        .Include(p => p.Account)
                        .FirstOrDefaultAsync(p => p.Username == usernameOrEmail || p.Account.Email == usernameOrEmail);

                    if (player != null)
                    {
                        if (ConnectionManager.IsUserOnline(player.Username))
                        {
                            Log.Warn($"Login blocked: User {player.Username} is already connected (resolved by email).");
                            return false;
                        }

                        if (BCrypt.Net.BCrypt.Verify(password, player.Account.PasswordHash))
                        {
                            if (player.Account.AccountStatus == (int)AccountStatus.Active)
                            {
                                Log.InfoFormat("Login successful for {0}", player.Username);
                                ConnectionManager.AddUser(player.Username);
                                isSuccess = true;
                            }
                            else
                            {
                                Log.WarnFormat("Login attempt with inactive account: {0}", player.Username);
                            }
                        }
                        else
                        {
                            Log.WarnFormat("Login failed: incorrect credentials for {0}", usernameOrEmail);
                        }
                    }
                    else
                    {
                        Log.WarnFormat("Login failed: User not found {0}", usernameOrEmail);
                    }
                }
            }
            catch (SqlException ex)
            {
                Log.Fatal($"Fatal SQL error in LogIn.", ex);
            }
            catch (Exception ex)
            {
                Log.Error($"Unexpected error in LogIn for {usernameOrEmail}.", ex);
            }
            return isSuccess;
        }

        public void Logout(string username)
        {
            try
            {
                ConnectionManager.RemoveUser(username);
                Log.Info($"User {username} logged out successfully.");
            }
            catch (Exception ex)
            {
                Log.Error($"Error during logout for {username}", ex);
            }
        }

        public async Task<bool> RequestPasswordResetAsync(string email)
        {
            bool isSuccess = false;
            try
            {
                using (var context = new GameDatabase_Container())
                {
                    var account = await context.Accounts.FirstOrDefaultAsync(a => a.Email == email);

                    if (account == null)
                    {
                        Log.WarnFormat("Password reset requested for non-existent email: {0}", email);
                        isSuccess = true;
                    }
                    else
                    {
                        string verifyCode = GenerateSecureCode();
                        account.VerificationCode = verifyCode;
                        account.CodeExpiration = DateTime.Now.AddMinutes(CodeExpirationMinutes);

                        await context.SaveChangesAsync();

                        bool emailSent = await EmailHelper.SendRecoveryEmailAsync(email, verifyCode)
                                                          .ConfigureAwait(false);

                        Log.InfoFormat("Reset email sent to: {0}", email);
                        isSuccess = emailSent;
                    }
                }
            }
            catch (SqlException ex)
            {
                Log.Fatal($"Fatal SQL error in RequestPasswordReset.", ex);
            }
            catch (Exception ex)
            {
                Log.Error($"Error in RequestPasswordReset for {email}", ex);
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
                        a.VerificationCode != null &&
                        a.CodeExpiration >= DateTime.Now);

                    if (isValid)
                    {
                        Log.InfoFormat("Recovery code verified for: {0}", email);
                    }
                    else
                    {
                        Log.WarnFormat("Failed recovery code attempt for: {0}", email);
                    }
                }
            }
            catch (SqlException ex)
            {
                Log.Fatal($"Fatal SQL error in VerifyRecoveryCode.", ex);
            }
            catch (Exception ex)
            {
                Log.Error($"Error in VerifyRecoveryCode for {email}", ex);
            }
            return isValid;
        }

        public async Task<bool> ResendVerificationCodeAsync(string email)
        {
            bool isSuccess = false;
            try
            {
                using (var context = new GameDatabase_Container())
                {
                    var account = context.Accounts.FirstOrDefault(a => a.Email == email);

                    if (account != null && account.AccountStatus == (int)AccountStatus.Pending)
                    {
                        string newCode = GenerateSecureCode();

                        account.VerificationCode = newCode;
                        account.CodeExpiration = DateTime.Now.AddMinutes(CodeExpirationMinutes);

                        await context.SaveChangesAsync();

                        bool emailSent = await EmailHelper.SendVerificationEmailAsync(email, newCode)
                                                          .ConfigureAwait(false);

                        if (emailSent)
                        {
                            Log.InfoFormat("Verification code resent to {0}.", email);
                            isSuccess = true;
                        }
                    }
                    else
                    {
                        Log.WarnFormat("Resend code requested for invalid or active account: {0}", email);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error in ResendVerificationCode for {email}", ex);
            }
            return isSuccess;
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
                        Log.ErrorFormat("Attempt to update password for non-existent account: {0}", email);
                    }
                    else if (BCrypt.Net.BCrypt.Verify(newPassword, account.PasswordHash))
                    {
                        Log.WarnFormat("User '{0}' tried to reuse old password.", email);
                    }
                    else
                    {
                        string newHashedPassword = BCrypt.Net.BCrypt.HashPassword(newPassword);
                        account.PasswordHash = newHashedPassword;
                        account.VerificationCode = null;
                        account.CodeExpiration = null;

                        context.SaveChanges();

                        Log.InfoFormat("Password reset successfully for: {0}", email);
                        isSuccess = true;
                    }
                }
            }
            catch (SqlException ex) 
            {
                Log.Fatal($"Fatal SQL error in UpdatePassword.", ex);
            }
            catch (Exception ex)
            {
                Log.Error($"Error in UpdatePassword for {email}", ex);
            }
            return isSuccess;
        }

        private string GenerateSecureCode()
        {
            using (var rng = new RNGCryptoServiceProvider())
            {
                byte[] data = new byte[4];
                rng.GetBytes(data);
                int value = BitConverter.ToInt32(data, 0);
                return Math.Abs(value % 1000000).ToString("D6");
            }
        }
    }
}