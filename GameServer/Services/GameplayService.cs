using GameServer.Contracts;
using log4net;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;

namespace GameServer.Services
{
    public class GameplayService : IGameplayService
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(GameplayService));
        private static readonly Random RandomGenerator = new Random();

        public async Task<DiceRollDTO> RollDiceAsync(string lobbyCode, string username)
        {
            try
            {
                using (var context = new GameDatabase_Container())
                {
                   

                    var game = await context.Games.FirstOrDefaultAsync(g => g.LobbyCode == lobbyCode);
                    if (game == null) return null;

                    var player = await context.Players.FirstOrDefaultAsync(p => p.Username == username);

                    int d1 = RandomGenerator.Next(1, 7);
                    int d2 = RandomGenerator.Next(1, 7);

                    var move = new MoveRecord
                    {
                        GameIdGame = game.IdGame,
                        PlayerIdPlayer = player.IdPlayer,
                        DiceOne = d1,
                        DiceTwo = d2,
                        TurnNumber = context.MoveRecords.Count(m => m.GameIdGame == game.IdGame) + 1,
                        ActionDescription = $"{username} tiró {d1} y {d2} (Total: {d1 + d2})",
                        StartPosition = 0, 
                        FinalPosition = 0 
                    };

                    context.MoveRecords.Add(move);
                    await context.SaveChangesAsync();

                    return new DiceRollDTO { DiceOne = d1, DiceTwo = d2, Total = d1 + d2 };
                }
            }
            catch (Exception ex)
            {
                Log.Error("Error en RollDiceAsync", ex);
                return new DiceRollDTO(); 
            }
        }

        public async Task<GameStateDTO> GetGameStateAsync(string lobbyCode, string requestingUsername)
        {
            try
            {
                using (var context = new GameDatabase_Container())
                {
                    var game = await context.Games.FirstOrDefaultAsync(g => g.LobbyCode == lobbyCode);
                    if (game == null) return new GameStateDTO();

                    var players = await context.Players
                        .Where(p => p.GameIdGame == game.IdGame)
                        .OrderBy(p => p.IdPlayer)
                        .ToListAsync();

                    if (players.Count == 0) return new GameStateDTO();

                    int totalMoves = await context.MoveRecords.CountAsync(m => m.GameIdGame == game.IdGame);

                    int nextPlayerIndex = totalMoves % players.Count;
                    var currentTurnPlayer = players[nextPlayerIndex];

                    var lastMove = await context.MoveRecords
                        .Where(m => m.GameIdGame == game.IdGame)
                        .OrderByDescending(m => m.IdMoveRecord)
                        .FirstOrDefaultAsync();

                    var logs = await context.MoveRecords
                        .Where(m => m.GameIdGame == game.IdGame)
                        .OrderByDescending(m => m.IdMoveRecord)
                        .Take(20)
                        .Select(m => m.ActionDescription)
                        .ToListAsync();

                    return new GameStateDTO
                    {
                        CurrentTurnUsername = currentTurnPlayer.Username,
                        IsMyTurn = (currentTurnPlayer.Username == requestingUsername),
                        LastDiceOne = lastMove?.DiceOne ?? 0,
                        LastDiceTwo = lastMove?.DiceTwo ?? 0,
                        GameLog = logs
                    };
                }
            }
            catch (Exception ex) // 
            {
                Log.Error("Error en GetGameStateAsync", ex);
                return new GameStateDTO();
            }
        }
    }
}