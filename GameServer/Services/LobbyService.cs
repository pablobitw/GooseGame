using GameServer.Contracts;
using log4net;
using System;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;

namespace GameServer.Services
{
    public class LobbyService : ILobbyService
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(LobbyService));
        private static readonly Random RandomGenerator = new Random();

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

                    if (hostPlayer.GameIdGame != 0) 
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

        private string GenerateLobbyCode(GameDatabase_Container context)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            string code;
            do
            {
                code = new string(Enumerable.Repeat(chars, 5)
                    .Select(s => s[RandomGenerator.Next(s.Length)]).ToArray());
            }
            while (context.Games.Any(g => g.LobbyCode == code && g.GameStatus != (int)GameStatus.Finished));

            return code;
        }
    }
}