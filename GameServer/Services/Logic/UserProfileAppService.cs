using GameServer.DTOs.User;
using GameServer.Faults;
using GameServer.Helpers;
using GameServer.Models;
using GameServer.Repositories;
using GameServer.Repositories.Interfaces;
using GameServer.Services.Common;
using log4net;
using System;
using System.Collections.Generic;
using System.Data.Entity.Core;
using System.Data.Entity.Infrastructure;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;

namespace GameServer.Services.Logic
{
    public class UserProfileAppService
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(UserProfileAppService));
        private const int MaxUsernameChanges = 3;
        private const int CodeExpirationMinutes = 15;

        private readonly IUserProfileRepository _repository;
        private readonly IEmailService _emailService;
        private readonly ISecurityCodeGenerator _codeGenerator;
        private readonly IPasswordHasher _passwordHasher;

        public UserProfileAppService(
            IUserProfileRepository repository = null,
            IEmailService emailService = null,
            ISecurityCodeGenerator codeGenerator = null,
            IPasswordHasher passwordHasher = null)
        {
            _repository = repository ?? new UserProfileRepository();
            _emailService = emailService ?? new EmailService();
            _codeGenerator = codeGenerator ?? new SecurityCodeGenerator();
            _passwordHasher = passwordHasher ?? new PasswordHasher();
        }

        public async Task<UserProfileDto> GetUserProfileAsync(string identifier)
        {
            UserProfileDto result = null;
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

                    result = new UserProfileDto
                    {
                        Username = player.Username,
                        Email = emailDisplay,
                        AvatarPath = player.Avatar,
                        Coins = player.Coins,
                        MatchesPlayed = player.PlayerStat?.MatchesPlayed ?? 0,
                        MatchesWon = player.PlayerStat?.MatchesWon ?? 0,
                        UsernameChangeCount = player.UsernameChangeCount,
                        PreferredLanguage = player.Account?.PreferredLanguage,
                        SocialLinks = new List<PlayerSocialLinkDto>()
                    };

                    if (player.PlayerSocialLinks != null)
                    {
                        foreach (var link in player.PlayerSocialLinks)
                        {
                            result.SocialLinks.Add(new PlayerSocialLinkDto
                            {
                                SocialType = ((SocialType)link.SocialType).ToString(),
                                Url = link.Url
                            });
                        }
                    }
                }
                else
                {
                    Log.WarnFormat("Perfil no encontrado para: {0}", identifier);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error crítico al obtener perfil de {identifier}", ex);
                throw ExceptionManager.Map(ex);
            }
            return result;
        }

        public async Task<bool> SendUsernameChangeCodeAsync(string identifier)
        {
            bool result = false;
            try
            {
                var player = await _repository.GetPlayerWithDetailsAsync(identifier);

                if (player != null && !player.IsGuest && player.Account != null)
                {
                    var account = player.Account;
                    string verifyCode = _codeGenerator.Next(100000, 999999).ToString();

                    account.VerificationCode = verifyCode;
                    account.CodeExpiration = DateTime.Now.AddMinutes(CodeExpirationMinutes);

                    await _repository.SaveChangesAsync();

                    bool isSent = await _emailService.SendVerificationEmailAsync(account.Email, verifyCode, account.PreferredLanguage).ConfigureAwait(false);

                    if (isSent) Log.InfoFormat("Código de cambio de usuario enviado a {0}", account.Email);
                    result = isSent;
                }
                else
                {
                    Log.WarnFormat("Solicitud de código inválida para: {0}", identifier);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error crítico enviando código de cambio de usuario a {identifier}", ex);
                throw ExceptionManager.Map(ex);
            }
            return result;
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
                        _ = _emailService.SendUsernameChangedNotificationAsync(player.Account.Email, oldUsername, newUsername, player.Account.PreferredLanguage);

                        Log.InfoFormat("Usuario cambiado: '{0}' -> '{1}'", oldUsername, newUsername);
                        result = UsernameChangeResult.Success;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error crítico al cambiar usuario para {identifier}", ex);
                throw ExceptionManager.Map(ex);
            }
            return result;
        }

        public async Task<bool> ChangeAvatarAsync(string identifier, string avatarName)
        {
            bool result = false;
            try
            {
                var player = await _repository.GetPlayerWithDetailsAsync(identifier);
                if (player != null)
                {
                    player.Avatar = avatarName;
                    await _repository.SaveChangesAsync();
                    Log.InfoFormat("Avatar actualizado para {0}: {1}", identifier, avatarName);
                    result = true;
                }
                else
                {
                    Log.WarnFormat("Cambio de avatar fallido (Usuario no encontrado): {0}", identifier);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error crítico cambiando avatar para {identifier}", ex);
                throw ExceptionManager.Map(ex);
            }
            return result;
        }

        public async Task<bool> SendPasswordChangeCodeAsync(string identifier)
        {
            bool result = false;
            try
            {
                var player = await _repository.GetPlayerWithDetailsAsync(identifier);

                if (player != null && !player.IsGuest && player.Account != null)
                {
                    var account = player.Account;
                    string verifyCode = _codeGenerator.Next(100000, 999999).ToString();

                    account.VerificationCode = verifyCode;
                    account.CodeExpiration = DateTime.Now.AddMinutes(CodeExpirationMinutes);

                    await _repository.SaveChangesAsync();

                    bool isSent = await _emailService.SendVerificationEmailAsync(account.Email, verifyCode, account.PreferredLanguage).ConfigureAwait(false);
                    if (isSent) Log.InfoFormat("Código de cambio de pass enviado a {0}", account.Email);
                    result = isSent;
                }
                else
                {
                    Log.WarnFormat("Solicitud inválida (Usuario no encontrado o es Invitado): {0}", identifier);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error crítico enviando código pass a {identifier}", ex);
                throw ExceptionManager.Map(ex);
            }
            return result;
        }

        public async Task<bool> ChangePasswordWithCodeAsync(ChangePasswordRequest request)
        {
            bool result = false;
            try
            {
                var player = await _repository.GetPlayerWithDetailsAsync(request.Email);

                if (player != null && !player.IsGuest && player.Account != null)
                {
                    var account = player.Account;

                    if (account.VerificationCode == request.Code && account.CodeExpiration >= DateTime.Now)
                    {
                        if (!_passwordHasher.Verify(request.NewPassword, account.PasswordHash))
                        {
                            account.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);
                            account.VerificationCode = null;
                            account.CodeExpiration = null;

                            await _repository.SaveChangesAsync();
                            _ = _emailService.SendPasswordChangedNotificationAsync(account.Email, player.Username, account.PreferredLanguage);

                            Log.InfoFormat("Contraseña cambiada exitosamente para {0}", request.Email);
                            result = true;
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
            catch (Exception ex)
            {
                Log.Error($"Error crítico cambiando password con código para {request.Email}", ex);
                throw ExceptionManager.Map(ex);
            }
            return result;
        }

        public async Task<bool> UpdateLanguageAsync(string identifier, string languageCode)
        {
            bool result = false;
            try
            {
                var player = await _repository.GetPlayerWithDetailsAsync(identifier);

                if (player != null && player.Account != null)
                {
                    string shortCode = languageCode.Length > 5 ? languageCode.Substring(0, 5) : languageCode;

                    player.Account.PreferredLanguage = shortCode;
                    await _repository.SaveChangesAsync();

                    Log.InfoFormat("Idioma actualizado para {0} a {1}", identifier, shortCode);
                    result = true;
                }
                else
                {
                    Log.WarnFormat("Intento de cambio de idioma para usuario no encontrado: {0}", identifier);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error crítico actualizando idioma para {identifier}", ex);
                throw ExceptionManager.Map(ex);
            }
            return result;
        }

        public async Task<bool> DeactivateAccountAsync(DeactivateAccountRequest request)
        {
            bool result = false;
            try
            {
                var player = await _repository.GetPlayerWithDetailsAsync(request.Username);

                if (player != null && player.Account != null && !player.IsGuest)
                {
                    if (_passwordHasher.Verify(request.Password, player.Account.PasswordHash))
                    {
                        player.Account.AccountStatus = 2;
                        await _repository.SaveChangesAsync();

                        Log.InfoFormat("Cuenta desactivada exitosamente: {0}", request.Username);
                        result = true;
                    }
                    else
                    {
                        Log.WarnFormat("Fallo de autenticación al desactivar cuenta: {0}", request.Username);
                    }
                }
                else
                {
                    Log.WarnFormat("Intento de desactivar cuenta inexistente/invitado: {0}", request.Username);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error crítico al desactivar cuenta de {request.Username}", ex);
                throw ExceptionManager.Map(ex);
            }
            return result;
        }

        public async Task<string> AddSocialLinkAsync(string identifier, string url)
        {
            string result = null;
            try
            {
                var player = await _repository.GetPlayerWithDetailsAsync(identifier);
                if (player == null)
                {
                    result = "Usuario no encontrado.";
                }
                else
                {
                    var existingTypes = player.PlayerSocialLinks.Select(l => (SocialType)l.SocialType);

                    if (!PlayerSocialLinkHelper.CanAddSocialLink(existingTypes, url, out SocialType newType, out string validationMsg))
                    {
                        Log.WarnFormat("Intento inválido de agregar red social para {0}: {1}", identifier, validationMsg);
                        result = validationMsg;
                    }
                    else
                    {
                        var newLink = new PlayerSocialLink
                        {
                            PlayerIdPlayer = player.IdPlayer,
                            SocialType = (byte)newType,
                            Url = url
                        };

                        player.PlayerSocialLinks.Add(newLink);
                        await _repository.SaveChangesAsync();

                        Log.InfoFormat("Red social agregada para {0}: {1} ({2})", identifier, newType, url);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error crítico al agregar red social para {identifier}", ex);
                throw ExceptionManager.Map(ex);
            }
            return result;
        }

        public async Task<bool> RemoveSocialLinkAsync(string identifier, string urlToRemove)
        {
            bool result = false;
            try
            {
                var player = await _repository.GetPlayerWithDetailsAsync(identifier);
                if (player != null && player.PlayerSocialLinks != null)
                {
                    var link = player.PlayerSocialLinks.FirstOrDefault(l => l.Url == urlToRemove);

                    if (link != null)
                    {
                        _repository.DeleteSocialLink(link);
                        await _repository.SaveChangesAsync();
                        Log.InfoFormat("Red social eliminada para {0}: {1}", identifier, urlToRemove);
                        result = true;
                    }
                    else
                    {
                        Log.WarnFormat("Intento de borrar red social no encontrada: {0} -> {1}", identifier, urlToRemove);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error crítico al eliminar red social para {identifier}", ex);
                throw ExceptionManager.Map(ex);
            }
            return result;
        }
    }
}