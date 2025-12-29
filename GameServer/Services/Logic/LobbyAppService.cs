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
        private readonly LobbyRepository _repository;

        public LobbyAppService(LobbyRepository repository)
        {
            _repository = repository;
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
            LobbyCreationResultDto result = new LobbyCreationResultDto { Success = false };
            try
            {
                if (request == null || request.Settings == null)
                {
                    result.ErrorMessage = "Datos inválidos.";
                    return result;
                }

                var hostPlayer = await _repository.GetPlayerByUsernameAsync(request.HostUsername);
                if (hostPlayer == null)
                {
                    result.ErrorMessage = "Jugador anfitrión no encontrado.";
                }
                else
                {
                    if (hostPlayer.IsGuest)
                    {
                        result.ErrorMessage = "Los invitados no pueden crear partidas.";
                        return result;
                    }

                    await CleanPlayerStateIfNeeded(hostPlayer);

                    if (hostPlayer.GameIdGame != null)
                    {
                        result.ErrorMessage = "Ya estás en una partida activa.";
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
                }
            }
            catch (DbUpdateException ex)
            {
                Log.Error("Error de integridad referencial al crear lobby.", ex);
                result.ErrorMessage = "Error de base de datos.";
            }
            catch (SqlException ex)
            {
                Log.Fatal("Error SQL crítico al crear lobby.", ex);
                result.ErrorMessage = "Error de conexión.";
            }
            catch (EntityException ex)
            {
                Log.Error("Error de Entity Framework al crear lobby.", ex);
                result.ErrorMessage = "Error interno.";
            }
            catch (TimeoutException ex)
            {
                Log.Error("Tiempo de espera agotado al crear lobby.", ex);
                result.ErrorMessage = "El servidor tardó demasiado en responder.";
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
                    return result;
                }

                await CleanPlayerStateIfNeeded(player);

                if (player.GameIdGame != null)
                {
                    result.ErrorMessage = "Ya estás en otra partida activa.";
                    return result;
                }

                var game = await _repository.GetGameByCodeAsync(request.LobbyCode);
                if (game == null)
                {
                    result.ErrorMessage = "Código de partida no encontrado.";
                    return result;
                }

                if (game.GameStatus != (int)GameStatus.WaitingForPlayers)
                {
                    result.ErrorMessage = "La partida ya ha comenzado.";
                    return result;
                }

                var playersInLobby = await _repository.GetPlayersInGameAsync(game.IdGame);
                if (playersInLobby.Count >= game.MaxPlayers)
                {
                    result.ErrorMessage = "La partida está llena.";
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

                var newPlayerDto = new PlayerLobbyDto { Username = request.Username, IsHost = false };
                await NotifyAllInLobby(game.IdGame, request.Username, client => client.OnPlayerJoined(newPlayerDto));
            }
            catch (DbUpdateException ex)
            {
                Log.Error("Error al actualizar estado del jugador al unirse.", ex);
                result.ErrorMessage = "Error al unirse a la partida.";
            }
            catch (SqlException ex)
            {
                Log.Fatal("Error SQL al unirse al lobby.", ex);
                result.ErrorMessage = "Error de conexión.";
            }
            catch (EntityException ex)
            {
                Log.Error("Error de entidad al unirse al lobby.", ex);
                result.ErrorMessage = "Error interno.";
            }
            catch (TimeoutException ex)
            {
                Log.Error("Timeout al unirse al lobby.", ex);
                result.ErrorMessage = "Tiempo de espera agotado.";
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
                Log.Error("Error SQL obteniendo estado del lobby.", ex);
            }
            catch (EntityException ex)
            {
                Log.Error("Error EF obteniendo estado del lobby.", ex);
            }
            catch (TimeoutException ex)
            {
                Log.Error("Timeout obteniendo estado del lobby.", ex);
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

                    if (players.Count < 2)
                    {
                        Log.WarnFormat("Intento de iniciar juego {0} con solo {1} jugadores.", lobbyCode, players.Count);
                        return false;
                    }

                    game.GameStatus = (int)GameStatus.InProgress;
                    await _repository.SaveChangesAsync();
                    Log.InfoFormat("Juego {0} iniciado con {1} jugadores.", lobbyCode, players.Count);
                    success = true;

                    await NotifyAllInLobby(game.IdGame, null, client => client.OnGameStarted());
                }
            }
            catch (DbUpdateException ex)
            {
                Log.Error("Error actualizando estado de juego a Iniciado.", ex);
            }
            catch (SqlException ex)
            {
                Log.Error("Error SQL iniciando juego.", ex);
            }
            catch (EntityException ex)
            {
                Log.Error("Error EF iniciando juego.", ex);
            }
            catch (TimeoutException ex)
            {
                Log.Error("Timeout iniciando juego.", ex);
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
                        await NotifyAllInLobby(gameId, hostUsername, client => client.OnLobbyDisbanded());

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
                Log.Error("Error DB al disolver lobby.", ex);
            }
            catch (SqlException ex)
            {
                Log.Error("Error SQL al disolver lobby.", ex);
            }
            catch (EntityException ex)
            {
                Log.Error("Error EF al disolver lobby.", ex);
            }
            catch (TimeoutException ex)
            {
                Log.Error("Timeout al disolver lobby.", ex);
            }
        }

        public async Task<bool> LeaveLobbyAsync(string username)
        {
            bool success = false;
            try
            {
                var player = await _repository.GetPlayerByUsernameAsync(username);
                if (player == null || player.GameIdGame == null)
                {
                    return false;
                }

                int gameId = player.GameIdGame.Value;

                player.GameIdGame = null;
                await _repository.SaveChangesAsync();

                Log.InfoFormat("Jugador {0} abandonó el lobby.", username);
                success = true;

                ConnectionManager.UnregisterLobbyClient(username);

                bool gameClosed = await HandleGameShutdownIfNeeded(gameId);

                if (!gameClosed)
                {
                    await NotifyAllInLobby(gameId, username, client => client.OnPlayerLeft(username));
                }
            }
            catch (DbUpdateException ex)
            {
                Log.Error("Error DB al abandonar lobby.", ex);
            }
            catch (SqlException ex)
            {
                Log.Error("Error SQL al abandonar lobby.", ex);
            }
            catch (EntityException ex)
            {
                Log.Error("Error EF al abandonar lobby.", ex);
            }
            catch (TimeoutException ex)
            {
                Log.Error("Timeout al abandonar lobby.", ex);
            }

            return success;
        }

        private async Task<bool> HandleGameShutdownIfNeeded(int gameId)
        {
            var game = await _repository.GetGameByIdAsync(gameId);
            if (game == null) return true;

            var remainingPlayers = await _repository.GetPlayersInGameAsync(gameId);

            if (game.GameStatus == (int)GameStatus.InProgress && remainingPlayers.Count < 2)
            {
                game.GameStatus = (int)GameStatus.Finished;
                string winnerName = remainingPlayers.Count > 0 ? remainingPlayers[0].Username : "Nadie";
                Log.InfoFormat("Juego terminado por abandono desde Lobby. Ganador: {0}", winnerName);
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
                Log.Error("Error SQL obteniendo partidas públicas.", ex);
            }
            catch (EntityException ex)
            {
                Log.Error("Error EF obteniendo partidas públicas.", ex);
            }
            catch (TimeoutException ex)
            {
                Log.Error("Timeout obteniendo partidas públicas.", ex);
            }
            return matchesList.ToArray();
        }

        public async Task KickPlayerAsync(KickPlayerRequest request)
        {
            if (request == null) return;

            try
            {
                var game = await _repository.GetGameByCodeAsync(request.LobbyCode);
                if (game == null)
                {
                    Log.Warn("KickPlayer: Lobby no encontrado.");
                    return;
                }

                var host = await _repository.GetPlayerByUsernameAsync(request.RequestorUsername);
                if (host == null || game.HostPlayerID != host.IdPlayer)
                {
                    Log.WarnFormat("KickPlayer: {0} intentó expulsar sin ser host.", request.RequestorUsername);
                    return;
                }

                var target = await _repository.GetPlayerByUsernameAsync(request.TargetUsername);
                if (target == null || target.GameIdGame != game.IdGame)
                {
                    Log.WarnFormat("KickPlayer: Jugador objetivo {0} no está en el lobby.", request.TargetUsername);
                    return;
                }

                target.GameIdGame = null;
                await _repository.SaveChangesAsync();

                NotifyClient(request.TargetUsername, "Has sido expulsado por el anfitrión.");
                await NotifyAllInLobby(game.IdGame, request.TargetUsername, client => client.OnPlayerLeft(request.TargetUsername));

                Log.InfoFormat("Jugador {0} expulsado del lobby {1} por {2}.", request.TargetUsername, request.LobbyCode, request.RequestorUsername);
            }
            catch (DbUpdateException ex)
            {
                Log.Error("Error DB al expulsar jugador.", ex);
            }
            catch (SqlException ex)
            {
                Log.Error("Error SQL al expulsar jugador.", ex);
            }
            catch (EntityException ex)
            {
                Log.Error("Error EF al expulsar jugador.", ex);
            }
            catch (TimeoutException ex)
            {
                Log.Error("Timeout al expulsar jugador.", ex);
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
                catch (CommunicationException ex)
                {
                    Log.WarnFormat("Error de comunicación notificando a {0}: {1}", username, ex.Message);
                }
                catch (TimeoutException ex)
                {
                    Log.WarnFormat("Timeout notificando a {0}: {1}", username, ex.Message);
                }
                finally
                {
                    ConnectionManager.UnregisterLobbyClient(username);
                }
            }
        }
    }
}