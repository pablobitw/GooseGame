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
            if (repository == null)
            {
                throw new ArgumentNullException(nameof(repository));
            }
            _repository = repository;
        }

        private void FireAndForgetNotification(int gameId, string excludeUsername, Action<ILobbyServiceCallback> notificationAction)
        {
            Task.Run(async () =>
            {
                await NotifyAllInLobby(gameId, excludeUsername, notificationAction);
            });
        }

        private async Task NotifyAllInLobby(int gameId, string excludeUsername, Action<ILobbyServiceCallback> notificationAction)
        {
            try
            {
                var players = await _repository.GetPlayersInGameAsync(gameId);

                foreach (var player in players)
                {
                    if (player.Username != excludeUsername)
                    {
                        var client = ConnectionManager.GetLobbyClient(player.Username);
                        if (client != null)
                        {
                            try
                            {
                                notificationAction(client);
                            }
                            catch (CommunicationException)
                            {
                                ConnectionManager.UnregisterLobbyClient(player.Username);
                            }
                            catch (TimeoutException)
                            {
                                ConnectionManager.UnregisterLobbyClient(player.Username);
                            }
                            catch (Exception ex)
                            {
                                Log.Warn($"Error notificando cliente {player.Username}: {ex.Message}");
                            }
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                Log.Error($"Error SQL al notificar lobby {gameId}: {ex.Message}");
            }
            catch (EntityException ex)
            {
                Log.Error($"Error Entity al notificar lobby {gameId}: {ex.Message}");
            }
            catch (Exception ex)
            {
                Log.WarnFormat("Error general en notificación masiva al lobby {0}: {1}", gameId, ex.Message);
            }
        }

        private async Task CleanPlayerStateIfNeeded(Player player)
        {
            if (player.GameIdGame != null)
            {
                try
                {
                    var oldGame = await _repository.GetGameByIdAsync(player.GameIdGame.Value);
                    if (oldGame == null || oldGame.GameStatus == (int)GameStatus.Finished)
                    {
                        player.GameIdGame = null;
                        await _repository.SaveChangesAsync();
                        Log.InfoFormat("Limpieza automática: Jugador {0} liberado.", player.Username);
                    }
                }
                catch (SqlException ex)
                {
                    Log.Error($"Error SQL limpiando estado jugador {player.Username}: {ex.Message}");
                }
                catch (EntityException ex)
                {
                    Log.Error($"Error Entity limpiando estado jugador {player.Username}: {ex.Message}");
                }
            }
        }

        public async Task<LobbyCreationResultDto> CreateLobbyAsync(CreateLobbyRequest request)
        {
            LobbyCreationResultDto result = new LobbyCreationResultDto { Success = false };

            if (request != null && request.Settings != null)
            {
                if (request.Settings.MaxPlayers >= 2 && request.Settings.MaxPlayers <= 4)
                {
                    try
                    {
                        var hostPlayer = await _repository.GetPlayerByUsernameAsync(request.HostUsername);
                        if (hostPlayer != null)
                        {
                            if (!hostPlayer.IsGuest)
                            {
                                await CleanPlayerStateIfNeeded(hostPlayer);

                                if (hostPlayer.GameIdGame == null)
                                {
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
                                else
                                {
                                    result.ErrorMessage = "Ya estás en una partida activa.";
                                    result.ErrorType = LobbyErrorType.PlayerAlreadyInGame;
                                }
                            }
                            else
                            {
                                result.ErrorMessage = "Los invitados no pueden crear partidas.";
                                result.ErrorType = LobbyErrorType.GuestNotAllowed;
                            }
                        }
                        else
                        {
                            result.ErrorMessage = "Jugador anfitrión no encontrado.";
                            result.ErrorType = LobbyErrorType.UserNotFound;
                        }
                    }
                    catch (DbUpdateException ex)
                    {
                        Log.Error("Error DB Update en CreateLobby", ex);
                        result.ErrorType = LobbyErrorType.DatabaseError;
                        result.ErrorMessage = "Error de integridad de datos.";
                    }
                    catch (SqlException ex)
                    {
                        Log.Fatal("Error SQL en CreateLobby", ex);
                        result.ErrorType = LobbyErrorType.DatabaseError;
                        result.ErrorMessage = "Error de conexión con la base de datos.";
                    }
                    catch (EntityException ex)
                    {
                        Log.Error("Error Entity en CreateLobby", ex);
                        result.ErrorType = LobbyErrorType.DatabaseError;
                        result.ErrorMessage = "Error interno de datos.";
                    }
                    catch (TimeoutException ex)
                    {
                        Log.Error("Timeout en CreateLobby", ex);
                        result.ErrorType = LobbyErrorType.ServerTimeout;
                        result.ErrorMessage = "Tiempo de espera agotado.";
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Error General en CreateLobby", ex);
                        result.ErrorType = LobbyErrorType.Unknown;
                        result.ErrorMessage = "Error desconocido.";
                    }
                }
                else
                {
                    result.ErrorMessage = "Jugadores entre 2 y 4.";
                    result.ErrorType = LobbyErrorType.InvalidData;
                }
            }
            else
            {
                result.ErrorMessage = "Datos inválidos.";
                result.ErrorType = LobbyErrorType.InvalidData;
            }

            return result;
        }

        public async Task<JoinLobbyResultDto> JoinLobbyAsync(JoinLobbyRequest request)
        {
            JoinLobbyResultDto result = new JoinLobbyResultDto { Success = false };

            try
            {
                var player = await _repository.GetPlayerByUsernameAsync(request.Username);
                if (player != null)
                {
                    await CleanPlayerStateIfNeeded(player);

                    if (player.GameIdGame == null)
                    {
                        var game = await _repository.GetGameByCodeAsync(request.LobbyCode);
                        if (game != null)
                        {
                            if (game.GameStatus == (int)GameStatus.WaitingForPlayers)
                            {
                                var playersInLobby = await _repository.GetPlayersInGameAsync(game.IdGame);
                                if (playersInLobby.Count < game.MaxPlayers)
                                {
                                    player.GameIdGame = game.IdGame;
                                    await _repository.SaveChangesAsync();

                                    Log.InfoFormat("Jugador '{0}' unido al lobby {1}", request.Username, request.LobbyCode);

                                    var updatedPlayers = await _repository.GetPlayersInGameAsync(game.IdGame);
                                    var dtoList = updatedPlayers.Select(p => new PlayerLobbyDto
                                    {
                                        Username = p.Username,
                                        IsHost = (p.IdPlayer == game.HostPlayerID)
                                    }).ToList();

                                    result.Success = true;
                                    result.BoardId = game.Board_idBoard;
                                    result.MaxPlayers = game.MaxPlayers;
                                    result.IsHost = (player.IdPlayer == game.HostPlayerID);
                                    result.IsPublic = game.IsPublic;
                                    result.PlayersInLobby = dtoList;
                                    result.ErrorType = LobbyErrorType.None;

                                    var newPlayerDto = new PlayerLobbyDto { Username = request.Username, IsHost = false };
                                    FireAndForgetNotification(game.IdGame, request.Username, client => client.OnPlayerJoined(newPlayerDto));
                                }
                                else
                                {
                                    result.ErrorType = LobbyErrorType.GameFull;
                                    result.ErrorMessage = "Partida llena";
                                }
                            }
                            else
                            {
                                result.ErrorType = LobbyErrorType.GameStarted;
                                result.ErrorMessage = "Partida ya iniciada";
                            }
                        }
                        else
                        {
                            result.ErrorType = LobbyErrorType.GameNotFound;
                            result.ErrorMessage = "Partida no encontrada";
                        }
                    }
                    else
                    {
                        result.ErrorType = LobbyErrorType.PlayerAlreadyInGame;
                        result.ErrorMessage = "Ya estás en partida";
                    }
                }
                else
                {
                    result.ErrorType = LobbyErrorType.UserNotFound;
                    result.ErrorMessage = "Usuario no encontrado";
                }
            }
            catch (DbUpdateException ex)
            {
                Log.Error("Error DB Update en JoinLobby", ex);
                result.ErrorType = LobbyErrorType.DatabaseError;
                result.ErrorMessage = "Error de actualización.";
            }
            catch (SqlException ex)
            {
                Log.Fatal("Error SQL en JoinLobby", ex);
                result.ErrorType = LobbyErrorType.DatabaseError;
                result.ErrorMessage = "Error de base de datos.";
            }
            catch (EntityException ex)
            {
                Log.Error("Error Entity en JoinLobby", ex);
                result.ErrorType = LobbyErrorType.DatabaseError;
                result.ErrorMessage = "Error interno.";
            }
            catch (TimeoutException ex)
            {
                Log.Error("Timeout en JoinLobby", ex);
                result.ErrorType = LobbyErrorType.ServerTimeout;
                result.ErrorMessage = "Tiempo de espera agotado.";
            }
            catch (Exception ex)
            {
                Log.Error("Error General en JoinLobby", ex);
                result.ErrorType = LobbyErrorType.DatabaseError;
                result.ErrorMessage = "Error del servidor.";
            }

            return result;
        }

        public async Task<LobbyStateDto> GetLobbyStateAsync(string lobbyCode)
        {
            LobbyStateDto result = null;
            try
            {
                var game = await _repository.GetGameByCodeAsync(lobbyCode);
                if (game != null)
                {
                    var players = await _repository.GetPlayersInGameAsync(game.IdGame);
                    var playerDtos = players.Select(p => new PlayerLobbyDto
                    {
                        Username = p.Username,
                        IsHost = (p.IdPlayer == game.HostPlayerID)
                    }).ToList();

                    result = new LobbyStateDto
                    {
                        IsGameStarted = (game.GameStatus == (int)GameStatus.InProgress),
                        Players = playerDtos,
                        BoardId = game.Board_idBoard,
                        MaxPlayers = game.MaxPlayers,
                        IsPublic = game.IsPublic
                    };
                }
            }
            catch (SqlException ex)
            {
                Log.Error("Error SQL en GetLobbyState", ex);
            }
            catch (EntityException ex)
            {
                Log.Error("Error Entity en GetLobbyState", ex);
            }
            catch (TimeoutException ex)
            {
                Log.Error("Timeout en GetLobbyState", ex);
            }
            catch (Exception ex)
            {
                Log.Error("Error General en GetLobbyState", ex);
            }
            return result;
        }

        public async Task<bool> StartGameAsync(string lobbyCode)
        {
            bool success = false;
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

                        GameManager.Instance.StartMonitoring(game.IdGame);
                        Log.InfoFormat("Juego {0} iniciado.", lobbyCode);

                        FireAndForgetNotification(game.IdGame, null, client => client.OnGameStarted());

                        success = true;
                    }
                }
            }
            catch (DbUpdateException ex)
            {
                Log.Error("Error DB Update en StartGame", ex);
            }
            catch (SqlException ex)
            {
                Log.Fatal("Error SQL en StartGame", ex);
            }
            catch (EntityException ex)
            {
                Log.Error("Error Entity en StartGame", ex);
            }
            catch (TimeoutException ex)
            {
                Log.Error("Timeout en StartGame", ex);
            }
            catch (Exception ex)
            {
                Log.Error("Error General en StartGame", ex);
            }
            return success;
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
                        FireAndForgetNotification(gameId, hostUsername, client => client.OnLobbyDisbanded());

                        await Task.Delay(100);

                        GameManager.Instance.StopMonitoring(gameId);
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
            catch (DbUpdateException ex)
            {
                Log.Error("Error DB Update en DisbandLobby", ex);
            }
            catch (SqlException ex)
            {
                Log.Fatal("Error SQL en DisbandLobby", ex);
            }
            catch (EntityException ex)
            {
                Log.Error("Error Entity en DisbandLobby", ex);
            }
            catch (TimeoutException ex)
            {
                Log.Error("Timeout en DisbandLobby", ex);
            }
            catch (Exception ex)
            {
                Log.Error("Error General en DisbandLobby", ex);
            }
        }

        public async Task<bool> LeaveLobbyAsync(string username)
        {
            bool success = false;
            try
            {
                var player = await _repository.GetPlayerByUsernameAsync(username);
                if (player != null && player.GameIdGame != null)
                {
                    int gameId = player.GameIdGame.Value;
                    player.GameIdGame = null;
                    await _repository.SaveChangesAsync();

                    ConnectionManager.UnregisterLobbyClient(username);
                    Log.InfoFormat("Jugador {0} salió del lobby.", username);

                    bool gameClosed = await HandleGameShutdownIfNeeded(gameId);
                    if (!gameClosed)
                    {
                        FireAndForgetNotification(gameId, username, client => client.OnPlayerLeft(username));
                    }
                    success = true;
                }
            }
            catch (DbUpdateException ex)
            {
                Log.Error("Error DB Update en LeaveLobby", ex);
            }
            catch (SqlException ex)
            {
                Log.Fatal("Error SQL en LeaveLobby", ex);
            }
            catch (EntityException ex)
            {
                Log.Error("Error Entity en LeaveLobby", ex);
            }
            catch (TimeoutException ex)
            {
                Log.Error("Timeout en LeaveLobby", ex);
            }
            catch (Exception ex)
            {
                Log.Error("Error General en LeaveLobby", ex);
            }
            return success;
        }

        private async Task<bool> HandleGameShutdownIfNeeded(int gameId)
        {
            bool shutdown = false;
            try
            {
                var game = await _repository.GetGameByIdAsync(gameId);
                if (game != null)
                {
                    var remainingPlayers = await _repository.GetPlayersInGameAsync(game.IdGame);

                    if (game.GameStatus == (int)GameStatus.InProgress && remainingPlayers.Count < 2)
                    {
                        game.GameStatus = (int)GameStatus.Finished;
                        GameManager.Instance.StopMonitoring(gameId);
                        await _repository.SaveChangesAsync();
                        shutdown = true;
                    }
                    else if (game.GameStatus == (int)GameStatus.WaitingForPlayers && remainingPlayers.Count == 0)
                    {
                        _repository.DeleteGameAndCleanDependencies(game);
                        await _repository.SaveChangesAsync();
                        shutdown = true;
                    }
                }
                else
                {
                    shutdown = true;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error en HandleGameShutdownIfNeeded para {gameId}: {ex.Message}");
            }
            return shutdown;
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
            catch (SqlException ex)
            {
                Log.Error("Error SQL en GetPublicMatches", ex);
            }
            catch (EntityException ex)
            {
                Log.Error("Error Entity en GetPublicMatches", ex);
            }
            catch (TimeoutException ex)
            {
                Log.Error("Timeout en GetPublicMatches", ex);
            }
            catch (Exception ex)
            {
                Log.Error("Error General en GetPublicMatches", ex);
            }
            return matchesList.ToArray();
        }

        public async Task<bool> KickPlayerAsync(KickPlayerRequest request)
        {
            bool operationSuccess = false;

            if (request != null && request.RequestorUsername != request.TargetUsername)
            {
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
                                bool isBanned = false;
                                if (target.KickCount >= 3)
                                {
                                    target.IsBanned = true;
                                    isBanned = true;
                                }

                                target.GameIdGame = null;
                                await _repository.SaveChangesAsync();

                                string msg = isBanned ? "Has sido BANEADO." : "Has sido expulsado.";

                                Task.Run(() => NotifyClient(request.TargetUsername, msg));
                                FireAndForgetNotification(game.IdGame, request.TargetUsername, client => client.OnPlayerLeft(request.TargetUsername));

                                operationSuccess = true;
                            }
                        }
                    }
                }
                catch (DbUpdateException ex)
                {
                    Log.Error("Error DB Update en KickPlayer", ex);
                }
                catch (SqlException ex)
                {
                    Log.Fatal("Error SQL en KickPlayer", ex);
                }
                catch (EntityException ex)
                {
                    Log.Error("Error Entity en KickPlayer", ex);
                }
                catch (TimeoutException ex)
                {
                    Log.Error("Timeout en KickPlayer", ex);
                }
                catch (Exception ex)
                {
                    Log.Error("Error General en KickPlayer", ex);
                }
            }

            return operationSuccess;
        }

        private static void NotifyClient(string username, string message)
        {
            var callback = ConnectionManager.GetLobbyClient(username);
            if (callback != null)
            {
                try { callback.OnPlayerKicked(message); }
                catch { ConnectionManager.UnregisterLobbyClient(username); }
            }
        }
    }
}