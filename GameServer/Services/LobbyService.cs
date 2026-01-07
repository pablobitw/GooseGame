using GameServer.DTOs.Lobby;
using GameServer.Interfaces;
using GameServer.Repositories;
using GameServer.Services.Logic;
using GameServer.Helpers;
using log4net;
using System;
using System.ServiceModel;
using System.Threading.Tasks;

namespace GameServer.Services
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerCall, ConcurrencyMode = ConcurrencyMode.Multiple)]
    public class LobbyService : ILobbyService, IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(LobbyService));
        private readonly ILobbyRepository _repository;
        private readonly LobbyAppService _logic;

        public LobbyService()
        {
            _repository = new LobbyRepository();
            _logic = new LobbyAppService(_repository);
        }

        public async Task<LobbyCreationResultDto> CreateLobbyAsync(CreateLobbyRequest request)
        {
            ILobbyServiceCallback callback = null;
            LobbyCreationResultDto result = new LobbyCreationResultDto();

            try
            {
                callback = OperationContext.Current.GetCallbackChannel<ILobbyServiceCallback>();
            }
            catch (Exception ex)
            {
                Log.Warn("CreateLobbyAsync: No se pudo obtener el canal de Callback.", ex);
            }

            try
            {
                result = await _logic.CreateLobbyAsync(request);
            }
            catch (Exception ex)
            {
                Log.Error("CreateLobbyAsync: Error crítico no controlado en lógica.", ex);
                result.Success = false;
                result.ErrorMessage = "Error interno del servidor.";
                result.ErrorType = LobbyErrorType.Unknown;
            }

            if (result != null && result.Success && callback != null)
            {
                try
                {
                    ConnectionManager.RegisterLobbyClient(request.HostUsername, callback);
                }
                catch (Exception ex)
                {
                    Log.Error($"CreateLobbyAsync: Error registrando cliente {request.HostUsername}.", ex);
                }
            }

            return result ?? new LobbyCreationResultDto { Success = false, ErrorType = LobbyErrorType.Unknown };
        }

        public async Task<JoinLobbyResultDto> JoinLobbyAsync(JoinLobbyRequest request)
        {
            ILobbyServiceCallback callback = null;
            JoinLobbyResultDto result = new JoinLobbyResultDto();

            try
            {
                callback = OperationContext.Current.GetCallbackChannel<ILobbyServiceCallback>();
            }
            catch (Exception ex)
            {
                Log.Warn("JoinLobbyAsync: No se pudo obtener el canal de Callback.", ex);
            }

            try
            {
                result = await _logic.JoinLobbyAsync(request);
            }
            catch (Exception ex)
            {
                Log.Error("JoinLobbyAsync: Error crítico no controlado en lógica.", ex);
                result.Success = false;
                result.ErrorMessage = "Error interno del servidor.";
                result.ErrorType = LobbyErrorType.Unknown;
            }

            if (result != null && result.Success && callback != null)
            {
                try
                {
                    ConnectionManager.RegisterLobbyClient(request.Username, callback);
                }
                catch (Exception ex)
                {
                    Log.Error($"JoinLobbyAsync: Error registrando cliente {request.Username}.", ex);
                }
            }

            return result ?? new JoinLobbyResultDto { Success = false, ErrorType = LobbyErrorType.Unknown };
        }

        public async Task<bool> StartGameAsync(string lobbyCode)
        {
            try
            {
                return await _logic.StartGameAsync(lobbyCode);
            }
            catch (Exception ex)
            {
                Log.Error($"StartGameAsync: Error inesperado para lobby {lobbyCode}", ex);
                return false;
            }
        }

        public async Task DisbandLobbyAsync(string hostUsername)
        {
            try
            {
                await _logic.DisbandLobbyAsync(hostUsername);
            }
            catch (Exception ex)
            {
                Log.Error($"DisbandLobbyAsync: Error inesperado para host {hostUsername}", ex);
            }
        }

        public async Task<bool> LeaveLobbyAsync(string username)
        {
            try
            {
                return await _logic.LeaveLobbyAsync(username);
            }
            catch (Exception ex)
            {
                Log.Error($"LeaveLobbyAsync: Error inesperado para usuario {username}", ex);
                return false;
            }
        }

        public async Task<LobbyStateDto> GetLobbyStateAsync(string lobbyCode)
        {
            try
            {
                return await _logic.GetLobbyStateAsync(lobbyCode);
            }
            catch (Exception ex)
            {
                Log.Error($"GetLobbyStateAsync: Error inesperado para lobby {lobbyCode}", ex);
                return null;
            }
        }

        public async Task<ActiveMatchDto[]> GetPublicMatchesAsync()
        {
            try
            {
                return await _logic.GetPublicMatchesAsync();
            }
            catch (Exception ex)
            {
                Log.Error("GetPublicMatchesAsync: Error inesperado.", ex);
                return new ActiveMatchDto[0];
            }
        }

        public async Task<bool> KickPlayerAsync(KickPlayerRequest request)
        {
            try
            {
                return await _logic.KickPlayerAsync(request);
            }
            catch (Exception ex)
            {
                Log.Error("KickPlayerAsync: Error inesperado.", ex);
                return false;
            }
        }

        public void Dispose()
        {
            _repository?.Dispose();
        }
    }
}