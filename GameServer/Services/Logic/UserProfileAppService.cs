using BCrypt.Net;
using GameServer.DTOs.User;
using GameServer.Helpers;
using GameServer.Repositories;
using log4net;
using System;
using System.Data.Entity.Infrastructure;
using System.Data.SqlClient;
using System.Threading.Tasks;

namespace GameServer.Services.Logic
{
    public class UserProfileAppService
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(UserProfileAppService));
        private static readonly Random RandomGenerator = new Random();
        private const int MaxUsernameChanges = 3;
        private const int CodeExpirationMinutes = 15;
        private readonly UserProfileRepository _repository;

        public UserProfileAppService(UserProfileRepository repository)
        {
            _repository = repository;
        }

        public async Task<UserProfileDto> GetUserProfileAsync(string identifier)
        {
            UserProfileDto profile = null;
            try
            {
                var player = await _repository.GetPlayerWithDetailsAsync(identifier);

                if (player != null)
                {
                    profile = new UserProfileDto
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
                else
                {
                    Log.WarnFormat("Perfil no encontrado para: {0}", identifier);
                }
            }
            catch (SqlException ex)
            {
                Log.Fatal("Error SQL al obtener perfil.", ex);
            }
            catch (TimeoutException ex)
            {
                Log.Error("Timeout al obtener perfil.", ex);
            }

            return profile;
        }

        public async Task<UsernameChangeResult> ChangeUsernameAsync(string identifier, string newUsername)
        {
            UsernameChangeResult result = UsernameChangeResult.FatalError;
            try
            {
                var player = await _repository.GetPlayerWithDetailsAsync(identifier);

                if (player == null)
                {
                    Log.WarnFormat("Cambio de usuario fallido (Usuario no encontrado): {0}", identifier);
                    result = UsernameChangeResult.UserNotFound;
                }
                else if (player.UsernameChangeCount >= MaxUsernameChanges)
                {
                    Log.WarnFormat("Límite de cambios de usuario alcanzado para {0}.", identifier);
                    result = UsernameChangeResult.LimitReached;
                }
                else
                {
                    bool isTaken = await _repository.IsUsernameTakenAsync(newUsername);
                    if (isTaken)
                    {
                        result = UsernameChangeResult.UsernameAlreadyExists;
                    }
                    else
                    {
                        string oldUsername = player.Username;
                        player.Username = newUsername;
                        player.UsernameChangeCount++;

                        await _repository.SaveChangesAsync();
                        Log.InfoFormat("Usuario cambiado: '{0}' -> '{1}'", oldUsername, newUsername);
                        result = UsernameChangeResult.Success;
                    }
                }
            }
            catch (DbUpdateException ex)
            {
                Log.Error($"Error actualizando usuario para {identifier}.", ex);
                result = UsernameChangeResult.FatalError;
            }
            catch (SqlException ex)
            {
                Log.Fatal("Error SQL crítico al cambiar usuario.", ex);
                result = UsernameChangeResult.FatalError;
            }

            return result;
        }

        public async Task<bool> ChangeAvatarAsync(string identifier, string avatarName)
        {
            bool isSuccess = false;
            try
            {
                var player = await _repository.GetPlayerWithDetailsAsync(identifier);
                if (player != null)
                {
                    player.Avatar = avatarName;
                    await _repository.SaveChangesAsync();
                    Log.InfoFormat("Avatar actualizado para {0}: {1}", identifier, avatarName);
                    isSuccess = true;
                }
                else
                {
                    Log.WarnFormat("Cambio de avatar fallido (Usuario no encontrado): {0}", identifier);
                }
            }
            catch (SqlException ex)
            {
                Log.Error($"Error SQL cambiando avatar para {identifier}.", ex);
            }
            catch (DbUpdateException ex)
            {
                Log.Error($"Error DB cambiando avatar para {identifier}.", ex);
            }

            return isSuccess;
        }

        public async Task<bool> SendPasswordChangeCodeAsync(string identifier)
        {
            bool isSent = false;
            try
            {
                var player = await _repository.GetPlayerWithDetailsAsync(identifier);
                if (player != null)
                {
                    var account = player.Account;
                    string verifyCode = RandomGenerator.Next(100000, 999999).ToString();

                    account.VerificationCode = verifyCode;
                    account.CodeExpiration = DateTime.Now.AddMinutes(CodeExpirationMinutes);

                    await _repository.SaveChangesAsync();

                    isSent = await EmailHelper.SendVerificationEmailAsync(account.Email, verifyCode).ConfigureAwait(false);
                    if (isSent) Log.InfoFormat("Código de cambio de pass enviado a {0}", account.Email);
                }
                else
                {
                    Log.WarnFormat("Solicitud de código fallida (Usuario no encontrado): {0}", identifier);
                }
            }
            catch (SqlException ex)
            {
                Log.Error("Error SQL enviando código pass.", ex);
            }
            catch (DbUpdateException ex)
            {
                Log.Error("Error DB enviando código pass.", ex);
            }

            return isSent;
        }

        public async Task<bool> ChangePasswordWithCodeAsync(ChangePasswordRequest request)
        {
            bool isChanged = false;
            try
            {
                var player = await _repository.GetPlayerWithDetailsAsync(request.Email);
                if (player != null)
                {
                    var account = player.Account;

                    if (account.VerificationCode == request.Code && account.CodeExpiration >= DateTime.Now)
                    {
                        if (!BCrypt.Net.BCrypt.Verify(request.NewPassword, account.PasswordHash))
                        {
                            account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
                            account.VerificationCode = null;
                            account.CodeExpiration = null;

                            await _repository.SaveChangesAsync();
                            Log.InfoFormat("Contraseña cambiada exitosamente para {0}", request.Email);
                            isChanged = true;
                        }
                        else
                        {
                            Log.WarnFormat("Intento de reusar contraseña: {0}", request.Email);
                        }
                    }
                    else
                    {
                        Log.WarnFormat("Código inválido o expirado para cambio de pass: {0}", request.Email);
                    }
                }
            }
            catch (SqlException ex)
            {
                Log.Error("Error SQL cambiando password con código.", ex);
            }
            catch (DbUpdateException ex)
            {
                Log.Error("Error DB cambiando password con código.", ex);
            }

            return isChanged;
        }
    }
}