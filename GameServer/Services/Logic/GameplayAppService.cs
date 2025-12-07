using GameServer.DTOs.Gameplay;
using GameServer.Repositories;
using log4net;
using System;
using System.Collections.Generic;
using System.Data.Entity.Infrastructure;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;

namespace GameServer.Services.Logic
{
    public class GameplayAppService
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(GameplayAppService));
        private static readonly Random RandomGenerator = new Random();
        private static readonly int[] GooseTiles = { 5, 9, 14, 18, 23, 27, 32, 36, 41, 45, 50, 54, 59 };
        private readonly GameplayRepository _repository;

        public GameplayAppService(GameplayRepository repository)
        {
            _repository = repository;
        }

        public async Task<DiceRollDTO> RollDiceAsync(GameplayRequest request)
        {
            DiceRollDTO result = null;
            try
            {
                if (request != null)
                {
                    var game = await _repository.GetGameByLobbyCodeAsync(request.LobbyCode);
                    if (game != null)
                    {
                        var player = await _repository.GetPlayerByUsernameAsync(request.Username);
                        if (player != null)
                        {
                            var lastMove = await _repository.GetLastMoveForPlayerAsync(game.IdGame, player.IdPlayer);
                            int currentPos = lastMove?.FinalPosition ?? 0;

                            int d1 = RandomGenerator.Next(1, 7);
                            int d2 = (currentPos < 60) ? RandomGenerator.Next(1, 7) : 0;
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
                            else if (finalPos == 6 || finalPos == 12) { message = "¡Puente! Saltas a la Posada (19)."; finalPos = 19; }
                            else if (finalPos == 42) { message = "¡Laberinto! Retrocedes a la 30."; finalPos = 30; }
                            else if (finalPos == 58) { message = "¡CALAVERA! Regresas al inicio (1)."; finalPos = 1; }
                            else if (finalPos == 26 || finalPos == 53)
                            {
                                int bonus = finalPos;
                                message = $"¡Dados! Sumas {bonus} casillas extra.";
                                finalPos += bonus;
                                if (finalPos > 64) finalPos = 64 - (finalPos - 64);
                            }
                            else if (finalPos == 19) message = "¡Posada! Pierdes turno.";
                            else if (finalPos == 31) message = "¡Pozo! Esperas rescate.";
                            else if (finalPos == 56) message = "¡Cárcel! Esperas turno.";

                            string baseMsg = d2 > 0 ? $"{request.Username} tiró {d1} y {d2}." : $"{request.Username} tiró {d1}.";
                            string fullDescription = string.IsNullOrEmpty(message) ? $"{baseMsg} Avanza a {finalPos}." : $"{baseMsg} {message}";
                            if (isExtraTurn) fullDescription = "[EXTRA] " + fullDescription;

                            int turnNum = await _repository.GetMoveCountAsync(game.IdGame) + 1;

                            var move = new MoveRecord
                            {
                                GameIdGame = game.IdGame,
                                PlayerIdPlayer = player.IdPlayer,
                                DiceOne = d1,
                                DiceTwo = d2,
                                TurnNumber = turnNum,
                                ActionDescription = fullDescription,
                                StartPosition = currentPos,
                                FinalPosition = finalPos
                            };

                            _repository.AddMove(move);
                            await _repository.SaveChangesAsync();

                            if (game.GameStatus == (int)GameStatus.Finished)
                            {
                                await _repository.SaveChangesAsync();
                            }

                            result = new DiceRollDTO { DiceOne = d1, DiceTwo = d2, Total = total };
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                Log.Error("Error SQL en RollDice.", ex);
            }
            catch (DbUpdateException ex)
            {
                Log.Error("Error DB en RollDice.", ex);
            }

            return result;
        }

        public async Task<GameStateDTO> GetGameStateAsync(GameplayRequest request)
        {
            GameStateDTO state = new GameStateDTO();
            try
            {
                if (request != null)
                {
                    var game = await _repository.GetGameByLobbyCodeAsync(request.LobbyCode);
                    if (game != null)
                    {
                        var players = await _repository.GetPlayersInGameAsync(game.IdGame);
                        if (players.Count > 0)
                        {
                            int totalMoves = await _repository.GetMoveCountAsync(game.IdGame);
                            int extraTurns = await _repository.GetExtraTurnCountAsync(game.IdGame);

                            int effectiveTurns = totalMoves - extraTurns;
                            int nextPlayerIndex = effectiveTurns % players.Count;
                            var currentTurnPlayer = players[nextPlayerIndex];

                            var lastMove = await _repository.GetLastGlobalMoveAsync(game.IdGame);
                            var logs = await _repository.GetGameLogsAsync(game.IdGame, 20);

                            var cleanLogs = logs.Select(l => l.Replace("[EXTRA] ", "")).ToList();

                            var playerPositions = new List<PlayerPositionDTO>();
                            foreach (var p in players)
                            {
                                var pLastMove = await _repository.GetLastMoveForPlayerAsync(game.IdGame, p.IdPlayer);
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
                                var winningMove = await _repository.GetWinningMoveAsync(game.IdGame);
                                if (winningMove != null)
                                {
                                    var winnerP = players.FirstOrDefault(p => p.IdPlayer == winningMove.PlayerIdPlayer);
                                    winner = winnerP?.Username ?? "Desconocido";
                                }
                            }

                            state = new GameStateDTO
                            {
                                CurrentTurnUsername = currentTurnPlayer.Username,
                                IsMyTurn = (currentTurnPlayer.Username == request.Username),
                                LastDiceOne = lastMove?.DiceOne ?? 0,
                                LastDiceTwo = lastMove?.DiceTwo ?? 0,
                                GameLog = cleanLogs,
                                PlayerPositions = playerPositions,
                                IsGameOver = isGameOver,
                                WinnerUsername = winner
                            };
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                Log.Error("Error SQL obteniendo estado de juego.", ex);
            }
            catch (Exception ex) 
            {
                Log.Error("Error en lógica de estado de juego.", ex);
            }

            return state;
        }
    }
}