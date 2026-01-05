using GameServer.DTOs.Gameplay;
using GameServer.DTOs.Lobby;
using GameServer.Helpers;
using GameServer.Models;
using GameServer.Repositories;
using log4net;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Entity.Core;
using System.Data.SqlClient;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;

namespace GameServer.Services.Logic
{
    public class GameplayAppService
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(GameplayAppService));
        private static readonly Random RandomGenerator = new Random();
        private static readonly object _randomLock = new object();
        private static readonly ConcurrentDictionary<int, bool> _processingGames = new ConcurrentDictionary<int, bool>();
        private static readonly ConcurrentDictionary<string, int> _afkStrikes = new ConcurrentDictionary<string, int>();

        private static readonly int[] GooseTiles = { 5, 9, 18, 23, 27, 32, 36, 41, 45, 50, 54, 59 };
        private static readonly int[] LuckyBoxTiles = { 7, 14, 25, 34 };

        private readonly IGameplayRepository _repository;
        private readonly VoteLogic _voteLogic;

        private readonly Func<SanctionAppService> _sanctionServiceFactory;

        private class BoardMoveResult
        {
            public int FinalPosition { get; set; }
            public string Message { get; set; }
            public bool IsExtraTurn { get; set; }
            public int TurnsToSkip { get; set; }
            public string LuckyBoxTag { get; set; }
        }

        public GameplayAppService(IGameplayRepository repository)
        {
            _repository = repository;
            _voteLogic = new VoteLogic(repository);
            _sanctionServiceFactory = () => new SanctionAppService();
        }

        private async Task NotifyTurnUpdate(int gameId)
        {
            try
            {
                var players = await _repository.GetPlayersInGameAsync(gameId);
                foreach (var player in players)
                {
                    var client = ConnectionManager.GetGameplayClient(player.Username);
                    if (client != null)
                    {
                        try
                        {
                            var stateDto = await BuildActiveGameStateAsync(gameId, player.Username);
                            client.OnTurnChanged(stateDto);
                        }
                        catch (CommunicationException ex)
                        {
                            Log.WarnFormat("Communication error notifying turn: {0}", ex.Message);
                            ConnectionManager.UnregisterGameplayClient(player.Username);
                        }
                        catch (TimeoutException ex)
                        {
                            Log.WarnFormat("Timeout notifying turn: {0}", ex.Message);
                            ConnectionManager.UnregisterGameplayClient(player.Username);
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                Log.ErrorFormat("SQL Error in turn broadcast: {0}", ex.Message);
            }
            catch (EntityException ex)
            {
                Log.ErrorFormat("Entity Error in turn broadcast: {0}", ex.Message);
            }
        }

        private async Task NotifyGameFinished(List<Player> players, string winnerUsername)
        {
            await Task.Yield();

            if (players != null && players.Count > 0)
            {
                foreach (var player in players)
                {
                    var client = ConnectionManager.GetGameplayClient(player.Username);
                    if (client != null)
                    {
                        try
                        {
                            client.OnGameFinished(winnerUsername);
                        }
                        catch (CommunicationException ex)
                        {
                            Log.WarnFormat("Communication error notifying game finish: {0}", ex.Message);
                        }
                        catch (TimeoutException ex)
                        {
                            Log.WarnFormat("Timeout notifying game finish: {0}", ex.Message);
                        }
                    }
                }
            }
        }

        public async Task<DiceRollDto> RollDiceAsync(GameplayRequest request)
        {
            DiceRollDto result = null;
            int gameIdForLock = 0;

            if (request != null)
            {
                try
                {
                    var game = await _repository.GetGameByLobbyCodeAsync(request.LobbyCode);
                    if (game != null && game.GameStatus == (int)GameStatus.InProgress)
                    {
                        gameIdForLock = game.IdGame;
                        if (_processingGames.TryAdd(game.IdGame, true))
                        {
                            var players = await _repository.GetPlayersInGameAsync(game.IdGame);
                            var sortedPlayers = players.OrderBy(p => p.IdPlayer).ToList();

                            if (await ValidateTurnAsync(game.IdGame, sortedPlayers, request.Username))
                            {
                                _afkStrikes.TryRemove(request.Username, out _);
                                GameManager.Instance.UpdateActivity(game.IdGame);

                                var player = sortedPlayers.First(p => p.Username == request.Username);

                                if (player.TurnsSkipped > 0)
                                {
                                    result = await HandleSkippedTurnAsync(game.IdGame, player);
                                }
                                else
                                {
                                    result = await ProcessNormalTurnAsync(game, player);
                                }

                                var gameCheck = await _repository.GetGameByIdAsync(game.IdGame);
                                if (gameCheck.GameStatus == (int)GameStatus.InProgress)
                                {
                                    await NotifyTurnUpdate(game.IdGame);
                                }
                            }
                        }
                    }
                }
                catch (SqlException ex)
                {
                    Log.Error("SQL Error in RollDice", ex);
                }
                catch (EntityException ex)
                {
                    Log.Error("Entity Error in RollDice", ex);
                }
                finally
                {
                    if (gameIdForLock != 0) _processingGames.TryRemove(gameIdForLock, out _);
                }
            }

            return result;
        }

        private async Task<bool> ValidateTurnAsync(int gameId, List<Player> sortedPlayers, string requestUsername)
        {
            bool isValid = false;
            if (sortedPlayers.Count > 0)
            {
                int totalMoves = await _repository.GetMoveCountAsync(gameId);
                int extraTurns = await _repository.GetExtraTurnCountAsync(gameId);
                int effectiveTurns = totalMoves - extraTurns;
                int nextPlayerIndex = effectiveTurns % sortedPlayers.Count;

                isValid = (sortedPlayers[nextPlayerIndex].Username == requestUsername);
            }
            return isValid;
        }

        private async Task<DiceRollDto> HandleSkippedTurnAsync(int gameId, Player player)
        {
            player.TurnsSkipped--;
            int turnNum = await _repository.GetMoveCountAsync(gameId) + 1;
            var prevMove = await _repository.GetLastMoveForPlayerAsync(gameId, player.IdPlayer);
            int samePos = prevMove?.FinalPosition ?? 0;

            var skipMove = new MoveRecord
            {
                GameIdGame = gameId,
                PlayerIdPlayer = player.IdPlayer,
                DiceOne = 0,
                DiceTwo = 0,
                TurnNumber = turnNum,
                ActionDescription = string.Format("{0} pierde el turno (Quedan {1}).", player.Username, player.TurnsSkipped),
                StartPosition = samePos,
                FinalPosition = samePos
            };

            _repository.AddMove(skipMove);
            await _repository.SaveChangesAsync();

            return new DiceRollDto { DiceOne = 0, DiceTwo = 0, Total = 0 };
        }

        private async Task<DiceRollDto> ProcessNormalTurnAsync(Game game, Player player)
        {
            var lastMove = await _repository.GetLastMoveForPlayerAsync(game.IdGame, player.IdPlayer);
            int currentPos = lastMove?.FinalPosition ?? 0;

            (int d1, int d2) = GenerateDiceRoll(currentPos);
            int total = d1 + d2;
            int initialCalcPos = currentPos + total;

            BoardMoveResult ruleResult = CalculateBoardRules(initialCalcPos, player);
            DiceRollDto resultDto;

            if (ruleResult.Message == "WIN")
            {
                resultDto = await HandleGameVictoryAsync(game, player, d1, d2, total, currentPos);
            }
            else
            {
                string description = BuildActionDescription(player.Username, d1, d2, ruleResult);
                player.TurnsSkipped = ruleResult.TurnsToSkip;

                int turnNum = await _repository.GetMoveCountAsync(game.IdGame) + 1;

                var move = new MoveRecord
                {
                    GameIdGame = game.IdGame,
                    PlayerIdPlayer = player.IdPlayer,
                    DiceOne = d1,
                    DiceTwo = d2,
                    TurnNumber = turnNum,
                    ActionDescription = description,
                    StartPosition = currentPos,
                    FinalPosition = ruleResult.FinalPosition
                };

                _repository.AddMove(move);
                await _repository.SaveChangesAsync();

                resultDto = new DiceRollDto { DiceOne = d1, DiceTwo = d2, Total = total };
            }

            return resultDto;
        }

        private (int, int) GenerateDiceRoll(int currentPos)
        {
            int d1;
            int d2;
            lock (_randomLock)
            {
                d1 = RandomGenerator.Next(1, 7);
                d2 = (currentPos < 60) ? RandomGenerator.Next(1, 7) : 0;
            }
            return (d1, d2);
        }

        private BoardMoveResult CalculateBoardRules(int initialPos, Player player)
        {
            var result = new BoardMoveResult
            {
                FinalPosition = initialPos,
                IsExtraTurn = false,
                TurnsToSkip = 0,
                Message = string.Empty,
                LuckyBoxTag = string.Empty
            };

            if (result.FinalPosition > 64)
            {
                result.FinalPosition = 64 - (result.FinalPosition - 64);
            }

            if (result.FinalPosition == 64)
            {
                result.Message = "WIN";
            }
            else
            {
                ApplyTileRules(result, player);
            }

            return result;
        }

        private void ApplyTileRules(BoardMoveResult result, Player player)
        {
            if (LuckyBoxTiles.Contains(result.FinalPosition))
            {
                var reward = ProcessLuckyBoxReward(player);
                result.LuckyBoxTag = string.Format("[LUCKYBOX:{0}:{1}_{2}]", player.Username, reward.Type, reward.Amount);
                result.Message = string.Format("¡CAJA DE LA SUERTE! {0}", reward.Description);
            }
            else if (GooseTiles.Contains(result.FinalPosition))
            {
                HandleGooseRule(result);
            }
            else if (result.FinalPosition == 6 || result.FinalPosition == 12)
            {
                HandleBridgeRule(result);
            }
            else
            {
                HandlePenaltyRules(result);
            }
        }

        private void HandleGooseRule(BoardMoveResult result)
        {
            int nextGoose = GooseTiles.FirstOrDefault(t => t > result.FinalPosition);
            if (nextGoose != 0)
            {
                result.Message = string.Format("¡De Oca a Oca ({0} -> {1})! Tira de nuevo.", result.FinalPosition, nextGoose);
                result.FinalPosition = nextGoose;
            }
            else
            {
                result.Message = "¡Oca (59)! Tira de nuevo.";
            }
            result.IsExtraTurn = true;
        }

        private void HandleBridgeRule(BoardMoveResult result)
        {
            if (result.FinalPosition == 6)
            {
                result.Message = "¡De Puente a Puente! Saltas al 12 y tiras de nuevo.";
                result.FinalPosition = 12;
            }
            else
            {
                result.Message = "¡De Puente a Puente! Regresas al 6 y tiras de nuevo.";
                result.FinalPosition = 6;
            }
            result.IsExtraTurn = true;
        }

        private void HandlePenaltyRules(BoardMoveResult result)
        {
            switch (result.FinalPosition)
            {
                case 42:
                    result.Message = "¡Laberinto! Retrocedes a la 30.";
                    result.FinalPosition = 30;
                    break;
                case 58:
                    result.Message = "¡CALAVERA! Regresas al inicio (1).";
                    result.FinalPosition = 1;
                    break;
                case 26:
                case 53:
                    int bonus = result.FinalPosition;
                    result.Message = string.Format("¡Dados! Sumas {0} casillas extra.", bonus);
                    result.FinalPosition += bonus;
                    if (result.FinalPosition > 64) result.FinalPosition = 64 - (result.FinalPosition - 64);
                    break;
                case 19:
                    result.Message = "¡Posada! Pierdes 1 turno.";
                    result.TurnsToSkip = 1;
                    break;
                case 31:
                    result.Message = "¡Pozo! Esperas rescate (2 turnos).";
                    result.TurnsToSkip = 2;
                    break;
                case 56:
                    result.Message = "¡Cárcel! Esperas 3 turnos.";
                    result.TurnsToSkip = 3;
                    break;
            }
        }

        private async Task<DiceRollDto> HandleGameVictoryAsync(Game game, Player player, int d1, int d2, int total, int currentPos)
        {
            int turnNum = await _repository.GetMoveCountAsync(game.IdGame) + 1;
            var move = new MoveRecord
            {
                GameIdGame = game.IdGame,
                PlayerIdPlayer = player.IdPlayer,
                DiceOne = d1,
                DiceTwo = d2,
                TurnNumber = turnNum,
                ActionDescription = string.Format("{0} tiró {1}. ¡HA LLEGADO A LA META!", player.Username, total),
                StartPosition = currentPos,
                FinalPosition = 64
            };
            _repository.AddMove(move);
            await _repository.SaveChangesAsync();

            var playersToNotify = await _repository.GetPlayersInGameAsync(game.IdGame);

            game.GameStatus = (int)GameStatus.Finished;
            game.WinnerIdPlayer = player.IdPlayer;
            await _repository.SaveChangesAsync();

            await NotifyGameFinished(playersToNotify, player.Username);

            await UpdateStatsEndGame(game.IdGame, player.IdPlayer);

            GameManager.Instance.StopMonitoring(game.IdGame);

            _voteLogic.CancelVote(game.IdGame);

            return new DiceRollDto { DiceOne = d1, DiceTwo = d2, Total = total };
        }

        private string BuildActionDescription(string username, int d1, int d2, BoardMoveResult rule)
        {
            string baseMsg = d2 > 0 ? string.Format("{0} tiró {1} y {2}.", username, d1, d2) : string.Format("{0} tiró {1}.", username, d1);
            string fullDescription = string.IsNullOrEmpty(rule.Message)
                ? string.Format("{0} Avanza a {1}.", baseMsg, rule.FinalPosition)
                : string.Format("{0} {1}", baseMsg, rule.Message);

            if (rule.IsExtraTurn) fullDescription = "[EXTRA] " + fullDescription;
            if (!string.IsNullOrEmpty(rule.LuckyBoxTag)) fullDescription = rule.LuckyBoxTag + " " + fullDescription;

            return fullDescription;
        }

        public async Task<GameStateDto> GetGameStateAsync(GameplayRequest request)
        {
            var gameState = new GameStateDto();

            if (request != null)
            {
                try
                {
                    var game = await _repository.GetGameByLobbyCodeAsync(request.LobbyCode);

                    if (game != null)
                    {
                        var player = await _repository.GetPlayerByUsernameAsync(request.Username);

                        if (player != null && player.GameIdGame != game.IdGame)
                        {
                            gameState.IsKicked = true;
                            gameState.IsBanned = player.IsBanned;
                        }
                        else
                        {
                            if (game.GameStatus == (int)GameStatus.Finished)
                            {
                                gameState = await BuildFinishedGameStateAsync(game);
                            }
                            else
                            {
                                gameState = await BuildActiveGameStateAsync(game.IdGame, request.Username);
                            }
                        }
                    }
                }
                catch (SqlException ex)
                {
                    Log.Error("SQL Error obtaining state", ex);
                }
                catch (EntityException ex)
                {
                    Log.Error("Entity Error obtaining state", ex);
                }
                catch (TimeoutException ex)
                {
                    Log.Error("Timeout obtaining state", ex);
                }
            }

            return gameState;
        }

        private async Task<GameStateDto> BuildFinishedGameStateAsync(Game game)
        {
            string winner = "Desconocido";

            if (game.WinnerIdPlayer.HasValue)
            {
                var winnerPlayer = await _repository.GetPlayerByIdAsync(game.WinnerIdPlayer.Value);
                if (winnerPlayer != null) winner = winnerPlayer.Username;
            }
            else
            {
                var winningMove = await _repository.GetWinningMoveAsync(game.IdGame);
                if (winningMove != null)
                {
                    var winnerP = await _repository.GetPlayerByIdAsync(winningMove.PlayerIdPlayer);
                    if (winnerP != null) winner = winnerP.Username;
                }
                else
                {
                    winner = "Nadie";
                }
            }

            return new GameStateDto
            {
                IsGameOver = true,
                WinnerUsername = winner,
                GameLog = new List<string> { "La partida ha finalizado." },
                PlayerPositions = new List<PlayerPositionDto>(),
                CurrentTurnUsername = "",
                IsMyTurn = false
            };
        }

        private async Task<GameStateDto> BuildActiveGameStateAsync(int gameId, string requestUsername)
        {
            var activePlayers = await _repository.GetPlayersInGameAsync(gameId);
            GameStateDto dto = new GameStateDto();

            if (activePlayers.Count > 0)
            {
                var sortedPlayers = activePlayers.OrderBy(p => p.IdPlayer).ToList();
                int totalMoves = await _repository.GetMoveCountAsync(gameId);
                int extraTurns = await _repository.GetExtraTurnCountAsync(gameId);
                int nextPlayerIndex = (totalMoves - extraTurns) % sortedPlayers.Count;
                var currentTurnPlayer = sortedPlayers[nextPlayerIndex];

                var lastMove = await _repository.GetLastGlobalMoveAsync(gameId);
                var logs = await _repository.GetGameLogsAsync(gameId, 20);
                var cleanLogs = logs.Select(l => l.Replace("[EXTRA] ", "")).ToList();

                var playerPositions = await GetPlayerPositionsAsync(gameId, sortedPlayers, currentTurnPlayer.Username);

                dto = new GameStateDto
                {
                    CurrentTurnUsername = currentTurnPlayer.Username,
                    IsMyTurn = (currentTurnPlayer.Username == requestUsername),
                    LastDiceOne = lastMove?.DiceOne ?? 0,
                    LastDiceTwo = lastMove?.DiceTwo ?? 0,
                    GameLog = cleanLogs,
                    PlayerPositions = playerPositions,
                    IsGameOver = false,
                    WinnerUsername = null
                };
            }

            return dto;
        }

        private async Task<List<PlayerPositionDto>> GetPlayerPositionsAsync(int gameId, List<Player> players, string currentTurnUsername)
        {
            var positions = new List<PlayerPositionDto>();
            foreach (var p in players)
            {
                var pLastMove = await _repository.GetLastMoveForPlayerAsync(gameId, p.IdPlayer);
                positions.Add(new PlayerPositionDto
                {
                    Username = p.Username,
                    CurrentTile = pLastMove?.FinalPosition ?? 0,
                    IsOnline = true,
                    AvatarPath = p.Avatar,
                    IsMyTurn = (p.Username == currentTurnUsername)
                });
            }
            return positions;
        }

        public async Task<bool> LeaveGameAsync(GameplayRequest request)
        {
            bool success = false;
            try
            {
                var game = await _repository.GetGameByLobbyCodeAsync(request.LobbyCode);
                if (game != null)
                {
                    var player = await _repository.GetPlayerByUsernameAsync(request.Username);
                    if (player != null)
                    {
                        _voteLogic.CancelVote(game.IdGame);

                        await ProcessLeavingPlayerStats(player);

                        var allPlayers = await _repository.GetPlayersInGameAsync(game.IdGame);
                        var remainingPlayers = allPlayers.Where(p => p.IdPlayer != player.IdPlayer).ToList();

                        if (remainingPlayers.Count < 2)
                        {
                            await FinishGameByAbandonment(game, remainingPlayers);
                        }
                        else
                        {
                            await NotifyTurnUpdate(game.IdGame);
                        }

                        await _repository.SaveChangesAsync();
                        success = true;
                    }
                }
            }
            catch (SqlException ex)
            {
                Log.Error("SQL Error leaving game", ex);
            }
            catch (EntityException ex)
            {
                Log.Error("Entity Error leaving game", ex);
            }

            return success;
        }

        private async Task ProcessLeavingPlayerStats(Player player)
        {
            var playerWithStats = await _repository.GetPlayerWithStatsByIdAsync(player.IdPlayer);
            if (playerWithStats?.PlayerStat != null)
            {
                playerWithStats.PlayerStat.MatchesPlayed++;
                playerWithStats.PlayerStat.MatchesLost++;
            }
            player.GameIdGame = null;
            player.TurnsSkipped = 0;
        }

        private async Task FinishGameByAbandonment(Game game, List<Player> remainingPlayers)
        {
            game.GameStatus = (int)GameStatus.Finished;
            string winnerName = "Nadie";

            if (remainingPlayers.Count == 1)
            {
                var winner = remainingPlayers[0];
                game.WinnerIdPlayer = winner.IdPlayer;
                winnerName = winner.Username;

                var winnerWithStats = await _repository.GetPlayerWithStatsByIdAsync(winner.IdPlayer);
                if (winnerWithStats?.PlayerStat != null)
                {
                    winnerWithStats.PlayerStat.MatchesPlayed++;
                    winnerWithStats.PlayerStat.MatchesWon++;
                    winnerWithStats.TicketCommon += 1;
                    winnerWithStats.Coins += 300;
                }
                Log.InfoFormat("Game {0} finished by abandonment. Winner: {1}", game.LobbyCode, winner.Username);
            }
            else
            {
                game.WinnerIdPlayer = null;
                Log.InfoFormat("Game {0} finished. All players abandoned.", game.LobbyCode);
            }

            await NotifyGameFinished(remainingPlayers, winnerName);

            foreach (var p in remainingPlayers)
            {
                p.GameIdGame = null;
                p.TurnsSkipped = 0;
            }
            await _repository.SaveChangesAsync();

            GameManager.Instance.StopMonitoring(game.IdGame);
        }

        public async Task InitiateVoteKickAsync(VoteRequestDto request)
        {
            await _voteLogic.InitiateVoteAsync(request);
        }

        public async Task CastVoteAsync(VoteResponseDto request)
        {
            await _voteLogic.CastVoteAsync(request);
        }

        private RewardResult ProcessLuckyBoxReward(Player player)
        {
            int roll = RandomGenerator.Next(1, 101);
            RewardResult reward;

            if (roll <= 50)
            {
                int coins = 50;
                player.Coins += coins;
                reward = new RewardResult { Type = "COINS", Amount = coins, Description = string.Format("¡Has encontrado {0} Monedas de Oro!", coins) };
            }
            else if (roll <= 80)
            {
                player.TicketCommon++;
                reward = new RewardResult { Type = "COMMON", Amount = 1, Description = "¡Has desbloqueado un Ticket COMÚN!" };
            }
            else if (roll <= 95)
            {
                player.TicketEpic++;
                reward = new RewardResult { Type = "EPIC", Amount = 1, Description = "¡INCREÍBLE! ¡Ticket ÉPICO obtenido!" };
            }
            else
            {
                player.TicketLegendary++;
                reward = new RewardResult { Type = "LEGENDARY", Amount = 1, Description = "¡JACKPOT! ¡Ticket LEGENDARIO!" };
            }

            return reward;
        }

        public async Task ProcessAfkTimeout(int gameId)
        {
            if (_processingGames.TryAdd(gameId, true))
            {
                try
                {
                    var players = await _repository.GetPlayersInGameAsync(gameId);
                    if (players.Count > 0)
                    {
                        var sortedPlayers = players.OrderBy(p => p.IdPlayer).ToList();
                        int totalMoves = await _repository.GetMoveCountAsync(gameId);
                        int extraTurns = await _repository.GetExtraTurnCountAsync(gameId);

                        int nextPlayerIndex = (totalMoves - extraTurns) % sortedPlayers.Count;
                        var afkPlayer = sortedPlayers[nextPlayerIndex];

                        int strikes = _afkStrikes.AddOrUpdate(afkPlayer.Username, 1, (key, oldValue) => oldValue + 1);

                        if (strikes >= 3)
                        {
                            await HandleMaxAfkStrikes(gameId, afkPlayer);
                        }
                        else
                        {
                            await HandleAfkWarning(gameId, afkPlayer, strikes, totalMoves);
                        }

                        await NotifyTurnUpdate(gameId);
                    }
                }
                catch (SqlException ex)
                {
                    Log.ErrorFormat("SQL Error processing AFK for game {0}: {1}", gameId, ex.Message);
                }
                catch (EntityException ex)
                {
                    Log.ErrorFormat("Entity Error processing AFK for game {0}: {1}", gameId, ex.Message);
                }
                finally
                {
                    _processingGames.TryRemove(gameId, out _);
                }
            }
        }

        private async Task HandleMaxAfkStrikes(int gameId, Player afkPlayer)
        {
            _afkStrikes.TryRemove(afkPlayer.Username, out _);
            _voteLogic.CancelVote(gameId);

            var game = await _repository.GetGameByIdAsync(gameId);
            string lobbyCode = game?.LobbyCode ?? "UNKNOWN";

            var sanctionService = _sanctionServiceFactory();
            await sanctionService.ProcessKickAsync(afkPlayer.Username, lobbyCode, "Inactividad prolongada (AFK)", "SYSTEM_AFK");

            GameManager.Instance.UpdateActivity(gameId);
        }

        private async Task HandleAfkWarning(int gameId, Player afkPlayer, int strikes, int totalMoves)
        {
            var lastMove = await _repository.GetLastMoveForPlayerAsync(gameId, afkPlayer.IdPlayer);
            int samePos = lastMove?.FinalPosition ?? 0;

            var skipMove = new MoveRecord
            {
                GameIdGame = gameId,
                PlayerIdPlayer = afkPlayer.IdPlayer,
                DiceOne = 0,
                DiceTwo = 0,
                TurnNumber = totalMoves + 1,
                ActionDescription = string.Format("{0} tardó demasiado. Pierde turno ({1}/3).", afkPlayer.Username, strikes),
                StartPosition = samePos,
                FinalPosition = samePos
            };

            _repository.AddMove(skipMove);
            await _repository.SaveChangesAsync();

            GameManager.Instance.UpdateActivity(gameId);
        }

        private async Task UpdateStatsEndGame(int gameId, int winnerId)
        {
            var players = await _repository.GetPlayersWithStatsInGameAsync(gameId);
            foreach (var p in players)
            {
                if (p.PlayerStat != null)
                {
                    p.PlayerStat.MatchesPlayed++;
                    if (p.IdPlayer == winnerId)
                    {
                        p.PlayerStat.MatchesWon++;
                        p.TicketCommon += 1;
                        p.Coins += 300;
                    }
                    else
                    {
                        p.PlayerStat.MatchesLost++;
                    }
                }
                p.GameIdGame = null;
                p.TurnsSkipped = 0;
            }
            await _repository.SaveChangesAsync();
        }
    }
}