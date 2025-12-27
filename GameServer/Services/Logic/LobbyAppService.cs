using GameServer.DTOs.Lobby;
using GameServer.Helpers;
using GameServer.Repositories;
using GameServer.Interfaces;  
using log4net;
using System;
using System.Collections.Generic;
using System.Data.Entity.Infrastructure;
using System.Data.SqlClient;
using System.Linq;
using System.Security.Cryptography;
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

        public async Task<LobbyCreationResultDTO> CreateLobbyAsync(CreateLobbyRequest request)
        {
            LobbyCreationResultDTO result = new LobbyCreationResultDTO { Success = false };
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
                        StartTime = DateTime.Now
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
            catch (Exception ex)
            {
                Log.Error("Error al crear lobby.", ex);
                result.ErrorMessage = "Error interno del servidor.";
            }

            return result;
        }

        public async Task<JoinLobbyResultDTO> JoinLobbyAsync(JoinLobbyRequest request)
        {
            JoinLobbyResultDTO result = new JoinLobbyResultDTO { Success = false };
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

                var dtoList = playersInLobby.Select(p => new PlayerLobbyDTO
                {
                    Username = p.Username,
                    IsHost = (p.IdPlayer == game.HostPlayerID)
                }).ToList();

                dtoList.Add(new PlayerLobbyDTO { Username = request.Username, IsHost = false });

                result.Success = true;
                result.BoardId = game.Board_idBoard;
                result.MaxPlayers = game.MaxPlayers;
                result.IsHost = false;
                result.IsPublic = game.IsPublic;
                result.PlayersInLobby = dtoList;
            }
            catch (Exception ex)
            {
                Log.Error("Error al unirse al lobby.", ex);
                result.ErrorMessage = "Error interno al unirse.";
            }

            return result;
        }

        public async Task<LobbyStateDTO> GetLobbyStateAsync(string lobbyCode)
        {
            LobbyStateDTO result = null;

            try
            {
                var game = await _repository.GetGameByCodeAsync(lobbyCode);

                if (game != null)
                {
                    var players = await _repository.GetPlayersInGameAsync(game.IdGame);
                    var playerDtos = players.Select(p => new PlayerLobbyDTO
                    {
                        Username = p.Username,
                        IsHost = (p.IdPlayer == game.HostPlayerID)
                    }).ToList();

                    if (game.GameStatus == (int)GameStatus.WaitingForPlayers)
                    {
                        result = new LobbyStateDTO
                        {
                            IsGameStarted = false,
                            Players = playerDtos,
                            BoardId = game.Board_idBoard,
                            MaxPlayers = game.MaxPlayers,
                            IsPublic = game.IsPublic
                        };
                    }
                    else if (game.GameStatus == (int)GameStatus.InProgress)
                    {
                        result = new LobbyStateDTO
                        {
                            IsGameStarted = true,
                            Players = playerDtos,
                            BoardId = game.Board_idBoard,
                            MaxPlayers = game.MaxPlayers,
                            IsPublic = game.IsPublic
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("Error obteniendo estado del lobby.", ex);
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
                }
            }
            catch (Exception ex)
            {
                Log.Error("Error iniciando juego.", ex);
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
            bool success = false;
            try
            {
                var player = await _repository.GetPlayerByUsernameAsync(username);
                if (player != null && player.GameIdGame != null)
                {
                    int gameId = player.GameIdGame.Value;

                    player.GameIdGame = null;
                    await _repository.SaveChangesAsync();

                    Log.InfoFormat("Jugador {0} abandonó el lobby.", username);
                    success = true;

                    ConnectionManager.UnregisterLobbyClient(username);

                    var game = await _repository.GetGameByIdAsync(gameId);
                    if (game != null)
                    {
                        var remainingPlayers = await _repository.GetPlayersInGameAsync(gameId);

                        if (game.GameStatus == (int)GameStatus.InProgress && remainingPlayers.Count < 2)
                        {
                            game.GameStatus = (int)GameStatus.Finished;
                            string winnerName = remainingPlayers.Count > 0 ? remainingPlayers[0].Username : "Nadie";
                            Log.InfoFormat("Juego terminado por abandono desde Lobby. Ganador: {0}", winnerName);
                            await _repository.SaveChangesAsync();
                        }
                        else if (game.GameStatus == (int)GameStatus.WaitingForPlayers && remainingPlayers.Count == 0)
                        {
                            _repository.DeleteGameAndCleanDependencies(game);
                            await _repository.SaveChangesAsync();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("Error abandonando lobby.", ex);
            }

            return success;
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

        public async Task<ActiveMatchDTO[]> GetPublicMatchesAsync()
        {
            var matchesList = new List<ActiveMatchDTO>();
            try
            {
                var games = await _repository.GetActivePublicGamesAsync();
                foreach (var game in games)
                {
                    int currentCount = await _repository.CountPlayersInGameAsync(game.IdGame);
                    if (currentCount < game.MaxPlayers)
                    {
                        string hostName = await _repository.GetUsernameByIdAsync(game.HostPlayerID);
                        matchesList.Add(new ActiveMatchDTO
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
                Log.Error("Error obteniendo partidas públicas.", ex);
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
                    Log.Warn($"KickPlayer: {request.RequestorUsername} intentó expulsar sin ser host.");
                    return;
                }

                var target = await _repository.GetPlayerByUsernameAsync(request.TargetUsername);
                if (target == null || target.GameIdGame != game.IdGame)
                {
                    Log.Warn($"KickPlayer: Jugador objetivo {request.TargetUsername} no está en el lobby.");
                    return;
                }

                target.GameIdGame = null;
                await _repository.SaveChangesAsync();

                NotifyKickedPlayer(request.TargetUsername);

                Log.Info($"Jugador {request.TargetUsername} expulsado del lobby {request.LobbyCode} por {request.RequestorUsername}.");
            }
            catch (Exception ex)
            {
                Log.Error($"Error crítico al expulsar jugador {request.TargetUsername}", ex);
            }
        }

        private void NotifyKickedPlayer(string username)
        {
            var callback = ConnectionManager.GetLobbyClient(username);
            if (callback != null)
            {
                try
                {
                    callback.OnPlayerKicked("Has sido expulsado por el anfitrión.");
                }
                catch (System.ServiceModel.CommunicationException)
                {
                }
                finally
                {
                    ConnectionManager.UnregisterLobbyClient(username);
                }
            }
        }
    }
}