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
    public class LobbyAppService
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(LobbyAppService));
        private readonly ILobbyRepository _repository;

        public LobbyAppService(ILobbyRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        private async Task NotifyAllInLobby(int gameId, string excludeUsername, Action<ILobbyServiceCallback> notificationAction)
        {
            try
            {
                var players = await _repository.GetPlayersInGameAsync(gameId);
                foreach (var player in players)
                {
                    if (player.Username == excludeUsername) continue;

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
                    }
                }
            }
            catch (Exception ex)
            {
                Log.WarnFormat("Error enviando notificación push al lobby {0}: {1}", gameId, ex.Message);
            }
        }

        private async Task CleanPlayerStateIfNeeded(Player player)
        {
            if (player.GameIdGame != null)
            {
                var oldGame = await _repository.GetGameByIdAsync(player.GameIdGame.Value);
                if (oldGame == null || oldGame.GameStatus == (int)GameStatus.Finished)
                {
                    player.GameIdGame = null;
                    await _repository.SaveChangesAsync();
                    Log.InfoFormat("Limpieza automática: Jugador {0} liberado de partida fantasma/terminada.", player.Username);
                }
            }
        }

        public async Task<LobbyCreationResultDto> CreateLobbyAsync(CreateLobbyRequest request)
        {
            if (request == null || request.Settings == null)
            {
                return new LobbyCreationResultDto
                {
                    Success = false,
                    ErrorMessage = "Datos inválidos.",
                    ErrorType = LobbyErrorType.InvalidData
                };
            }

            if (request.Settings.MaxPlayers < 2 || request.Settings.MaxPlayers > 4)
            {
                return new LobbyCreationResultDto
                {
                    Success = false,
                    ErrorMessage = "El número de jugadores debe ser entre 2 y 4.",
                    ErrorType = LobbyErrorType.InvalidData
                };
            }

            LobbyCreationResultDto result = new LobbyCreationResultDto { Success = false };

            try
            {
                var hostPlayer = await _repository.GetPlayerByUsernameAsync(request.HostUsername);
                if (hostPlayer == null)
                {
                    result.ErrorMessage = "Jugador anfitrión no encontrado.";
                    result.ErrorType = LobbyErrorType.UserNotFound;
                }
                else
                {
                    if (hostPlayer.IsGuest)
                    {
                        result.ErrorMessage = "Los invitados no pueden crear partidas.";
                        result.ErrorType = LobbyErrorType.GuestNotAllowed;
                        return result;
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
            }
            catch (DbUpdateException ex)
            {
                Log.Error("Error de integridad referencial al crear lobby.", ex);
                result.ErrorMessage = "Error de base de datos.";
                result.ErrorType = LobbyErrorType.DatabaseError;
            }
            catch (SqlException ex)
            {
                Log.Fatal("Error SQL crítico al crear lobby.", ex);
                result.ErrorMessage = "Error de conexión.";
                result.ErrorType = LobbyErrorType.DatabaseError;
            }
            catch (EntityException ex)
            {
                Log.Error("Error de Entity Framework al crear lobby.", ex);
                result.ErrorMessage = "Error interno.";
                result.ErrorType = LobbyErrorType.DatabaseError;
            }
            catch (TimeoutException ex)
            {
                Log.Error("Tiempo de espera agotado al crear lobby.", ex);
                result.ErrorMessage = "El servidor tardó demasiado en responder.";
                result.ErrorType = LobbyErrorType.ServerTimeout;
            }
            catch (Exception ex)
            {
                Log.Error("Error inesperado al crear lobby.", ex);
                result.ErrorMessage = "Error inesperado.";
                result.ErrorType = LobbyErrorType.Unknown;
            }

            return result;
        }

        public async Task<JoinLobbyResultDto> JoinLobbyAsync(JoinLobbyRequest request)
        {
            JoinLobbyResultDto result = new JoinLobbyResultDto { Success = false };
            try
            {
                var player = await _repository.GetPlayerByUsernameAsync(request.Username);
                if (player == null)
                {
                    result.ErrorMessage = "Jugador no encontrado.";
                    result.ErrorType = LobbyErrorType.UserNotFound;
                    return result;
                }

                await CleanPlayerStateIfNeeded(player);

                if (player.GameIdGame != null)
                {
                    result.ErrorMessage = "Ya estás en otra partida activa.";
                    result.ErrorType = LobbyErrorType.PlayerAlreadyInGame;
                    return result;
                }

                var game = await _repository.GetGameByCodeAsync(request.LobbyCode);
                if (game == null)
                {
                    result.ErrorMessage = "Código de partida no encontrado.";
                    result.ErrorType = LobbyErrorType.GameNotFound;
                    return result;
                }

                if (game.GameStatus != (int)GameStatus.WaitingForPlayers)
                {
                    result.ErrorMessage = "La partida ya ha comenzado.";
                    result.ErrorType = LobbyErrorType.GameStarted;
                    return result;
                }

                var playersInLobby = await _repository.GetPlayersInGameAsync(game.IdGame);
                if (playersInLobby.Count >= game.MaxPlayers)
                {
                    result.ErrorMessage = "La partida está llena.";
                    result.ErrorType = LobbyErrorType.GameFull;
                    return result;
                }

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
                await NotifyAllInLobby(game.IdGame, request.Username, client => client.OnPlayerJoined(newPlayerDto));
            }
            catch (DbUpdateException ex)
            {
                Log.Error("Error al actualizar estado del jugador al unirse.", ex);
                result.ErrorMessage = "Error al unirse a la partida.";
                result.ErrorType = LobbyErrorType.DatabaseError;
            }
            catch (SqlException ex)
            {
                Log.Fatal("Error SQL al unirse al lobby.", ex);
                result.ErrorMessage = "Error de conexión.";
                result.ErrorType = LobbyErrorType.DatabaseError;
            }
            catch (EntityException ex)
            {
                Log.Error("Error de entidad al unirse al lobby.", ex);
                result.ErrorMessage = "Error interno.";
                result.ErrorType = LobbyErrorType.DatabaseError;
            }
            catch (TimeoutException ex)
            {
                Log.Error("Timeout al unirse al lobby.", ex);
                result.ErrorMessage = "Tiempo de espera agotado.";
                result.ErrorType = LobbyErrorType.ServerTimeout;
            }
            catch (Exception ex)
            {
                Log.Error("Error inesperado al unirse al lobby.", ex);
                result.ErrorMessage = "Error inesperado.";
                result.ErrorType = LobbyErrorType.Unknown;
            }

            return result;
        }

        public async Task<LobbyStateDto> GetLobbyStateAsync(string lobbyCode)
        {
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

                    return new LobbyStateDto
                    {
                        IsGameStarted = (game.GameStatus == (int)GameStatus.InProgress),
                        Players = playerDtos,
                        BoardId = game.Board_idBoard,
                        MaxPlayers = game.MaxPlayers,
                        IsPublic = game.IsPublic
                    };
                }
            }
            catch (Exception ex)
            {
                Log.Error("Error obteniendo estado del lobby.", ex);
            }
            return null;
        }

        public async Task<bool> StartGameAsync(string lobbyCode)
        {
            try
            {
                var game = await _repository.GetGameByCodeAsync(lobbyCode);
                if (game == null) return false;

                var players = await _repository.GetPlayersInGameAsync(game.IdGame);
                if (players.Count < 2)
                {
                    Log.WarnFormat("Intento de iniciar juego {0} con insuficientes jugadores.", lobbyCode);
                    return false;
                }

                game.GameStatus = (int)GameStatus.InProgress;
                await _repository.SaveChangesAsync();

                GameManager.Instance.StartMonitoring(game.IdGame);
                Log.InfoFormat("Juego {0} iniciado.", lobbyCode);

                await NotifyAllInLobby(game.IdGame, null, client => client.OnGameStarted());
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("Error iniciando juego.", ex);
                return false;
            }
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
                        await NotifyAllInLobby(gameId, hostUsername, client => client.OnLobbyDisbanded());
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
            catch (Exception ex)
            {
                Log.Error("Error disolviendo lobby.", ex);
            }
        }

        public async Task<bool> LeaveLobbyAsync(string username)
        {
            try
            {
                var player = await _repository.GetPlayerByUsernameAsync(username);
                if (player == null || player.GameIdGame == null) return false;

                int gameId = player.GameIdGame.Value;
                player.GameIdGame = null;
                await _repository.SaveChangesAsync();

                ConnectionManager.UnregisterLobbyClient(username);
                Log.InfoFormat("Jugador {0} salió del lobby.", username);

                bool gameClosed = await HandleGameShutdownIfNeeded(gameId);
                if (!gameClosed)
                {
                    await NotifyAllInLobby(gameId, username, client => client.OnPlayerLeft(username));
                }
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("Error saliendo del lobby.", ex);
                return false;
            }
        }

        private async Task<bool> HandleGameShutdownIfNeeded(int gameId)
        {
            var game = await _repository.GetGameByIdAsync(gameId);
            if (game == null) return true;

            var remainingPlayers = await _repository.GetPlayersInGameAsync(gameId);

            if (game.GameStatus == (int)GameStatus.InProgress && remainingPlayers.Count < 2)
            {
                game.GameStatus = (int)GameStatus.Finished;
                GameManager.Instance.StopMonitoring(gameId);
                await _repository.SaveChangesAsync();
                return true;
            }
            else if (game.GameStatus == (int)GameStatus.WaitingForPlayers && remainingPlayers.Count == 0)
            {
                _repository.DeleteGameAndCleanDependencies(game);
                await _repository.SaveChangesAsync();
                return true;
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

        public async Task<ActiveMatchDto[]> GetPublicMatchesAsync()
        {
            try
            {
                var games = await _repository.GetActivePublicGamesAsync();
                var matchesList = new List<ActiveMatchDto>();

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
                return matchesList.ToArray();
            }
            catch (Exception ex)
            {
                Log.Error("Error obteniendo partidas públicas.", ex);
                return new ActiveMatchDto[0];
            }
        }

        public async Task<bool> KickPlayerAsync(KickPlayerRequest request)
        {
            if (request == null) return false;
            if (request.RequestorUsername == request.TargetUsername) return false;

            try
            {
                var game = await _repository.GetGameByCodeAsync(request.LobbyCode);
                if (game == null) return false;

                var host = await _repository.GetPlayerByUsernameAsync(request.RequestorUsername);
                if (host == null || game.HostPlayerID != host.IdPlayer) return false;

                var target = await _repository.GetPlayerByUsernameAsync(request.TargetUsername);
                if (target == null || target.GameIdGame != game.IdGame) return false;

                target.KickCount++;
                bool isBanned = target.KickCount >= 3;

                if (isBanned)
                {
                    target.IsBanned = true;
                    Log.WarnFormat("Jugador {0} BANEADO.", target.Username);
                }

                target.GameIdGame = null;
                await _repository.SaveChangesAsync();

                string msg = isBanned ? "Has sido BANEADO." : "Has sido expulsado.";
                NotifyClient(request.TargetUsername, msg);
                await NotifyAllInLobby(game.IdGame, request.TargetUsername, client => client.OnPlayerLeft(request.TargetUsername));

                return true;
            }
            catch (Exception ex)
            {
                Log.Error("Error al expulsar jugador.", ex);
                return false;
            }
        }

        private static void NotifyClient(string username, string message)
        {
            var callback = ConnectionManager.GetLobbyClient(username);
            if (callback != null)
            {
                try
                {
                    callback.OnPlayerKicked(message);
                }
                catch (Exception)
                {
                    ConnectionManager.UnregisterLobbyClient(username);
                }
            }
        }

    }
}