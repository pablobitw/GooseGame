using BCrypt.Net;
using GameServer.Contracts;
using GameServer.Helpers;
using log4net;
using System;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Validation;
using System.Data.SqlClient;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;

namespace GameServer
{
    public class GameService : IGameService
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(GameService));
        private static readonly Random RandomGenerator = new Random();
        private const string DefaultAvatar = "default_avatar.png";

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
                            Log.InfoFormat("User {0} tried to register with a pending email. Redirecting to verification.", email);
                            return RegistrationResult.EmailPendingVerification;
                        }
                        else
                        {
                            Log.WarnFormat("Registration failed: Email '{0}' is already in use.", email);
                            return RegistrationResult.EmailAlreadyExists;
                        }
                    }

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
                Log.Warn($"Entity validation error for {username}.", ex);
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

                    if (account != null && account.VerificationCode == code)
                    {
                        account.AccountStatus = (int)AccountStatus.Active;
                        account.VerificationCode = null;

                        context.SaveChanges();

                        Log.InfoFormat("Account for {0} verified successfully.", email);
                        isSuccess = true;
                    }
                    else
                    {
                        Log.WarnFormat("Verification failed for {0} (wrong code).", email);
                    }
                }
            }
            catch (DbEntityValidationException ex)
            {
                Log.Warn($"Entity validation error verifying {email}.", ex);
            }
            catch (DbUpdateException ex)
            {
                Log.Error($"DB update error verifying {email}.", ex);
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
                using (var context = new GameDatabase_Container())
                {
                    var player = await context.Players
                        .Include(p => p.Account)
                        .FirstOrDefaultAsync(p => p.Username == usernameOrEmail || p.Account.Email == usernameOrEmail);

                    if (player != null &&
                        BCrypt.Net.BCrypt.Verify(password, player.Account.PasswordHash))
                    {
                        if (player.Account.AccountStatus == (int)AccountStatus.Active)
                        {
                            Log.InfoFormat("Login successful for {0}", player.Username);
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
                        string verifyCode = RandomGenerator.Next(100000, 999999).ToString();
                        account.VerificationCode = verifyCode;

                        await context.SaveChangesAsync();

                        bool emailSent = await EmailHelper.SendRecoveryEmailAsync(email, verifyCode)
                                                          .ConfigureAwait(false);

                        Log.InfoFormat("Reset email sent to: {0}", email);
                        isSuccess = emailSent;
                    }
                }
            }
            catch (DbUpdateException ex)
            {
                Log.Error($"DB update error in RequestPasswordReset for {email}.", ex);
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
                        a.VerificationCode != null);

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

                        context.SaveChanges();

                        Log.InfoFormat("Password reset successfully for: {0}", email);
                        isSuccess = true;
                    }
                }
            }
            catch (DbUpdateException ex)
            {
                Log.Error($"DB update error in UpdatePassword for {email}.", ex);
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
    }
}