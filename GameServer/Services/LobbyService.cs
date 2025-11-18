using GameServer; 
using GameServer.Contracts;
using log4net;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace GameServer.Services
{
    public class LobbyService : ILobbyService
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(LobbyService));

        public async Task<LobbyCreationResultDTO> CreateLobbyAsync(LobbySettingsDTO settings, string hostUsername)
        {
            try
            {
                using (var context = new GameDatabase_Container())
                {
                    var hostPlayer = await context.Players.FirstOrDefaultAsync(p => p.Username == hostUsername);
                    if (hostPlayer == null)
                    {
                        return new LobbyCreationResultDTO { Success = false, ErrorMessage = "Host player not found." };
                    }

                    if (hostPlayer.GameIdGame != null)
                    {
                        return new LobbyCreationResultDTO { Success = false, ErrorMessage = "Player is already in a game." };
                    }

                    string newLobbyCode = GenerateLobbyCode(context);

                    var newGame = new Game
                    {
                        GameStatus = (int)GameStatus.WaitingForPlayers,
                        HostPlayerID = hostPlayer.IdPlayer,
                        Board_idBoard = settings.BoardId,
                        IsPublic = settings.IsPublic,
                        MaxPlayers = settings.MaxPlayers,
                        LobbyCode = newLobbyCode,
                        StartTime = DateTime.Now
                    };

                    context.Games.Add(newGame);

                    await context.SaveChangesAsync();

                    hostPlayer.GameIdGame = newGame.IdGame;

                    await context.SaveChangesAsync();

                    Log.InfoFormat("Lobby created by '{0}'. Code: {1}", hostUsername, newLobbyCode);

                    return new LobbyCreationResultDTO { Success = true, LobbyCode = newLobbyCode };
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error creating lobby for {hostUsername}", ex);
                return new LobbyCreationResultDTO { Success = false, ErrorMessage = ex.Message };
            }
        }

        public async Task<JoinLobbyResultDTO> JoinLobbyAsync(string lobbyCode, string joiningUsername)
        {
            try
            {
                using (var context = new GameDatabase_Container())
                {
                    var player = await context.Players.FirstOrDefaultAsync(p => p.Username == joiningUsername);
                    if (player == null)
                    {
                        return new JoinLobbyResultDTO { Success = false, ErrorMessage = "Jugador no encontrado." };
                    }
                    if (player.GameIdGame != null)
                    {
                        return new JoinLobbyResultDTO { Success = false, ErrorMessage = "Ya estás en otra partida." };
                    }

                    var game = await context.Games.FirstOrDefaultAsync(g => g.LobbyCode == lobbyCode);
                    if (game == null)
                    {
                        return new JoinLobbyResultDTO { Success = false, ErrorMessage = "Código de partida no encontrado." };
                    }
                    if (game.GameStatus != (int)GameStatus.WaitingForPlayers)
                    {
                        return new JoinLobbyResultDTO { Success = false, ErrorMessage = "La partida ya ha comenzado." };
                    }

                    var playersInLobby = await context.Players
                        .Where(p => p.GameIdGame == game.IdGame)
                        .ToListAsync();

                    if (playersInLobby.Count >= game.MaxPlayers)
                    {
                        return new JoinLobbyResultDTO { Success = false, ErrorMessage = "La partida está llena." };
                    }

                    player.GameIdGame = game.IdGame;
                    await context.SaveChangesAsync();

                    Log.InfoFormat("El jugador '{0}' se ha unido al lobby {1}", joiningUsername, lobbyCode);

                    var dtoList = playersInLobby.Select(p => new PlayerLobbyDTO
                    {
                        Username = p.Username,
                        IsHost = (p.IdPlayer == game.HostPlayerID)
                    }).ToList();

                    dtoList.Add(new PlayerLobbyDTO { Username = joiningUsername, IsHost = false });

                    return new JoinLobbyResultDTO
                    {
                        Success = true,
                        BoardId = game.Board_idBoard,
                        MaxPlayers = game.MaxPlayers,
                        IsHost = false,
                        PlayersInLobby = dtoList
                    };
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error en JoinLobbyAsync para {joiningUsername}", ex);
                return new JoinLobbyResultDTO { Success = false, ErrorMessage = "Error del servidor." };
            }
        }

        public async Task<LobbyStateDTO> GetLobbyStateAsync(string lobbyCode)
        {
            try
            {
                using (var context = new GameDatabase_Container())
                {
                    var game = await context.Games.FirstOrDefaultAsync(g => g.LobbyCode == lobbyCode);
                    if (game == null)
                    {
                        return new LobbyStateDTO { IsGameStarted = true, Players = new List<PlayerLobbyDTO>() };
                    }

                    if (game.GameStatus != (int)GameStatus.WaitingForPlayers)
                    {
                        return new LobbyStateDTO { IsGameStarted = true, Players = new List<PlayerLobbyDTO>() };
                    }

                    var players = await context.Players
                        .Where(p => p.GameIdGame == game.IdGame)
                        .Select(p => new PlayerLobbyDTO
                        {
                            Username = p.Username,
                            IsHost = (p.IdPlayer == game.HostPlayerID)
                        })
                        .ToListAsync();

                    return new LobbyStateDTO { IsGameStarted = false, Players = players };
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error en GetLobbyStateAsync para {lobbyCode}", ex);
                return new LobbyStateDTO { IsGameStarted = true, Players = new List<PlayerLobbyDTO>() };
            }
        }
        public async Task<bool> StartGameAsync(string lobbyCode)
        {
            try
            {
                using (var context = new GameDatabase_Container())
                {
                    var game = await context.Games.FirstOrDefaultAsync(g => g.LobbyCode == lobbyCode);
                    if (game == null)
                    {
                        Log.WarnFormat("StartGameAsync: Lobby code {0} not found.", lobbyCode);
                        return false;
                    }

                    game.GameStatus = (int)GameStatus.InProgress;
                    await context.SaveChangesAsync();

                    Log.InfoFormat("Game {0} has started.", lobbyCode);

                    return true;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error starting game {lobbyCode}", ex);
                return false;
            }
        }

        public async Task DisbandLobbyAsync(string hostUsername)
        {
            try
            {
                using (var context = new GameDatabase_Container())
                {
                    var hostPlayer = await context.Players.FirstOrDefaultAsync(p => p.Username == hostUsername);

                    if (hostPlayer == null || hostPlayer.GameIdGame == null)
                    {
                        Log.WarnFormat("DisbandLobbyAsync: El host {0} no fue encontrado o no estaba en un lobby.", hostUsername);
                        return;
                    }

                    int gameIdToDisband = hostPlayer.GameIdGame.Value;

                    var gameToDisband = await context.Games.FindAsync(gameIdToDisband);

                    if (gameToDisband == null)
                    {
                        Log.ErrorFormat("DisbandLobbyAsync: El jugador {0} apuntaba al Game ID {1} pero no se encontró. Limpiando jugador.", hostUsername, gameIdToDisband);
                        hostPlayer.GameIdGame = null;
                        await context.SaveChangesAsync();
                        return;
                    }

                    var playersInLobby = await context.Players
                        .Where(p => p.GameIdGame == gameIdToDisband)
                        .ToListAsync();

                    foreach (var player in playersInLobby)
                    {
                        player.GameIdGame = null;
                    }

                    var moveRecords = context.MoveRecords.Where(m => m.GameIdGame == gameIdToDisband);
                    context.MoveRecords.RemoveRange(moveRecords);

                   
                    var sanctions = context.Sanctions.Where(s => s.Game_IdGame == gameIdToDisband);
                    context.Sanctions.RemoveRange(sanctions);

                    
                    context.Games.Remove(gameToDisband);

                    await context.SaveChangesAsync();
                    Log.InfoFormat("Lobby {0} (Host: {1}) fue disuelto y limpiado.", gameToDisband.LobbyCode, hostUsername);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error en DisbandLobbyAsync para {hostUsername}", ex);
            }
        }

        private string GenerateLobbyCode(GameDatabase_Container context)
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
            while (context.Games.Any(g => g.LobbyCode == code && g.GameStatus != (int)GameStatus.Finished));

            return code;
        }
    }
}