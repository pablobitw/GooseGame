using GameServer.DTOs.Lobby;
using GameServer.Repositories;
using log4net;
using System;
using System.Collections.Generic;
using System.Data.Entity.Infrastructure;
using System.Data.SqlClient;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

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
                    if (hostPlayer.GameIdGame != null)
                    {
                        hostPlayer.GameIdGame = null;
                        await _repository.SaveChangesAsync();
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
            catch (SqlException ex)
            {
                Log.Error("Error SQL al crear lobby.", ex);
                result.ErrorMessage = "Error de base de datos.";
            }
            catch (DbUpdateException ex)
            {
                Log.Error("Error DB al crear lobby.", ex);
                result.ErrorMessage = "Error al guardar el lobby.";
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

                if (player.GameIdGame != null)
                {
                    result.ErrorMessage = "Ya estás en otra partida.";
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
            catch (SqlException ex)
            {
                Log.Error("Error SQL al unirse al lobby.", ex);
                result.ErrorMessage = "Error de base de datos.";
            }
            catch (DbUpdateException ex)
            {
                Log.Error("Error DB al unirse al lobby.", ex);
                result.ErrorMessage = "Error al unirse.";
            }

            return result;
        }

        public async Task<LobbyStateDTO> GetLobbyStateAsync(string lobbyCode)
        {
            LobbyStateDTO result = new LobbyStateDTO { IsGameStarted = true, Players = new List<PlayerLobbyDTO>() };
            try
            {
                var game = await _repository.GetGameByCodeAsync(lobbyCode);

                if (game != null && game.GameStatus == (int)GameStatus.WaitingForPlayers)
                {
                    var players = await _repository.GetPlayersInGameAsync(game.IdGame);
                    var playerDtos = players.Select(p => new PlayerLobbyDTO
                    {
                        Username = p.Username,
                        IsHost = (p.IdPlayer == game.HostPlayerID)
                    }).ToList();

                    result = new LobbyStateDTO
                    {
                        IsGameStarted = false,
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
                    game.GameStatus = (int)GameStatus.InProgress;
                    await _repository.SaveChangesAsync();
                    Log.InfoFormat("Juego {0} iniciado.", lobbyCode);
                    success = true;
                }
            }
            catch (SqlException ex)
            {
                Log.Error("Error SQL iniciando juego.", ex);
            }
            catch (DbUpdateException ex)
            {
                Log.Error("Error DB iniciando juego.", ex);
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
            catch (SqlException ex)
            {
                Log.Error("Error SQL disolviendo lobby.", ex);
            }
            catch (DbUpdateException ex)
            {
                Log.Error("Error DB disolviendo lobby.", ex);
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
                    player.GameIdGame = null;
                    await _repository.SaveChangesAsync();
                    Log.InfoFormat("Jugador {0} abandonó el lobby.", username);
                    success = true;
                }
            }
            catch (SqlException ex)
            {
                Log.Error("Error SQL abandonando lobby.", ex);
            }
            catch (DbUpdateException ex)
            {
                Log.Error("Error DB abandonando lobby.", ex);
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
    }
}