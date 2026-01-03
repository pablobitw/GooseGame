using BCrypt.Net;
using GameServer.DTOs.User;
using GameServer.Helpers;
using GameServer.Repositories;
using log4net;
using System;
using System.Data.Entity.Core;
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
                    string emailDisplay = "Invitado";
                    if (!player.IsGuest && player.Account != null)
                    {
                        emailDisplay = player.Account.Email;
                    }

                    profile = new UserProfileDto
                    {
                        Username = player.Username,
                        Email = emailDisplay, 
                        AvatarPath = player.Avatar,
                        Coins = player.Coins,
                        MatchesPlayed = player.PlayerStat?.MatchesPlayed ?? 0,
                        MatchesWon = player.PlayerStat?.MatchesWon ?? 0,
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
            catch (EntityException ex)
            {
                Log.Error("Error de Entity Framework al obtener perfil.", ex);
            }
            catch (TimeoutException ex)
            {
                Log.Error("Timeout al obtener perfil.", ex);
            }

            return profile;
        }

        public async Task<bool> SendUsernameChangeCodeAsync(string identifier)
        {
            bool isSent = false;
            try
            {
                var player = await _repository.GetPlayerWithDetailsAsync(identifier);

                if (player != null && !player.IsGuest && player.Account != null)
                {
                    var account = player.Account;
                    string verifyCode = RandomGenerator.Next(100000, 999999).ToString();

                    account.VerificationCode = verifyCode;
                    account.CodeExpiration = DateTime.Now.AddMinutes(CodeExpirationMinutes);

                    await _repository.SaveChangesAsync();

                    isSent = await EmailHelper.SendVerificationEmailAsync(account.Email, verifyCode).ConfigureAwait(false);

                    if (isSent) Log.InfoFormat("Código de cambio de usuario enviado a {0}", account.Email);
                }
                else
                {
                    Log.WarnFormat("Solicitud de código inválida para: {0}", identifier);
                }
            }
            catch (SqlException ex)
            {
                Log.Fatal("Error SQL enviando código de cambio de usuario.", ex);
            }
            catch (DbUpdateException ex)
            {
                Log.Error("Error DB enviando código de cambio de usuario.", ex);
            }
            catch (EntityException ex)
            {
                Log.Error("Error EF enviando código de cambio de usuario.", ex);
            }
            catch (TimeoutException ex)
            {
                Log.Error("Timeout enviando código de cambio de usuario.", ex);
            }
            catch (Exception ex)
            {
                Log.Error("Error general enviando código de cambio de usuario.", ex);
            }

            return isSent;
        }

        public async Task<UsernameChangeResult> ChangeUsernameAsync(string identifier, string newUsername, string verificationCode)
        {
            UsernameChangeResult result = UsernameChangeResult.FatalError;
            try
            {
                var player = await _repository.GetPlayerWithDetailsAsync(identifier);

                if (player == null || player.Account == null)
                {
                    Log.WarnFormat("Cambio de usuario fallido (Usuario no encontrado): {0}", identifier);
                    result = UsernameChangeResult.UserNotFound;
                }
                else if (player.Account.VerificationCode != verificationCode || player.Account.CodeExpiration < DateTime.Now)
                {
                    Log.WarnFormat("Código incorrecto o expirado para cambio de usuario: {0}", identifier);
                    result = UsernameChangeResult.FatalError;
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

                        player.Account.VerificationCode = null;
                        player.Account.CodeExpiration = null;

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
            catch (EntityException ex)
            {
                Log.Error("Error EF al cambiar usuario.", ex);
                result = UsernameChangeResult.FatalError;
            }
            catch (TimeoutException ex)
            {
                Log.Error("Timeout al cambiar usuario.", ex);
                result = UsernameChangeResult.FatalError;
            }
            catch (Exception ex)
            {
                Log.Error($"Error inesperado al cambiar usuario para {identifier}.", ex);
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
            catch (EntityException ex)
            {
                Log.Error($"Error EF cambiando avatar para {identifier}.", ex);
            }
            catch (TimeoutException ex)
            {
                Log.Error($"Timeout cambiando avatar para {identifier}.", ex);
            }

            return isSuccess;
        }

        public async Task<bool> SendPasswordChangeCodeAsync(string identifier)
        {
            bool isSent = false;
            try
            {
                var player = await _repository.GetPlayerWithDetailsAsync(identifier);

                if (player != null && !player.IsGuest && player.Account != null)
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
                    Log.WarnFormat("Solicitud inválida (Usuario no encontrado o es Invitado): {0}", identifier);
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
            catch (EntityException ex)
            {
                Log.Error("Error EF enviando código pass.", ex);
            }
            catch (TimeoutException ex)
            {
                Log.Error("Timeout enviando código pass.", ex);
            }

            return isSent;
        }

        public async Task<bool> ChangePasswordWithCodeAsync(ChangePasswordRequest request)
        {
            bool isChanged = false;
            try
            {
                var player = await _repository.GetPlayerWithDetailsAsync(request.Email);

                if (player != null && !player.IsGuest && player.Account != null)
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
                else
                {
                    Log.WarnFormat("Intento de cambio de pass en cuenta inválida/invitado: {0}", request.Email);
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
            catch (EntityException ex)
            {
                Log.Error("Error EF cambiando password con código.", ex);
            }
            catch (TimeoutException ex)
            {
                Log.Error("Timeout cambiando password con código.", ex);
            }

            return isChanged;
        }

        public async Task<bool> DeactivateAccountAsync(DeactivateAccountRequest request)
        {
            try
            {
                var player = await _repository.GetPlayerWithDetailsAsync(request.Username);

                if (player == null || player.Account == null)
                {
                    Log.WarnFormat("Intento de desactivar cuenta inexistente o sin cuenta asociada: {0}", request.Username);
                    return false;
                }

                if (player.IsGuest)
                {
                    Log.WarnFormat("Intento de desactivar cuenta de invitado: {0}", request.Username);
                    return false;
                }

                if (!BCrypt.Net.BCrypt.Verify(request.Password, player.Account.PasswordHash))
                {
                    Log.WarnFormat("Fallo de autenticación al desactivar cuenta: {0}", request.Username);
                    return false;
                }

                
                player.Account.AccountStatus = 2; 

                await _repository.SaveChangesAsync();

                Log.InfoFormat("Cuenta desactivada exitosamente: {0}", request.Username);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"Error al desactivar cuenta de {request.Username}", ex);
                return false;
            }
        }
    }
}