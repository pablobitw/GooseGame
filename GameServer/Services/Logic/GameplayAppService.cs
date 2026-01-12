using GameServer.DTOs.Gameplay;
using GameServer.DTOs.Lobby;
using GameServer.Faults;
using GameServer.GameEngines;
using GameServer.Helpers;
using GameServer.Interfaces;
using GameServer.Models;
using GameServer.Repositories;
using GameServer.Services.Common;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GameServer.Services.Logic
{
    public class GameplayAppService : IGameplayService
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(GameplayAppService));

        private readonly IGameplayRepository _repository;
        private readonly IGameplayRepositoryFactory _repoFactory;
        private readonly IGameplayConnectionManager _connectionManager;
        private readonly IGameplayStateManager _stateManager;
        private readonly IGameMonitor _gameMonitor;
        private readonly IVoteLogic _voteLogic;
        private readonly ISanctionFactory _sanctionFactory;
        private readonly GooseBoardEngine _gameEngine;

        public GameplayAppService(
            IGameplayRepository repository = null,
            IGameplayRepositoryFactory repoFactory = null,
            IGameplayConnectionManager connectionManager = null,
            IGameplayStateManager stateManager = null,
            IGameMonitor gameMonitor = null,
            IVoteLogic voteLogic = null,
            ISanctionFactory sanctionFactory = null)
        {
            _repository = repository ?? new GameplayRepository();
            _repoFactory = repoFactory ?? new GameplayRepositoryFactory();
            _connectionManager = connectionManager ?? new GameplayConnectionManagerWrapper();
            _stateManager = stateManager ?? new GameplayStateManager();
            _gameMonitor = gameMonitor ?? new GameMonitorWrapper();
            _sanctionFactory = sanctionFactory ?? new SanctionFactory();

            _voteLogic = voteLogic ?? new VoteLogic(_repository, _sanctionFactory, _connectionManager);

            _gameEngine = new GooseBoardEngine();
        }

        private void NotifyTurnUpdateSafe(int gameId)
        {
            Task.Run(async () =>
            {
                try
                {
                    List<string> usernames;
                    using (var repo = _repoFactory.Create())
                    {
                        var players = await repo.GetPlayersInGameAsync(gameId);
                        usernames = players.Select(p => p.Username).ToList();
                    }

                    var tasks = usernames.Select(username => Task.Run(async () =>
                    {
                        var client = _connectionManager.GetGameplayClient(username);
                        if (client != null)
                        {
                            try
                            {
                                GameStateDto stateDto;
                                using (var repo = _repoFactory.Create())
                                {
                                    var logic = new GameplayAppService(
                                        repo,
                                        _repoFactory,
                                        _connectionManager,
                                        _stateManager,
                                        _gameMonitor,
                                        new VoteLogic(repo, _sanctionFactory, _connectionManager),
                                        _sanctionFactory
                                    );
                                    stateDto = await logic.BuildActiveGameStateAsync(gameId, username);
                                }
                                stateDto.Success = true;
                                client.OnTurnChanged(stateDto);
                            }
                            catch (Exception ex)
                            {
                                Log.Warn($"Error notificando turno a {username}: {ex.Message}");
                            }
                        }
                    }));

                    await Task.WhenAll(tasks);
                }
                catch (Exception ex)
                {
                    Log.Error($"Error general en NotifyTurnUpdateSafe {gameId}: {ex.Message}");
                }
            });
        }

        public async Task<DiceRollDto> RollDiceAsync(GameplayRequest request)
        {
            var result = new DiceRollDto { Success = false };
            int gameIdForLock = 0;

            if (request == null)
            {
                result.ErrorType = GameplayErrorType.Unknown;
                result.ErrorMessage = "Solicitud vacía.";
                return result;
            }

            try
            {
                var game = await _repository.GetGameByLobbyCodeAsync(request.LobbyCode);
                if (game == null)
                {
                    result.ErrorType = GameplayErrorType.GameNotFound;
                    result.ErrorMessage = "La partida no existe.";
                    return result;
                }

                if (game.GameStatus != (int)GameStatus.InProgress)
                {
                    result.ErrorType = GameplayErrorType.GameFinished;
                    result.ErrorMessage = "La partida no está en progreso.";
                    return result;
                }

                gameIdForLock = game.IdGame;

                if (!_stateManager.TryAddProcessingGame(game.IdGame))
                {
                    result.ErrorType = GameplayErrorType.Timeout;
                    result.ErrorMessage = "Procesando turno anterior...";
                    return result;
                }

                result = await ProcessGameTurn(game, request.Username);
            }
            catch (Exception ex)
            {
                Log.Error("Error in RollDice", ex);
                throw ExceptionManager.Map(ex);
            }
            finally
            {
                if (gameIdForLock != 0) _stateManager.RemoveProcessingGame(gameIdForLock);
            }

            return result;
        }

        private async Task<DiceRollDto> ProcessGameTurn(Game game, string username)
        {
            var players = await _repository.GetPlayersInGameAsync(game.IdGame);
            var sortedPlayers = players.OrderBy(p => p.IdPlayer).ToList();

            if (!await ValidateTurnAsync(game.IdGame, sortedPlayers, username))
            {
                return new DiceRollDto { Success = false, ErrorType = GameplayErrorType.NotYourTurn, ErrorMessage = "No es tu turno." };
            }

            _stateManager.RemoveAfkStrike(username);
            _gameMonitor.UpdateActivity(game.IdGame);

            var player = sortedPlayers.First(p => p.Username == username);
            DiceRollDto result;

            if (player.TurnsSkipped > 0)
                result = await HandleSkippedTurnAsync(game.IdGame, player);
            else
                result = await ProcessNormalTurnAsync(game, player);

            result.Success = true;
            result.ErrorType = GameplayErrorType.None;

            NotifyTurnUpdateSafe(game.IdGame);

            return result;
        }

        private async Task<DiceRollDto> ProcessNormalTurnAsync(Game game, Player player)
        {
            var lastMove = await _repository.GetLastMoveForPlayerAsync(game.IdGame, player.IdPlayer);
            int currentPos = lastMove?.FinalPosition ?? 0;

            var (d1, d2) = _gameEngine.GenerateDiceRoll(currentPos);
            int total = d1 + d2;
            int initialCalcPos = currentPos + total;

            int pCoins = player.Coins;
            int pCommon = player.TicketCommon;
            int pEpic = player.TicketEpic;
            int pLegend = player.TicketLegendary;

            var ruleResult = _gameEngine.CalculateBoardRules(initialCalcPos, player.Username, ref pCoins, ref pCommon, ref pEpic, ref pLegend);

            player.Coins = pCoins;
            player.TicketCommon = pCommon;
            player.TicketEpic = pEpic;
            player.TicketLegendary = pLegend;

            DiceRollDto resultDto;

            if (ruleResult.Message == "WIN")
            {
                resultDto = await HandleGameVictoryAsync(game, player, d1, d2, total, currentPos);
            }
            else
            {
                string description = _gameEngine.BuildActionDescription(player.Username, d1, d2, ruleResult);
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

        public async Task<GameStateDto> GetGameStateAsync(GameplayRequest request)
        {
            var gameState = new GameStateDto { Success = false };
            if (request == null) return gameState;

            try
            {
                var game = await _repository.GetGameByLobbyCodeAsync(request.LobbyCode);
                if (game == null)
                {
                    gameState.ErrorType = GameplayErrorType.GameNotFound;
                    return gameState;
                }

                var player = await _repository.GetPlayerByUsernameAsync(request.Username);
                if (player != null && player.GameIdGame != game.IdGame)
                {
                    gameState.Success = true;
                    gameState.IsKicked = true;
                    gameState.ErrorType = GameplayErrorType.PlayerKicked;
                    return gameState;
                }

                if (game.GameStatus == (int)GameStatus.Finished)
                    gameState = await BuildFinishedGameStateAsync(game);
                else
                    gameState = await BuildActiveGameStateAsync(game.IdGame, request.Username);

                gameState.Success = true;
                gameState.ErrorType = GameplayErrorType.None;
            }
            catch (Exception ex)
            {
                Log.Error("Error GetGameState", ex);
                throw ExceptionManager.Map(ex);
            }

            return gameState;
        }

        public async Task<bool> LeaveGameAsync(GameplayRequest request)
        {
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
                            await FinishGameByAbandonment(game, remainingPlayers);
                        else
                            NotifyTurnUpdateSafe(game.IdGame);

                        await _repository.SaveChangesAsync();
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("Error leaving game", ex);
                throw ExceptionManager.Map(ex);
            }
            return false;
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
                var winnerStats = await _repository.GetPlayerWithStatsByIdAsync(winner.IdPlayer);
                if (winnerStats?.PlayerStat != null)
                {
                    winnerStats.PlayerStat.MatchesPlayed++;
                    winnerStats.PlayerStat.MatchesWon++;
                    winnerStats.Coins += 300;
                }
            }
            else
            {
                game.WinnerIdPlayer = null;
            }

            NotifyGameFinishedSafe(remainingPlayers.Select(p => p.Username).ToList(), winnerName);

            foreach (var p in remainingPlayers) { p.GameIdGame = null; p.TurnsSkipped = 0; }
            await _repository.SaveChangesAsync();
            _gameMonitor.StopMonitoring(game.IdGame);
        }

        private void NotifyGameFinishedSafe(List<string> usernames, string winner)
        {
            Task.Run(() =>
            {
                foreach (var u in usernames)
                {
                    var c = _connectionManager.GetGameplayClient(u);
                    if (c != null) try { c.OnGameFinished(winner); } catch { }
                }
            });
        }

        private async Task<bool> ValidateTurnAsync(int gameId, List<Player> sortedPlayers, string requestUsername)
        {
            if (sortedPlayers.Count == 0) return false;
            int totalMoves = await _repository.GetMoveCountAsync(gameId);
            int extraTurns = await _repository.GetExtraTurnCountAsync(gameId);
            int effectiveTurns = totalMoves - extraTurns;
            int nextPlayerIndex = effectiveTurns % sortedPlayers.Count;
            return sortedPlayers[nextPlayerIndex].Username == requestUsername;
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
                ActionDescription = $"{player.Username} pierde el turno.",
                StartPosition = samePos,
                FinalPosition = samePos
            };
            _repository.AddMove(skipMove);
            await _repository.SaveChangesAsync();
            return new DiceRollDto { DiceOne = 0, DiceTwo = 0, Total = 0 };
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
                ActionDescription = $"{player.Username} llegó a la meta!",
                StartPosition = currentPos,
                FinalPosition = 64
            };
            _repository.AddMove(move);
            await _repository.SaveChangesAsync();

            var playersToNotify = await _repository.GetPlayersInGameAsync(game.IdGame);
            game.GameStatus = (int)GameStatus.Finished;
            game.WinnerIdPlayer = player.IdPlayer;
            await _repository.SaveChangesAsync();

            NotifyGameFinishedSafe(playersToNotify.Select(p => p.Username).ToList(), player.Username);
            await UpdateStatsEndGame(game.IdGame, player.IdPlayer);
            _gameMonitor.StopMonitoring(game.IdGame);
            _voteLogic.CancelVote(game.IdGame);

            return new DiceRollDto { DiceOne = d1, DiceTwo = d2, Total = total };
        }

        private Task<GameStateDto> BuildFinishedGameStateAsync(Game game)
        {
            return Task.FromResult(new GameStateDto
            {
                Success = true,
                IsGameOver = true,
                WinnerUsername = "Winner"
            });
        }

        private async Task<GameStateDto> BuildActiveGameStateAsync(int gameId, string requestUsername)
        {
            var activePlayers = await _repository.GetPlayersInGameAsync(gameId);
            var sortedPlayers = activePlayers.OrderBy(p => p.IdPlayer).ToList();
            int totalMoves = await _repository.GetMoveCountAsync(gameId);
            int extraTurns = await _repository.GetExtraTurnCountAsync(gameId);
            int nextPlayerIndex = (totalMoves - extraTurns) % sortedPlayers.Count;
            var currentTurnPlayer = sortedPlayers[nextPlayerIndex];

            var lastMove = await _repository.GetLastGlobalMoveAsync(gameId);
            var logs = await _repository.GetGameLogsAsync(gameId, 20);
            var positions = await GetPlayerPositionsAsync(gameId, sortedPlayers, currentTurnPlayer.Username);

            return new GameStateDto
            {
                CurrentTurnUsername = currentTurnPlayer.Username,
                IsMyTurn = (currentTurnPlayer.Username == requestUsername),
                LastDiceOne = lastMove?.DiceOne ?? 0,
                LastDiceTwo = lastMove?.DiceTwo ?? 0,
                GameLog = logs.Select(l => l.Replace("[EXTRA] ", "")).ToList(),
                PlayerPositions = positions,
                IsGameOver = false
            };
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

        private async Task UpdateStatsEndGame(int gameId, int winnerId)
        {
            var players = await _repository.GetPlayersWithStatsInGameAsync(gameId);
            foreach (var p in players)
            {
                if (p.IdPlayer == winnerId)
                {
                    p.Coins += 300;
                    if (p.PlayerStat != null)
                    {
                        p.PlayerStat.MatchesWon++;
                        p.PlayerStat.MatchesPlayed++;
                    }
                }
                else
                {
                    if (p.PlayerStat != null)
                    {
                        p.PlayerStat.MatchesLost++;
                        p.PlayerStat.MatchesPlayed++;
                    }
                }

                p.GameIdGame = null;
                p.TurnsSkipped = 0;
            }
            await _repository.SaveChangesAsync();
        }

        public async Task InitiateVoteKickAsync(VoteRequestDto request) => await _voteLogic.InitiateVoteAsync(request);
        public async Task CastVoteAsync(VoteResponseDto request) => await _voteLogic.CastVoteAsync(request);

        public async Task ProcessAfkTimeout(int gameId)
        {
            if (_stateManager.TryAddProcessingGame(gameId))
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

                        int strikes = _stateManager.AddOrUpdateAfkStrike(afkPlayer.Username);

                        if (strikes >= 3) await HandleMaxAfkStrikes(gameId, afkPlayer);
                        else await HandleAfkWarning(gameId, afkPlayer, strikes, totalMoves);

                        NotifyTurnUpdateSafe(gameId);
                    }
                }
                catch (Exception ex) { Log.Error($"AFK Process Error {gameId}", ex); }
                finally
                {
                    _stateManager.RemoveProcessingGame(gameId);
                }
            }
        }

        private async Task HandleMaxAfkStrikes(int gameId, Player afkPlayer)
        {
            _stateManager.RemoveAfkStrike(afkPlayer.Username);
            _voteLogic.CancelVote(gameId);
            var game = await _repository.GetGameByIdAsync(gameId);

            var sanctionService = _sanctionFactory.Create();
            await sanctionService.ProcessKickAsync(afkPlayer.Username, game?.LobbyCode, "AFK", "SYSTEM");

            _gameMonitor.UpdateActivity(game.IdGame);
        }

        private async Task HandleAfkWarning(int gameId, Player afkPlayer, int strikes, int totalMoves)
        {
            var lastMove = await _repository.GetLastMoveForPlayerAsync(gameId, afkPlayer.IdPlayer);
            int pos = lastMove?.FinalPosition ?? 0;
            _repository.AddMove(new MoveRecord
            {
                GameIdGame = gameId,
                PlayerIdPlayer = afkPlayer.IdPlayer,
                DiceOne = 0,
                DiceTwo = 0,
                TurnNumber = totalMoves + 1,
                ActionDescription = $"AFK Warning ({strikes}/3)",
                StartPosition = pos,
                FinalPosition = pos
            });
            await _repository.SaveChangesAsync();
            _gameMonitor.UpdateActivity(gameId);
        }
    }
}