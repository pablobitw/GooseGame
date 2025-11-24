using BCrypt.Net;
using GameServer.Contracts;
using GameServer.Helpers;
using log4net;
using System;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;

namespace GameServer.Services
{
    public class UserProfileService : IUserProfileService
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(UserProfileService));
        private static readonly Random RandomGenerator = new Random();

        private const int MaxUsernameChanges = 3;
        private const int CodeExpirationMinutes = 15;

        public async Task<UserProfileDto> GetUserProfileAsync(string email)
        {
            try
            {
                using (var context = new GameDatabase_Container())
                {
                    var player = await context.Players
                        .Include(p => p.Account)
                        .Include(p => p.PlayerStat)
                        .FirstOrDefaultAsync(p => p.Account.Email == email);

                    if (player == null)
                    {
                        Log.WarnFormat("Profile requested for non-existent user email: {0}", email);
                        return null;
                    }

                    return new UserProfileDto
                    {
                        Username = player.Username,
                        Email = player.Account.Email,
                        AvatarPath = player.Avatar,
                        Coins = player.Coins,
                        MatchesPlayed = player.PlayerStat.MatchesPlayed,
                        MatchesWon = player.PlayerStat.MatchesWon,
                        UsernameChangeCount = player.UsernameChangeCount
                    };
                }
            }
            catch (SqlException ex)
            {
                Log.Fatal("Database error while retrieving user profile.", ex);
                return null;
            }
            catch (Exception ex)
            {
                Log.Error($"Unexpected error retrieving profile for {email}.", ex);
                return null;
            }
        }

        public async Task<UsernameChangeResult> ChangeUsernameAsync(string email, string newUsername)
        {
            try
            {
                using (var context = new GameDatabase_Container())
                {
                    var player = await context.Players
                        .Include(p => p.Account)
                        .FirstOrDefaultAsync(p => p.Account.Email == email);

                    if (player == null)
                    {
                        Log.WarnFormat("Username change attempted for unknown email: {0}", email);
                        return UsernameChangeResult.UserNotFound;
                    }

                    if (player.UsernameChangeCount >= MaxUsernameChanges)
                    {
                        Log.WarnFormat("User {0} reached max username changes limit ({1}).", player.Username, MaxUsernameChanges);
                        return UsernameChangeResult.LimitReached;
                    }

                    bool usernameExists = await context.Players.AnyAsync(p => p.Username == newUsername);
                    if (usernameExists)
                    {
                        return UsernameChangeResult.UsernameAlreadyExists;
                    }

                    string oldUsername = player.Username;

                    player.Username = newUsername;
                    player.UsernameChangeCount++;

                    await context.SaveChangesAsync();

                    Log.InfoFormat("Username changed successfully: '{0}' -> '{1}'. Changes used: {2}/{3}",
                        oldUsername, newUsername, player.UsernameChangeCount, MaxUsernameChanges);

                    return UsernameChangeResult.Success;
                }
            }
            catch (DbUpdateException ex)
            {
                Log.Error($"DB Error updating username for {email}.", ex);
                return UsernameChangeResult.FatalError;
            }
            catch (SqlException ex)
            {
                Log.Fatal("SQL Error during username change.", ex);
                return UsernameChangeResult.FatalError;
            }
            catch (Exception ex)
            {
                Log.Error($"Unexpected error changing username for {email}.", ex);
                return UsernameChangeResult.FatalError;
            }
        }

        public async Task<bool> ChangeAvatarAsync(string email, string avatarName)
        {
            try
            {
                using (var context = new GameDatabase_Container())
                {
                    var player = await context.Players
                        .Include(p => p.Account)
                        .FirstOrDefaultAsync(p => p.Account.Email == email);

                    if (player == null)
                    {
                        Log.WarnFormat("Avatar change failed: User {0} not found.", email);
                        return false;
                    }

                    player.Avatar = avatarName;
                    await context.SaveChangesAsync();

                    Log.InfoFormat("Avatar updated for user {0} to '{1}'.", player.Username, avatarName);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error changing avatar for {email}.", ex);
                return false;
            }
        }

        public async Task<bool> SendPasswordChangeCodeAsync(string email)
        {
            bool emailSent = false;
            try
            {
                using (var context = new GameDatabase_Container())
                {
                    var account = await context.Accounts.FirstOrDefaultAsync(a => a.Email == email);

                    if (account == null)
                    {
                        Log.WarnFormat("Password change code requested for unknown email: {0}", email);
                        return false;
                    }

                    string verifyCode = RandomGenerator.Next(100000, 999999).ToString();
                    account.VerificationCode = verifyCode;
                    account.CodeExpiration = DateTime.Now.AddMinutes(CodeExpirationMinutes);

                    await context.SaveChangesAsync();

                  
                    emailSent = await EmailHelper.SendVerificationEmailAsync(email, verifyCode).ConfigureAwait(false);

                    if (emailSent)
                    {
                        Log.InfoFormat("Password change code sent to {0}", email);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error generating password code for {email}.", ex);
            }
            return emailSent;
        }

        public async Task<bool> ChangePasswordWithCodeAsync(string email, string code, string newPassword)
        {
            try
            {
                using (var context = new GameDatabase_Container())
                {
                    var account = await context.Accounts.FirstOrDefaultAsync(a => a.Email == email);

                    if (account == null) return false;

                    if (account.VerificationCode != code)
                    {
                        Log.WarnFormat("Invalid code attempt for password change: {0}", email);
                        return false;
                    }

                    if (account.CodeExpiration < DateTime.Now)
                    {
                        Log.WarnFormat("Expired code attempt for password change: {0}", email);
                        return false;
                    }

                    if (BCrypt.Net.BCrypt.Verify(newPassword, account.PasswordHash))
                    {
                        Log.WarnFormat("User {0} tried to use the same password.", email);
                        return false;
                    }

                    string newHashedPassword = BCrypt.Net.BCrypt.HashPassword(newPassword);
                    account.PasswordHash = newHashedPassword;

                    account.VerificationCode = null;
                    account.CodeExpiration = null;

                    await context.SaveChangesAsync();

                    Log.InfoFormat("Password successfully changed for {0} using verification code.", email);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error changing password with code for {email}.", ex);
                return false;
            }
        }
    }
}


