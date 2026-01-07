using GameServer.DTOs.Gameplay;
using GameServer.DTOs.Lobby;
using GameServer.GameEngines;
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

        private static readonly ConcurrentDictionary<int, bool> _processingGames = new ConcurrentDictionary<int, bool>();
        private static readonly ConcurrentDictionary<string, int> _afkStrikes = new ConcurrentDictionary<string, int>();

        private readonly IGameplayRepository _repository;
        private readonly VoteLogic _voteLogic;
        private readonly Func<SanctionAppService> _sanctionServiceFactory;
        private readonly GooseBoardEngine _gameEngine; 

        public GameplayAppService(IGameplayRepository repository)
        {
            _repository = repository;
            _voteLogic = new VoteLogic(repository);
            _sanctionServiceFactory = () => new SanctionAppService();
            _gameEngine = new GooseBoardEngine(); 
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

                if (!_processingGames.TryAdd(game.IdGame, true))
                {
                    result.ErrorType = GameplayErrorType.Timeout;
                    result.ErrorMessage = "Procesando turno anterior...";
                    return result;
                }

                result = await ProcessGameTurn(game, request.Username);
            }
            catch (SqlException ex)
            {
                Log.Error("SQL Error in RollDice", ex);
                result.ErrorType = GameplayErrorType.DatabaseError;
                result.ErrorMessage = "Error de conexión con base de datos.";
                result.Success = false;
            }
            catch (EntityException ex)
            {
                Log.Error("Entity Error en RollDice", ex);
                result.ErrorType = GameplayErrorType.DatabaseError;
                result.ErrorMessage = "Error interno de datos.";
                result.Success = false;
            }
            catch (Exception ex)
            {
                Log.Error("Unexpected Error en RollDice", ex);
                result.ErrorType = GameplayErrorType.Unknown;
                result.ErrorMessage = "Error inesperado en el servidor.";
                result.Success = false;
            }
            finally
            {
                if (gameIdForLock != 0)
                {
                    _processingGames.TryRemove(gameIdForLock, out _);
                }
            }

            return result;
        }

        private async Task<DiceRollDto> ProcessGameTurn(Game game, string username)
        {
            var players = await _repository.GetPlayersInGameAsync(game.IdGame);
            var sortedPlayers = players.OrderBy(p => p.IdPlayer).ToList();

            if (!await ValidateTurnAsync(game.IdGame, sortedPlayers, username))
            {
                return new DiceRollDto
                {
                    Success = false,
                    ErrorType = GameplayErrorType.NotYourTurn,
                    ErrorMessage = "No es tu turno."
                };
            }

            _afkStrikes.TryRemove(username, out _);
            GameManager.Instance.UpdateActivity(game.IdGame);

            var player = sortedPlayers.First(p => p.Username == username);

            DiceRollDto result;
            if (player.TurnsSkipped > 0)
            {
                result = await HandleSkippedTurnAsync(game.IdGame, player);
            }
            else
            {
                result = await ProcessNormalTurnAsync(game, player);
            }

            result.Success = true;
            result.ErrorType = GameplayErrorType.None;

            var gameCheck = await _repository.GetGameByIdAsync(game.IdGame);
            if (gameCheck.GameStatus == (int)GameStatus.InProgress)
            {
                await NotifyTurnUpdate(game.IdGame);
            }

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

            if (request == null)
            {
                gameState.ErrorMessage = "Solicitud inválida.";
                return gameState;
            }

            try
            {
                var game = await _repository.GetGameByLobbyCodeAsync(request.LobbyCode);

                if (game == null)
                {
                    gameState.ErrorType = GameplayErrorType.GameNotFound;
                    gameState.ErrorMessage = "Partida no encontrada.";
                    return gameState;
                }

                var player = await _repository.GetPlayerByUsernameAsync(request.Username);

                if (player != null && player.GameIdGame != game.IdGame)
                {
                    gameState.Success = true;
                    gameState.IsKicked = true;
                    gameState.IsBanned = player.IsBanned;
                    gameState.ErrorType = GameplayErrorType.PlayerKicked;
                    return gameState;
                }

                if (game.GameStatus == (int)GameStatus.Finished)
                {
                    gameState = await BuildFinishedGameStateAsync(game);
                }
                else
                {
                    gameState = await BuildActiveGameStateAsync(game.IdGame, request.Username);
                }

                gameState.Success = true;
                gameState.ErrorType = GameplayErrorType.None;
            }
            catch (SqlException ex)
            {
                Log.Error("SQL Error", ex);
                gameState.ErrorType = GameplayErrorType.DatabaseError;
                gameState.ErrorMessage = "Error de base de datos.";
            }
            catch (EntityException ex)
            {
                Log.Error("Entity Error", ex);
                gameState.ErrorType = GameplayErrorType.DatabaseError;
                gameState.ErrorMessage = "Error interno de datos.";
            }
            catch (TimeoutException ex)
            {
                Log.Error("Timeout", ex);
                gameState.ErrorType = GameplayErrorType.Timeout;
                gameState.ErrorMessage = "Tiempo de espera agotado.";
            }
            catch (Exception ex)
            {
                Log.Error("General Error", ex);
                gameState.ErrorType = GameplayErrorType.Unknown;
                gameState.ErrorMessage = "Error desconocido.";
            }

            return gameState;
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
            catch (Exception ex)
            {
                Log.Error("Error leaving game", ex);
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

        private async Task NotifyTurnUpdate(int gameId)
        {
            try
            {
                var players = await _repository.GetPlayersInGameAsync(gameId);
                var usernames = players.Select(player => player.Username).ToList();

                foreach (var username in usernames)
                {
                    var client = ConnectionManager.GetGameplayClient(username);
                    if (client != null)
                    {
                        try
                        {
                            var stateDto = await BuildActiveGameStateAsync(gameId, username);
                            stateDto.Success = true;
                            client.OnTurnChanged(stateDto);
                        }
                        catch (CommunicationException ex)
                        {
                            Log.WarnFormat("Communication error notifying turn: {0}", ex.Message);
                            ConnectionManager.UnregisterGameplayClient(username);
                        }
                        catch (TimeoutException ex)
                        {
                            Log.WarnFormat("Timeout notifying turn: {0}", ex.Message);
                            ConnectionManager.UnregisterGameplayClient(username);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.ErrorFormat("Critical Error in turn broadcast: {0}", ex.Message);
            }
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
                Success = true,
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

        private static async Task NotifyGameFinished(List<Player> players, string winnerUsername)
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
                        catch (Exception ex)
                        {
                            Log.WarnFormat("Error notifying game finish: {0}", ex.Message);
                        }
                    }
                }
            }
        }

        public async Task InitiateVoteKickAsync(VoteRequestDto request)
        {
            await _voteLogic.InitiateVoteAsync(request);
        }

        public async Task CastVoteAsync(VoteResponseDto request)
        {
            await _voteLogic.CastVoteAsync(request);
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
                    Log.ErrorFormat("SQL Error {0}: {1}", gameId, ex.Message);
                }
                catch (EntityException ex)
                {
                    Log.ErrorFormat("Entity Error {0}: {1}", gameId, ex.Message);
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
            string lobbyCode = game?.LobbyCode ?? "DESCONOCIDO";

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