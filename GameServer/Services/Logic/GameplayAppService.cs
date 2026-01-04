using GameServer.DTOs.Gameplay;
using GameServer.DTOs.Lobby;
using GameServer.Models;
using GameServer.Repositories;
using GameServer.Helpers;
using log4net;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Entity.Core;
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
        private static readonly ConcurrentDictionary<int, VoteState> _activeVotes = new ConcurrentDictionary<int, VoteState>();

        private static readonly int[] GooseTiles = { 5, 9, 18, 23, 27, 32, 36, 41, 45, 50, 54, 59 };
        private static readonly int[] LuckyBoxTiles = { 7, 14, 25, 34 };

        private readonly IGameplayRepository _repository;

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
                        catch (Exception ex)
                        {
                            Log.WarnFormat("No se pudo notificar turno a {0}: {1}", player.Username, ex.Message);
                            ConnectionManager.UnregisterGameplayClient(player.Username);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.ErrorFormat("Error en broadcast de turno para juego {0}: {1}", gameId, ex.Message);
            }
        }

        private async Task NotifyGameFinished(List<Player> players, string winnerUsername)
        {
            await Task.Yield();

            try
            {
                if (players == null || players.Count == 0) return;

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
                            Log.WarnFormat("Error notificando fin de juego a {0}: {1}", player.Username, ex.Message);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("Error en broadcast de fin de juego.", ex);
            }
        }

        public async Task<DiceRollDto> RollDiceAsync(GameplayRequest request)
        {
            if (request == null) return null;

            int gameIdForLock = 0;
            try
            {
                var game = await _repository.GetGameByLobbyCodeAsync(request.LobbyCode);
                if (game == null || game.GameStatus != (int)GameStatus.InProgress) return null;

                gameIdForLock = game.IdGame;
                if (!_processingGames.TryAdd(game.IdGame, true)) return null;

                var players = await _repository.GetPlayersInGameAsync(game.IdGame);
                var sortedPlayers = players.OrderBy(p => p.IdPlayer).ToList();

                if (!await ValidateTurnAsync(game.IdGame, sortedPlayers, request.Username))
                {
                    return null;
                }

                _afkStrikes.TryRemove(request.Username, out _);
                GameManager.Instance.UpdateActivity(game.IdGame);

                var player = sortedPlayers.First(p => p.Username == request.Username);
                DiceRollDto result;

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

                return result;
            }
            catch (Exception ex)
            {
                Log.Error("Error general en RollDice.", ex);
                return null;
            }
            finally
            {
                if (gameIdForLock != 0) _processingGames.TryRemove(gameIdForLock, out _);
            }
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

            if (ruleResult.Message == "WIN")
            {
                return await HandleGameVictoryAsync(game, player, d1, d2, total, currentPos);
            }

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

            return new DiceRollDto { DiceOne = d1, DiceTwo = d2, Total = total };
        }

        private (int, int) GenerateDiceRoll(int currentPos)
        {
            lock (_randomLock)
            {
                int d1 = RandomGenerator.Next(1, 7);
                int d2 = (currentPos < 60) ? RandomGenerator.Next(1, 7) : 0;
                return (d1, d2);
            }
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
                return result;
            }

            ApplyTileRules(result, player);

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
            _activeVotes.TryRemove(game.IdGame, out _);

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
            if (request == null) return new GameStateDto();

            try
            {
                var game = await _repository.GetGameByLobbyCodeAsync(request.LobbyCode);
                if (game == null) return new GameStateDto();

                if (game.GameStatus == (int)GameStatus.Finished)
                {
                    return await BuildFinishedGameStateAsync(game);
                }

                return await BuildActiveGameStateAsync(game.IdGame, request.Username);
            }
            catch (Exception ex)
            {
                Log.Error("Error obteniendo estado de juego.", ex);
                return new GameStateDto();
            }
        }

        private async Task<GameStateDto> BuildFinishedGameStateAsync(Game game)
        {
            string winner = "Desconocido";

            if (game.WinnerIdPlayer.HasValue)
            {
                var winnerPlayer = await _repository.GetPlayerByIdAsync(game.WinnerIdPlayer.Value);
                winner = winnerPlayer?.Username ?? "Desconocido";
            }
            else
            {
                var winningMove = await _repository.GetWinningMoveAsync(game.IdGame);
                if (winningMove != null)
                {
                    var winnerP = await _repository.GetPlayerByIdAsync(winningMove.PlayerIdPlayer);
                    winner = winnerP?.Username ?? "Desconocido";
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
            if (activePlayers.Count == 0) return new GameStateDto();

            var sortedPlayers = activePlayers.OrderBy(p => p.IdPlayer).ToList();
            int totalMoves = await _repository.GetMoveCountAsync(gameId);
            int extraTurns = await _repository.GetExtraTurnCountAsync(gameId);
            int nextPlayerIndex = (totalMoves - extraTurns) % sortedPlayers.Count;
            var currentTurnPlayer = sortedPlayers[nextPlayerIndex];

            var lastMove = await _repository.GetLastGlobalMoveAsync(gameId);
            var logs = await _repository.GetGameLogsAsync(gameId, 20);
            var cleanLogs = logs.Select(l => l.Replace("[EXTRA] ", "")).ToList();

            var playerPositions = await GetPlayerPositionsAsync(gameId, sortedPlayers, currentTurnPlayer.Username);

            return new GameStateDto
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
            try
            {
                var game = await _repository.GetGameByLobbyCodeAsync(request.LobbyCode);
                if (game == null) return false;

                var player = await _repository.GetPlayerByUsernameAsync(request.Username);
                if (player == null) return false;

                _activeVotes.TryRemove(game.IdGame, out _);

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
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("Error al abandonar juego.", ex);
                return false;
            }
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
                Log.InfoFormat("Juego {0} terminado por abandono. Ganador: {1}", game.LobbyCode, winner.Username);
            }
            else
            {
                game.WinnerIdPlayer = null;
                Log.InfoFormat("Juego {0} terminado. Todos los jugadores abandonaron.", game.LobbyCode);
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
            await ValidateVoteRequestAsync(request);

            var player = await _repository.GetPlayerByUsernameAsync(request.Username);
            int gameId = player.GameIdGame.Value;
            var activePlayers = await _repository.GetPlayersInGameAsync(gameId);
            int eligibleVoters = activePlayers.Count - 1;

            var voteState = new VoteState
            {
                TargetUsername = request.TargetUsername,
                InitiatorUsername = request.Username,
                Reason = request.Reason,
                TotalEligibleVoters = eligibleVoters
            };

            voteState.VotesFor.Add(request.Username);

            if (_activeVotes.TryAdd(gameId, voteState))
            {
                Log.InfoFormat("Votación iniciada en juego {0} contra {1}. Razón: {2}", gameId, request.TargetUsername, request.Reason);
                NotifyVoteStarted(activePlayers, request.TargetUsername, request.Reason);
            }
        }

        private async Task ValidateVoteRequestAsync(VoteRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.TargetUsername))
                throw new InvalidOperationException("Debe especificar un objetivo.");

            if (request.Username == request.TargetUsername)
                throw new InvalidOperationException("No puedes expulsarte a ti mismo.");

            var player = await _repository.GetPlayerByUsernameAsync(request.Username);
            if (player == null || !player.GameIdGame.HasValue)
                throw new FaultException("No estás en una partida.");

            int gameId = player.GameIdGame.Value;
            if (_activeVotes.ContainsKey(gameId))
                throw new InvalidOperationException("Ya hay una votación en curso.");

            var activePlayers = await _repository.GetPlayersInGameAsync(gameId);
            if (activePlayers.Count < 3)
                throw new FaultException("Se requieren al menos 3 jugadores para iniciar una votación.");

            var targetPlayer = activePlayers.FirstOrDefault(p => p.Username == request.TargetUsername);
            if (targetPlayer == null)
                throw new FaultException("El jugador objetivo no está en la partida.");

            var lastMove = await _repository.GetLastMoveForPlayerAsync(gameId, targetPlayer.IdPlayer);
            if (lastMove != null && lastMove.FinalPosition >= 55)
            {
                throw new FaultException("Protección Activada: No se puede expulsar a un jugador en la recta final.");
            }
        }

        private void NotifyVoteStarted(List<Player> players, string targetUsername, string reason)
        {
            foreach (var p in players.Where(p => p.Username != targetUsername))
            {
                var callback = ConnectionManager.GetGameplayClient(p.Username);
                if (callback != null)
                {
                    try
                    {
                        callback.OnVoteKickStarted(targetUsername, reason);
                    }
                    catch (Exception ex)
                    {
                        Log.WarnFormat("No se pudo notificar voto a {0}: {1}", p.Username, ex.Message);
                    }
                }
            }
        }

        public async Task CastVoteAsync(VoteResponseDto request)
        {
            var player = await _repository.GetPlayerByUsernameAsync(request.Username);
            if (player == null || !player.GameIdGame.HasValue) return;

            int gameId = player.GameIdGame.Value;

            if (!_activeVotes.TryGetValue(gameId, out VoteState state))
                throw new FaultException("No hay votación activa en esta partida.");

            if (request.Username == state.TargetUsername) return;

            bool voteFinished = false;

            lock (state)
            {
                if (!state.VotesFor.Contains(request.Username) && !state.VotesAgainst.Contains(request.Username))
                {
                    if (request.AcceptKick) state.VotesFor.Add(request.Username);
                    else state.VotesAgainst.Add(request.Username);

                    int totalVotesCast = state.VotesFor.Count + state.VotesAgainst.Count;
                    if (totalVotesCast >= state.TotalEligibleVoters)
                    {
                        voteFinished = true;
                    }
                }
            }

            if (voteFinished)
            {
                await ProcessVoteResult(gameId, state);
            }
        }

        private async Task ProcessVoteResult(int gameId, VoteState state)
        {
            _activeVotes.TryRemove(gameId, out _);

            bool isKicked = false;
            if (state.TotalEligibleVoters == 2) isKicked = (state.VotesFor.Count == 2);
            else if (state.TotalEligibleVoters >= 3) isKicked = (state.VotesFor.Count >= 2);

            if (isKicked)
            {
                Log.InfoFormat("Jugador {0} expulsado por votación.", state.TargetUsername);
                NotifyGameplayKicked(state.TargetUsername, "Has sido expulsado por votación de la mayoría.");
                await SanctionAndRemovePlayer(gameId, state);
            }
            else
            {
                Log.InfoFormat("Votación contra {0} rechazada.", state.TargetUsername);
            }
        }

        private async Task SanctionAndRemovePlayer(int gameId, VoteState state)
        {
            using (var repo = new GameplayRepository())
            {
                try
                {
                    var targetP = await repo.GetPlayerByUsernameAsync(state.TargetUsername);
                    string lobbyCodeForSanction = "UNKNOWN";

                    if (targetP != null)
                    {
                        var game = await repo.GetGameByIdAsync(gameId);
                        lobbyCodeForSanction = game?.LobbyCode ?? "UNKNOWN";

                        await ApplyLossStats(repo, targetP.IdPlayer);

                        targetP.GameIdGame = null;
                        targetP.TurnsSkipped = 0;
                        await repo.SaveChangesAsync();
                    }

                    var sanctionLogic = new SanctionAppService(repo);
                    await sanctionLogic.ProcessKickSanctionAsync(state.TargetUsername, lobbyCodeForSanction, state.Reason);

                    await NotifyTurnUpdate(gameId);
                }
                catch (Exception ex)
                {
                    Log.Error("Error al ejecutar expulsión y sanción", ex);
                }
            }
        }

        private async Task ApplyLossStats(GameplayRepository repo, int playerId)
        {
            var playerStats = await repo.GetPlayerWithStatsByIdAsync(playerId);
            if (playerStats?.PlayerStat != null)
            {
                playerStats.PlayerStat.MatchesPlayed++;
                playerStats.PlayerStat.MatchesLost++;
            }
        }

        private static void NotifyGameplayKicked(string username, string reason)
        {
            var callback = ConnectionManager.GetGameplayClient(username);
            if (callback != null)
            {
                try
                {
                    callback.OnPlayerKicked(reason);
                }
                catch (Exception ex)
                {
                    Log.WarnFormat("Error de comunicación al notificar kick a {0}: {1}", username, ex.Message);
                }
                finally
                {
                    ConnectionManager.UnregisterGameplayClient(username);
                }
            }
        }

        private RewardResult ProcessLuckyBoxReward(Player player)
        {
            int roll = RandomGenerator.Next(1, 101);

            if (roll <= 50)
            {
                int coins = 50;
                player.Coins += coins;
                return new RewardResult { Type = "COINS", Amount = coins, Description = string.Format("¡Has encontrado {0} Monedas de Oro!", coins) };
            }
            else if (roll <= 80)
            {
                player.TicketCommon++;
                return new RewardResult { Type = "COMMON", Amount = 1, Description = "¡Has desbloqueado un Ticket COMÚN!" };
            }
            else if (roll <= 95)
            {
                player.TicketEpic++;
                return new RewardResult { Type = "EPIC", Amount = 1, Description = "¡INCREÍBLE! ¡Ticket ÉPICO obtenido!" };
            }
            else
            {
                player.TicketLegendary++;
                return new RewardResult { Type = "LEGENDARY", Amount = 1, Description = "¡JACKPOT! ¡Ticket LEGENDARIO!" };
            }
        }

        public async Task ProcessAfkTimeout(int gameId)
        {
            if (!_processingGames.TryAdd(gameId, true)) return;

            try
            {
                var players = await _repository.GetPlayersInGameAsync(gameId);
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
            catch (Exception ex)
            {
                Log.Error(string.Format("Error procesando AFK para juego {0}", gameId), ex);
            }
            finally
            {
                _processingGames.TryRemove(gameId, out _);
            }
        }

        private async Task HandleMaxAfkStrikes(int gameId, Player afkPlayer)
        {
            _afkStrikes.TryRemove(afkPlayer.Username, out _);
            _activeVotes.TryRemove(gameId, out _);

            NotifyGameplayKicked(afkPlayer.Username, "Expulsado por inactividad (AFK).");

            var game = await _repository.GetGameByIdAsync(gameId);
            using (var lobbyRepo = new LobbyRepository())
            {
                var lobbyLogic = new LobbyAppService(lobbyRepo);
                var kickReq = new KickPlayerRequest
                {
                    LobbyCode = game.LobbyCode,
                    TargetUsername = afkPlayer.Username,
                    RequestorUsername = "SYSTEM"
                };

                await lobbyLogic.KickPlayerAsync(kickReq);
            }
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