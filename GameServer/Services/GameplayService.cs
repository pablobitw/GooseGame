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

        private static readonly int[] GooseTiles = { 5, 9, 14, 18, 23, 27, 32, 36, 41, 45, 50, 54, 59 };

        public async Task<DiceRollDTO> RollDiceAsync(string lobbyCode, string username)
        {
            try
            {
                using (var context = new GameDatabase_Container())
                {
                    var game = await context.Games.FirstOrDefaultAsync(g => g.LobbyCode == lobbyCode);
                    if (game == null) return null;

                    var player = await context.Players.FirstOrDefaultAsync(p => p.Username == username);

                    var lastMove = await context.MoveRecords
                        .Where(m => m.PlayerIdPlayer == player.IdPlayer && m.GameIdGame == game.IdGame)
                        .OrderByDescending(m => m.IdMoveRecord)
                        .FirstOrDefaultAsync();

                    int currentPos = lastMove?.FinalPosition ?? 0;

                    int d1 = RandomGenerator.Next(1, 7);
                    int d2 = 0;

                    if (currentPos < 60)
                    {
                        d2 = RandomGenerator.Next(1, 7);
                    }

                    int total = d1 + d2;
                    int finalPos = currentPos + total;
                    string message = "";
                    bool isExtraTurn = false;

                    if (finalPos > 64)
                    {
                        int excess = finalPos - 64;
                        finalPos = 64 - excess;
                    }

                    if (finalPos == 64)
                    {
                        message = "¡HA LLEGADO A LA META!";
                        game.GameStatus = (int)GameStatus.Finished;
                    }
                    else if (GooseTiles.Contains(finalPos))
                    {
                        int nextGoose = GooseTiles.FirstOrDefault(t => t > finalPos);
                        if (nextGoose != 0)
                        {
                            message = $"¡De Oca a Oca ({finalPos} -> {nextGoose})! Tira de nuevo.";
                            finalPos = nextGoose;
                        }
                        else
                        {
                            message = "¡Oca (59)! Tira de nuevo.";
                        }
                        isExtraTurn = true;
                    }
                    else if (finalPos == 6 || finalPos == 12)
                    {
                        message = "¡Puente! Saltas a la Posada (19).";
                        finalPos = 19;
                    }
                    else if (finalPos == 26 || finalPos == 53)
                    {
                        int bonus = finalPos;
                        message = $"¡Dados! Sumas {bonus} casillas extra.";
                        finalPos += bonus;

                        if (finalPos > 64)
                        {
                            int excess = finalPos - 64;
                            finalPos = 64 - excess;
                        }
                    }
                    else if (finalPos == 42)
                    {
                        message = "¡Laberinto! Retrocedes a la 30.";
                        finalPos = 30;
                    }
                    else if (finalPos == 58)
                    {
                        message = "¡CALAVERA! Regresas al inicio (1).";
                        finalPos = 1;
                    }
                    else if (finalPos == 19) message = "¡Posada! Pierdes turno.";
                    else if (finalPos == 31) message = "¡Pozo! Esperas rescate.";
                    else if (finalPos == 56) message = "¡Cárcel! Esperas turno.";


                    string baseMsg = d2 > 0
                        ? $"{username} tiró {d1} y {d2}."
                        : $"{username} tiró {d1}.";

                    string fullDescription = string.IsNullOrEmpty(message)
                        ? $"{baseMsg} Avanza a {finalPos}."
                        : $"{baseMsg} {message}";

                    if (isExtraTurn) fullDescription = "[EXTRA] " + fullDescription;

                    var move = new MoveRecord
                    {
                        GameIdGame = game.IdGame,
                        PlayerIdPlayer = player.IdPlayer,
                        DiceOne = d1,
                        DiceTwo = d2,
                        TurnNumber = context.MoveRecords.Count(m => m.GameIdGame == game.IdGame) + 1,
                        ActionDescription = fullDescription,
                        StartPosition = currentPos,
                        FinalPosition = finalPos
                    };

                    context.MoveRecords.Add(move);
                    await context.SaveChangesAsync();

                    if (finalPos == 64 && game.GameStatus == (int)GameStatus.Finished)
                    {
                        await context.SaveChangesAsync();
                    }

                    return new DiceRollDTO { DiceOne = d1, DiceTwo = d2, Total = total };
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

                    int extraTurns = await context.MoveRecords
                        .CountAsync(m => m.GameIdGame == game.IdGame && m.ActionDescription.Contains("[EXTRA]"));

                    int effectiveTurns = totalMoves - extraTurns;
                    int nextPlayerIndex = effectiveTurns % players.Count;
                    var currentTurnPlayer = players[nextPlayerIndex];

                    var lastMove = await context.MoveRecords
                        .Where(m => m.GameIdGame == game.IdGame)
                        .OrderByDescending(m => m.IdMoveRecord)
                        .FirstOrDefaultAsync();

                    var logs = await context.MoveRecords
                        .Where(m => m.GameIdGame == game.IdGame)
                        .OrderByDescending(m => m.IdMoveRecord)
                        .Take(20)
                        .Select(m => m.ActionDescription.Replace("[EXTRA] ", ""))
                        .ToListAsync();

                    var playerPositions = new List<PlayerPositionDTO>();
                    foreach (var p in players)
                    {
                        var pLastMove = await context.MoveRecords
                            .Where(m => m.PlayerIdPlayer == p.IdPlayer && m.GameIdGame == game.IdGame)
                            .OrderByDescending(m => m.IdMoveRecord)
                            .FirstOrDefaultAsync();

                        playerPositions.Add(new PlayerPositionDTO
                        {
                            Username = p.Username,
                            CurrentTile = pLastMove?.FinalPosition ?? 0,
                            IsOnline = true,
                            AvatarPath = p.Avatar,
                            IsMyTurn = (p.Username == currentTurnPlayer.Username)
                        });
                    }

                    bool isGameOver = game.GameStatus == (int)GameStatus.Finished;
                    string winner = null;

                    if (isGameOver)
                    {
                        var winningMove = await context.MoveRecords
                            .Where(m => m.GameIdGame == game.IdGame && m.FinalPosition == 64)
                            .OrderByDescending(m => m.IdMoveRecord)
                            .FirstOrDefaultAsync();

                        if (winningMove != null)
                        {
                            var winnerP = players.FirstOrDefault(p => p.IdPlayer == winningMove.PlayerIdPlayer);
                            winner = winnerP?.Username ?? "Desconocido";
                        }
                    }

                    return new GameStateDTO
                    {
                        CurrentTurnUsername = currentTurnPlayer.Username,
                        IsMyTurn = (currentTurnPlayer.Username == requestingUsername),
                        LastDiceOne = lastMove?.DiceOne ?? 0,
                        LastDiceTwo = lastMove?.DiceTwo ?? 0,
                        GameLog = logs,
                        PlayerPositions = playerPositions,
                        IsGameOver = isGameOver,
                        WinnerUsername = winner
                    };
                }
            }
            catch (Exception ex)
            {
                Log.Error("Error en GetGameStateAsync", ex);
                return new GameStateDTO();
            }
        }
    }
}