using GameServer.DTOs.Lobby;
using GameServer.Helpers;
using GameServer.Repositories;
using GameServer.Interfaces;
using log4net;
using System;
using System.Collections.Generic;
using System.Data.Entity.Core;
using System.Data.Entity.Infrastructure;
using System.Data.SqlClient;
using System.Linq;
using System.Security.Cryptography;
using System.ServiceModel;
using System.Threading.Tasks;
using GameServer;

namespace GameServer.Services.Logic
{
    [ServiceBehavior(ConcurrencyMode = ConcurrencyMode.Multiple, InstanceContextMode = InstanceContextMode.PerCall)]
    public class LobbyAppService : ILobbyService
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(LobbyAppService));
        private readonly ILobbyRepository _repository;

        public LobbyAppService(ILobbyRepository repository)
        {
            if (repository == null) throw new ArgumentNullException(nameof(repository));
            _repository = repository;
        }

        private void FireAndForgetNotification(List<string> usernames, Action<ILobbyServiceCallback> notificationAction)
        {
            if (usernames == null || !usernames.Any()) return;

            var safeList = new List<string>(usernames);

            _ = Task.Run(() =>
            {
                NotifyUsersSafe(safeList, notificationAction);
            });
        }

        private void NotifyUsersSafe(List<string> usernames, Action<ILobbyServiceCallback> notificationAction)
        {
            foreach (var username in usernames)
            {
                var client = ConnectionManager.GetLobbyClient(username);
                if (client != null)
                {
                    try
                    {
                        notificationAction(client);
                    }
                    catch (Exception ex)
                    {
                        Log.Warn($"Error notificando a {username}: {ex.Message}");
                    }
                }
            }
        }

        private async Task CleanPlayerStateIfNeeded(Player player)
        {
            if (player.GameIdGame != null)
            {
                try
                {
                    var oldGame = await _repository.GetGameByIdAsync(player.GameIdGame.Value);
                    if (oldGame == null || oldGame.GameStatus == (int)GameStatus.Finished || oldGame.GameStatus == (int)GameStatus.WaitingForPlayers)
                    {
                        player.GameIdGame = null;
                        await _repository.SaveChangesAsync();
                        Log.InfoFormat("Limpieza automática: Jugador {0} liberado.", player.Username);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"Error limpiando estado jugador {player.Username}: {ex.Message}");
                }
            }
        }

        public async Task<LobbyCreationResultDto> CreateLobbyAsync(CreateLobbyRequest request)
        {
            var result = new LobbyCreationResultDto();
            if (request?.Settings == null)
            {
                result.ErrorMessage = "Datos inválidos.";
                result.ErrorType = LobbyErrorType.InvalidData;
                return result;
            }

            if (request.Settings.MaxPlayers < 2 || request.Settings.MaxPlayers > 4)
            {
                result.ErrorMessage = "Jugadores entre 2 y 4.";
                result.ErrorType = LobbyErrorType.InvalidData;
                return result;
            }

            try
            {
                var callback = OperationContext.Current != null
                    ? OperationContext.Current.GetCallbackChannel<ILobbyServiceCallback>()
                    : null;

                var hostPlayer = await _repository.GetPlayerByUsernameAsync(request.HostUsername);
                if (hostPlayer == null)
                {
                    result.ErrorMessage = "Jugador anfitrión no encontrado.";
                    result.ErrorType = LobbyErrorType.UserNotFound;
                    return result;
                }

                if (hostPlayer.IsGuest)
                {
                    result.ErrorMessage = "Los invitados no pueden crear partidas.";
                    result.ErrorType = LobbyErrorType.GuestNotAllowed;
                    return result;
                }

                if (callback != null)
                {
                    ConnectionManager.RegisterLobbyClient(request.HostUsername, callback);
                }

                await CleanPlayerStateIfNeeded(hostPlayer);

                if (hostPlayer.GameIdGame != null)
                {
                    result.ErrorMessage = "Ya estás en una partida activa.";
                    result.ErrorType = LobbyErrorType.PlayerAlreadyInGame;
                    return result;
                }

                string newLobbyCode = GenerateLobbyCode();
                var newGame = new Game
                {
                    GameStatus = (int)GameStatus.WaitingForPlayers,
                    HostPlayerID = hostPlayer.IdPlayer,
                    Board_idBoard = request.Settings.BoardId,
                    IsPublic = request.Settings.IsPublic,
                    MaxPlayers = request.Settings.MaxPlayers,
                    LobbyCode = newLobbyCode,
                    StartTime = DateTime.UtcNow
                };

                _repository.AddGame(newGame);
                await _repository.SaveChangesAsync();

                hostPlayer.GameIdGame = newGame.IdGame;
                await _repository.SaveChangesAsync();

                Log.InfoFormat("Lobby creado por '{0}'. Código: {1}", request.HostUsername, newLobbyCode);
                result.Success = true;
                result.LobbyCode = newLobbyCode;
                result.ErrorType = LobbyErrorType.None;
            }
            catch (Exception ex)
            {
                HandleException(ex, result);
            }

            return result;
        }

        public async Task<JoinLobbyResultDto> JoinLobbyAsync(JoinLobbyRequest request)
        {
            var result = new JoinLobbyResultDto();

            try
            {
                var callback = OperationContext.Current != null
                    ? OperationContext.Current.GetCallbackChannel<ILobbyServiceCallback>()
                    : null;

                var player = await _repository.GetPlayerByUsernameAsync(request.Username);
                if (player == null)
                {
                    result.ErrorType = LobbyErrorType.UserNotFound;
                    result.ErrorMessage = "Usuario no encontrado";
                    return result;
                }

                if (callback != null)
                {
                    ConnectionManager.RegisterLobbyClient(request.Username, callback);
                }

                await CleanPlayerStateIfNeeded(player);

                if (player.GameIdGame != null)
                {
                    result.ErrorType = LobbyErrorType.PlayerAlreadyInGame;
                    result.ErrorMessage = "Ya estás en partida";
                    return result;
                }

                var game = await _repository.GetGameByCodeAsync(request.LobbyCode);
                if (game == null)
                {
                    result.ErrorType = LobbyErrorType.GameNotFound;
                    result.ErrorMessage = "Partida no encontrada";
                    return result;
                }

                if (game.GameStatus != (int)GameStatus.WaitingForPlayers)
                {
                    result.ErrorType = LobbyErrorType.GameStarted;
                    result.ErrorMessage = "Partida ya iniciada";
                    return result;
                }

                var playersInLobby = await _repository.GetPlayersInGameAsync(game.IdGame);
                if (playersInLobby.Count >= game.MaxPlayers)
                {
                    result.ErrorType = LobbyErrorType.GameFull;
                    result.ErrorMessage = "Partida llena";
                    return result;
                }

                player.GameIdGame = game.IdGame;
                await _repository.SaveChangesAsync();

                Log.InfoFormat("Jugador '{0}' unido al lobby {1}", request.Username, request.LobbyCode);

                var updatedPlayers = await _repository.GetPlayersInGameAsync(game.IdGame);

                result.Success = true;
                result.BoardId = game.Board_idBoard;
                result.MaxPlayers = game.MaxPlayers;
                result.IsHost = (player.IdPlayer == game.HostPlayerID);
                result.IsPublic = game.IsPublic;
                result.PlayersInLobby = updatedPlayers.Select(p => new PlayerLobbyDto
                {
                    Username = p.Username,
                    IsHost = (p.IdPlayer == game.HostPlayerID)
                }).ToList();
                result.ErrorType = LobbyErrorType.None;

                var usernamesToNotify = updatedPlayers
                    .Where(p => p.Username != request.Username)
                    .Select(p => p.Username)
                    .ToList();

                var newPlayerDto = new PlayerLobbyDto { Username = request.Username, IsHost = false };
                FireAndForgetNotification(usernamesToNotify, client => client.OnPlayerJoined(newPlayerDto));
            }
            catch (Exception ex)
            {
                Log.Error("Error en JoinLobby", ex);
                result.Success = false;
                if (ex is SqlException || ex is EntityException) result.ErrorType = LobbyErrorType.DatabaseError;
                else if (ex is TimeoutException) result.ErrorType = LobbyErrorType.ServerTimeout;
                else result.ErrorType = LobbyErrorType.Unknown;
                result.ErrorMessage = "Error al unirse.";
            }

            return result;
        }

        public async Task<bool> StartGameAsync(string lobbyCode)
        {
            try
            {
                var game = await _repository.GetGameByCodeAsync(lobbyCode);
                if (game != null)
                {
                    var players = await _repository.GetPlayersInGameAsync(game.IdGame);
                    if (players.Count >= 2)
                    {
                        game.GameStatus = (int)GameStatus.InProgress;
                        await _repository.SaveChangesAsync();

                        try
                        {
                            GameManager.Instance.StartMonitoring(game.IdGame);
                            Log.InfoFormat("Juego {0} iniciado y monitoreado.", lobbyCode);
                        }
                        catch (Exception gmEx)
                        {
                            Log.Error($"CRITICAL: GameManager falló al iniciar monitoreo para {lobbyCode}", gmEx);
                        }

                        var usernames = players.Select(p => p.Username).ToList();
                        FireAndForgetNotification(usernames, client => client.OnGameStarted());

                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("Error en StartGame", ex);
            }
            return false;
        }

        public async Task DisbandLobbyAsync(string hostUsername)
        {
            try
            {
                var hostPlayer = await _repository.GetPlayerByUsernameAsync(hostUsername);
                if (hostPlayer != null && hostPlayer.GameIdGame != null)
                {
                    int gameId = hostPlayer.GameIdGame.Value;
                    var game = await _repository.GetGameByIdAsync(gameId);

                    if (game != null)
                    {
                        var players = await _repository.GetPlayersInGameAsync(gameId);

                        var usernamesToNotify = players
                            .Where(p => p.Username != hostUsername)
                            .Select(p => p.Username)
                            .ToList();

                        FireAndForgetNotification(usernamesToNotify, client => client.OnLobbyDisbanded());

                        await Task.Delay(100);

                        try { GameManager.Instance.StopMonitoring(gameId); } catch { }

                        _repository.DeleteGameAndCleanDependencies(game);
                        await _repository.SaveChangesAsync();
                        Log.InfoFormat("Lobby {0} disuelto por {1}.", game.LobbyCode, hostUsername);
                    }
                    else
                    {
                        hostPlayer.GameIdGame = null;
                        await _repository.SaveChangesAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("Error en DisbandLobby", ex);
            }
        }

        public async Task<bool> LeaveLobbyAsync(string username)
        {
            try
            {
                var player = await _repository.GetPlayerByUsernameAsync(username);
                if (player != null && player.GameIdGame != null)
                {
                    int gameId = player.GameIdGame.Value;
                    var game = await _repository.GetGameByIdAsync(gameId);

                    if (game != null && game.HostPlayerID == player.IdPlayer)
                    {
                        await DisbandLobbyAsync(username);
                        return true;
                    }

                    var allPlayers = await _repository.GetPlayersInGameAsync(gameId);
                    var usernamesToNotify = allPlayers
                        .Where(p => p.Username != username)
                        .Select(p => p.Username)
                        .ToList();

                    player.GameIdGame = null;
                    await _repository.SaveChangesAsync();

                    ConnectionManager.UnregisterLobbyClient(username);
                    Log.InfoFormat("Jugador {0} salió del lobby.", username);

                    bool gameClosed = await HandleGameShutdownIfNeeded(gameId);

                    if (!gameClosed)
                    {
                        FireAndForgetNotification(usernamesToNotify, client => client.OnPlayerLeft(username));
                    }
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log.Error("Error en LeaveLobby", ex);
            }
            return false;
        }

        public async Task<bool> KickPlayerAsync(KickPlayerRequest request)
        {
            if (request == null || request.RequestorUsername == request.TargetUsername) return false;

            try
            {
                var game = await _repository.GetGameByCodeAsync(request.LobbyCode);
                if (game != null)
                {
                    var host = await _repository.GetPlayerByUsernameAsync(request.RequestorUsername);
                    if (host != null && game.HostPlayerID == host.IdPlayer)
                    {
                        var target = await _repository.GetPlayerByUsernameAsync(request.TargetUsername);
                        if (target != null && target.GameIdGame == game.IdGame)
                        {
                            target.KickCount++;
                            bool isBanned = (target.KickCount >= 3);
                            if (isBanned) target.IsBanned = true;

                            target.GameIdGame = null;
                            await _repository.SaveChangesAsync();

                            var remainingPlayers = await _repository.GetPlayersInGameAsync(game.IdGame);
                            var usernamesToNotify = remainingPlayers.Select(p => p.Username).ToList();

                            string msg = isBanned ? "Has sido BANEADO." : "Has sido expulsado.";

                            _ = Task.Run(() => NotifyClientDirect(request.TargetUsername, c => c.OnPlayerKicked(msg)));

                            FireAndForgetNotification(usernamesToNotify, client => client.OnPlayerLeft(request.TargetUsername));

                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("Error en KickPlayer", ex);
            }
            return false;
        }

        public async Task SystemKickPlayerAsync(string lobbyCode, string username, string reason)
        {
            try
            {
                var game = await _repository.GetGameByCodeAsync(lobbyCode);
                var player = await _repository.GetPlayerByUsernameAsync(username);

                if (game != null && player != null && player.GameIdGame == game.IdGame)
                {
                    _ = Task.Run(() => NotifyClientDirect(username, c => c.OnPlayerKicked(reason)));

                    player.GameIdGame = null;
                    await _repository.SaveChangesAsync();

                    var remainingPlayers = await _repository.GetPlayersInGameAsync(game.IdGame);
                    var usernamesToNotify = remainingPlayers.Select(p => p.Username).ToList();
                    FireAndForgetNotification(usernamesToNotify, client => client.OnPlayerLeft(username));

                    Log.Info($"System Kick aplicado a {username} en lobby {lobbyCode}. Razón: {reason}");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error en SystemKickPlayerAsync para {username}", ex);
            }
        }

        private void NotifyClientDirect(string username, Action<ILobbyServiceCallback> action)
        {
            var client = ConnectionManager.GetLobbyClient(username);
            if (client != null)
            {
                try { action(client); } catch { ConnectionManager.UnregisterLobbyClient(username); }
            }
        }

        public async Task<LobbyStateDto> GetLobbyStateAsync(string lobbyCode)
        {
            try
            {
                var game = await _repository.GetGameByCodeAsync(lobbyCode);
                if (game != null)
                {
                    var players = await _repository.GetPlayersInGameAsync(game.IdGame);
                    return new LobbyStateDto
                    {
                        IsGameStarted = (game.GameStatus == (int)GameStatus.InProgress),
                        Players = players.Select(p => new PlayerLobbyDto
                        {
                            Username = p.Username,
                            IsHost = (p.IdPlayer == game.HostPlayerID)
                        }).ToList(),
                        BoardId = game.Board_idBoard,
                        MaxPlayers = game.MaxPlayers,
                        IsPublic = game.IsPublic
                    };
                }
            }
            catch (Exception ex)
            {
                Log.Error("Error en GetLobbyState", ex);
            }
            return null;
        }

        public async Task<ActiveMatchDto[]> GetPublicMatchesAsync()
        {
            var matchesList = new List<ActiveMatchDto>();
            try
            {
                var games = await _repository.GetActivePublicGamesAsync();
                foreach (var game in games)
                {
                    int currentCount = await _repository.CountPlayersInGameAsync(game.IdGame);
                    if (currentCount < game.MaxPlayers)
                    {
                        string hostName = await _repository.GetUsernameByIdAsync(game.HostPlayerID);
                        matchesList.Add(new ActiveMatchDto
                        {
                            LobbyCode = game.LobbyCode,
                            HostUsername = hostName,
                            BoardId = game.Board_idBoard,
                            CurrentPlayers = currentCount,
                            MaxPlayers = game.MaxPlayers
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("Error en GetPublicMatches", ex);
            }
            return matchesList.ToArray();
        }

        private async Task<bool> HandleGameShutdownIfNeeded(int gameId)
        {
            try
            {
                var game = await _repository.GetGameByIdAsync(gameId);
                if (game == null) return true;

                var remainingPlayers = await _repository.GetPlayersInGameAsync(game.IdGame);
                if (game.GameStatus == (int)GameStatus.InProgress && remainingPlayers.Count < 2)
                {
                    game.GameStatus = (int)GameStatus.Finished;
                    try { GameManager.Instance.StopMonitoring(gameId); } catch { }
                    await _repository.SaveChangesAsync();
                    return true;
                }
                else if (game.GameStatus == (int)GameStatus.WaitingForPlayers && remainingPlayers.Count == 0)
                {
                    _repository.DeleteGameAndCleanDependencies(game);
                    await _repository.SaveChangesAsync();
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error HandleGameShutdown {gameId}: {ex.Message}");
            }
            return false;
        }

        private string GenerateLobbyCode()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            string code;
            do
            {
                byte[] randomBytes = new byte[5];
                using (var rng = RandomNumberGenerator.Create())
                {
                    rng.GetBytes(randomBytes);
                }
                code = new string(randomBytes.Select(b => chars[b % chars.Length]).ToArray());
            }
            while (!_repository.IsLobbyCodeUnique(code));
            return code;
        }

        private void HandleException(Exception ex, LobbyCreationResultDto result)
        {
            Log.Error("Error creando lobby", ex);
            result.Success = false;
            if (ex is SqlException || ex is EntityException) result.ErrorType = LobbyErrorType.DatabaseError;
            else if (ex is TimeoutException) result.ErrorType = LobbyErrorType.ServerTimeout;
            else result.ErrorType = LobbyErrorType.Unknown;
            result.ErrorMessage = "Error en el servidor.";
        }
    }
}