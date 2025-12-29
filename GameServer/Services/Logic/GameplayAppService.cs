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
using System.Data.Entity.Infrastructure;
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
        private static readonly ConcurrentDictionary<int, DateTime> _lastGameActivity = new ConcurrentDictionary<int, DateTime>();
        private static readonly ConcurrentDictionary<string, int> _afkStrikes = new ConcurrentDictionary<string, int>();

        private static readonly ConcurrentDictionary<int, VoteState> _activeVotes = new ConcurrentDictionary<int, VoteState>();

        private static readonly int[] GooseTiles = { 5, 9, 14, 18, 23, 27, 32, 36, 41, 45, 50, 54, 59 };
        private static readonly int[] LuckyBoxTiles = { 7, 14, 25, 34 };
        private const int TurnTimeLimitSeconds = 20;
        private readonly GameplayRepository _repository;

        public GameplayAppService(GameplayRepository repository)
        {
            _repository = repository;
        }

        public async Task<DiceRollDTO> RollDiceAsync(GameplayRequest request)
        {
            DiceRollDTO result = null;
            int gameIdForLock = 0;

            if (request == null) return null;

            var game = await _repository.GetGameByLobbyCodeAsync(request.LobbyCode);
            if (game == null || game.GameStatus != (int)GameStatus.InProgress) return null;

            gameIdForLock = game.IdGame;
            if (!_processingGames.TryAdd(game.IdGame, true))
            {
                return null;
            }

            try
            {
                var activePlayers = await _repository.GetPlayersInGameAsync(game.IdGame);
                activePlayers = activePlayers.OrderBy(p => p.IdPlayer).ToList();

                int totalMoves = await _repository.GetMoveCountAsync(game.IdGame);
                int extraTurns = await _repository.GetExtraTurnCountAsync(game.IdGame);
                int effectiveTurns = totalMoves - extraTurns;

                int nextPlayerIndex = effectiveTurns % activePlayers.Count;
                var currentTurnPlayer = activePlayers[nextPlayerIndex];

                if (currentTurnPlayer.Username != request.Username)
                {
                    return null;
                }

                _afkStrikes.TryRemove(request.Username, out _);
                _lastGameActivity[game.IdGame] = DateTime.Now;

                var player = await _repository.GetPlayerByUsernameAsync(request.Username);
                if (player != null)
                {
                    if (player.TurnsSkipped > 0)
                    {
                        player.TurnsSkipped--;
                        int turnNumSkipped = await _repository.GetMoveCountAsync(game.IdGame) + 1;
                        var prevMove = await _repository.GetLastMoveForPlayerAsync(game.IdGame, player.IdPlayer);
                        int samePos = prevMove?.FinalPosition ?? 0;

                        var skipMove = new MoveRecord
                        {
                            GameIdGame = game.IdGame,
                            PlayerIdPlayer = player.IdPlayer,
                            DiceOne = 0,
                            DiceTwo = 0,
                            TurnNumber = turnNumSkipped,
                            ActionDescription = $"{request.Username} pierde el turno (Quedan {player.TurnsSkipped}).",
                            StartPosition = samePos,
                            FinalPosition = samePos
                        };

                        _repository.AddMove(skipMove);
                        await _repository.SaveChangesAsync();

                        return new DiceRollDTO { DiceOne = 0, DiceTwo = 0, Total = 0 };
                    }

                    var lastMove = await _repository.GetLastMoveForPlayerAsync(game.IdGame, player.IdPlayer);
                    int currentPos = lastMove?.FinalPosition ?? 0;

                    int d1;
                    int d2;

                    lock (_randomLock)
                    {
                        d1 = RandomGenerator.Next(1, 7);
                        d2 = (currentPos < 60) ? RandomGenerator.Next(1, 7) : 0;
                    }

                    int total = d1 + d2;
                    int finalPos = currentPos + total;

                    string message = "";
                    string luckyBoxTag = "";
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
                        game.WinnerIdPlayer = player.IdPlayer;
                        await UpdateStatsEndGame(game.IdGame, player.IdPlayer);
                        _lastGameActivity.TryRemove(game.IdGame, out _);

                        _activeVotes.TryRemove(game.IdGame, out _);
                    }
                    else if (LuckyBoxTiles.Contains(finalPos))
                    {
                        var reward = ProcessLuckyBoxReward(player);
                        luckyBoxTag = $"[LUCKYBOX:{player.Username}:{reward.Type}_{reward.Amount}]";
                        message = $"¡CAJA DE LA SUERTE! {reward.Description}";
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
                    else if (finalPos == 6)
                    {
                        message = "¡De Puente a Puente! Saltas al 12 y tiras de nuevo.";
                        finalPos = 12;
                        isExtraTurn = true;
                    }
                    else if (finalPos == 12)
                    {
                        message = "¡De Puente a Puente! Regresas al 6 y tiras de nuevo.";
                        finalPos = 6;
                        isExtraTurn = true;
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
                    else if (finalPos == 26 || finalPos == 53)
                    {
                        int bonus = finalPos;
                        message = $"¡Dados! Sumas {bonus} casillas extra.";
                        finalPos += bonus;
                        if (finalPos > 64) finalPos = 64 - (finalPos - 64);
                    }
                    else if (finalPos == 19)
                    {
                        message = "¡Posada! Pierdes 1 turno.";
                        player.TurnsSkipped = 1;
                    }
                    else if (finalPos == 31)
                    {
                        message = "¡Pozo! Esperas rescate (2 turnos).";
                        player.TurnsSkipped = 2;
                    }
                    else if (finalPos == 56)
                    {
                        message = "¡Cárcel! Esperas 3 turnos.";
                        player.TurnsSkipped = 3;
                    }

                    string baseMsg = d2 > 0 ? $"{request.Username} tiró {d1} y {d2}." : $"{request.Username} tiró {d1}.";
                    string fullDescription = string.IsNullOrEmpty(message) ? $"{baseMsg} Avanza a {finalPos}." : $"{baseMsg} {message}";

                    if (isExtraTurn) fullDescription = "[EXTRA] " + fullDescription;
                    if (!string.IsNullOrEmpty(luckyBoxTag)) fullDescription = luckyBoxTag + " " + fullDescription;

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

                    result = new DiceRollDTO { DiceOne = d1, DiceTwo = d2, Total = total };
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
            catch (TimeoutException ex)
            {
                Log.Error("Timeout en RollDice.", ex);
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

        public async Task<GameStateDTO> GetGameStateAsync(GameplayRequest request)
        {
            GameStateDTO state = new GameStateDTO
            {
                GameLog = new List<string>(),
                PlayerPositions = new List<PlayerPositionDTO>()
            };

            try
            {
                if (request != null)
                {
                    var game = await _repository.GetGameByLobbyCodeAsync(request.LobbyCode);
                    if (game != null)
                    {
                        if (game.GameStatus == (int)GameStatus.Finished)
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

                            return new GameStateDTO
                            {
                                IsGameOver = true,
                                WinnerUsername = winner,
                                GameLog = new List<string> { "La partida ha finalizado." },
                                PlayerPositions = new List<PlayerPositionDTO>(),
                                CurrentTurnUsername = "",
                                IsMyTurn = false
                            };
                        }

                        if (!_lastGameActivity.ContainsKey(game.IdGame))
                        {
                            _lastGameActivity[game.IdGame] = DateTime.Now;
                        }
                        else
                        {
                            TimeSpan inactivity = DateTime.Now - _lastGameActivity[game.IdGame];
                            if (inactivity.TotalSeconds > TurnTimeLimitSeconds)
                            {
                                await ProcessAfkTimeout(game.IdGame);
                            }
                        }

                        var activePlayers = await _repository.GetPlayersInGameAsync(game.IdGame);

                        if (activePlayers.Count > 0)
                        {
                            activePlayers = activePlayers.OrderBy(p => p.IdPlayer).ToList();

                            int totalMoves = await _repository.GetMoveCountAsync(game.IdGame);
                            int extraTurns = await _repository.GetExtraTurnCountAsync(game.IdGame);

                            int effectiveTurns = totalMoves - extraTurns;
                            int nextPlayerIndex = effectiveTurns % activePlayers.Count;
                            var currentTurnPlayer = activePlayers[nextPlayerIndex];

                            var lastMove = await _repository.GetLastGlobalMoveAsync(game.IdGame);
                            var logs = await _repository.GetGameLogsAsync(game.IdGame, 20);

                            var cleanLogs = logs.Select(l => l.Replace("[EXTRA] ", "")).ToList();

                            var playerPositions = new List<PlayerPositionDTO>();
                            foreach (var p in activePlayers)
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

                            state = new GameStateDTO
                            {
                                CurrentTurnUsername = currentTurnPlayer.Username,
                                IsMyTurn = (currentTurnPlayer.Username == request.Username),
                                LastDiceOne = lastMove?.DiceOne ?? 0,
                                LastDiceTwo = lastMove?.DiceTwo ?? 0,
                                GameLog = cleanLogs,
                                PlayerPositions = playerPositions,
                                IsGameOver = false,
                                WinnerUsername = null
                            };
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                Log.Error("Error SQL obteniendo estado de juego.", ex);
            }
            catch (TimeoutException ex)
            {
                Log.Error("Timeout obteniendo estado de juego.", ex);
            }

            return state;
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

                var playerWithStats = await _repository.GetPlayerWithStatsByIdAsync(player.IdPlayer);
                if (playerWithStats != null && playerWithStats.PlayerStat != null)
                {
                    playerWithStats.PlayerStat.MatchesPlayed++;
                    playerWithStats.PlayerStat.MatchesLost++;
                }

                var allPlayers = await _repository.GetPlayersInGameAsync(game.IdGame);
                player.GameIdGame = null;
                player.TurnsSkipped = 0;

                var remainingPlayers = allPlayers.Where(p => p.IdPlayer != player.IdPlayer).ToList();

                if (remainingPlayers.Count < 2)
                {
                    game.GameStatus = (int)GameStatus.Finished;

                    if (remainingPlayers.Count == 1)
                    {
                        var winner = remainingPlayers[0];
                        game.WinnerIdPlayer = winner.IdPlayer;

                        var winnerWithStats = await _repository.GetPlayerWithStatsByIdAsync(winner.IdPlayer);
                        if (winnerWithStats != null && winnerWithStats.PlayerStat != null)
                        {
                            winnerWithStats.PlayerStat.MatchesPlayed++;
                            winnerWithStats.PlayerStat.MatchesWon++;
                            winnerWithStats.TicketCommon += 1;
                            winnerWithStats.Coins += 300;
                        }

                        Log.InfoFormat("Juego {0} terminado por abandono. Ganador: {1}", game.LobbyCode, winner.Username);
                        _lastGameActivity.TryRemove(game.IdGame, out _);
                    }
                    else
                    {
                        game.WinnerIdPlayer = null;
                        Log.InfoFormat("Juego {0} terminado. Todos los jugadores abandonaron.", game.LobbyCode);
                    }
                }

                await _repository.SaveChangesAsync();
                return true;
            }
            catch (SqlException ex)
            {
                Log.Error("Error SQL al abandonar juego.", ex);
                return false;
            }
            catch (DbUpdateException ex)
            {
                Log.Error("Error DB al abandonar juego.", ex);
                return false;
            }
            catch (TimeoutException ex)
            {
                Log.Error("Timeout al abandonar juego.", ex);
                return false;
            }
        }

        public async Task InitiateVoteKickAsync(VoteRequestDTO request)
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
                Log.Info($"Votación iniciada en juego {gameId} contra {request.TargetUsername}. Razón: {request.Reason}");

                foreach (var p in activePlayers)
                {
                    if (p.Username == request.TargetUsername) continue;

                    var callback = ConnectionManager.GetGameplayClient(p.Username);
                    if (callback != null)
                    {
                        try
                        {
                            callback.OnVoteKickStarted(request.TargetUsername, request.Reason);
                        }
                        catch (Exception ex)
                        {
                            Log.Warn($"No se pudo notificar voto a {p.Username}: {ex.Message}");
                        }
                    }
                }
            }
        }

        public async Task CastVoteAsync(VoteResponseDTO request)
        {
            var player = await _repository.GetPlayerByUsernameAsync(request.Username);
            if (player == null || !player.GameIdGame.HasValue) return;

            int gameId = player.GameIdGame.Value;

            if (!_activeVotes.TryGetValue(gameId, out VoteState state))
                throw new FaultException("No hay votación activa en esta partida.");

            if (request.Username == state.TargetUsername) return;

            lock (state)
            {
                if (state.VotesFor.Contains(request.Username) || state.VotesAgainst.Contains(request.Username))
                {
                    return;
                }

                if (request.AcceptKick) state.VotesFor.Add(request.Username);
                else state.VotesAgainst.Add(request.Username);

                int totalVotesCast = state.VotesFor.Count + state.VotesAgainst.Count;

                if (totalVotesCast >= state.TotalEligibleVoters)
                {
                    _ = ProcessVoteResult(gameId, state);
                }
            }
            await Task.CompletedTask;
        }

        private async Task ProcessVoteResult(int gameId, VoteState state)
        {
            _activeVotes.TryRemove(gameId, out _);

            bool isKicked = false;

            if (state.TotalEligibleVoters == 2) isKicked = (state.VotesFor.Count == 2);
            else if (state.TotalEligibleVoters >= 3) isKicked = (state.VotesFor.Count >= 2);

            if (isKicked)
            {
                Log.Info($"Jugador {state.TargetUsername} expulsado por votación.");

                NotifyGameplayKicked(state.TargetUsername, "Has sido expulsado por votación de la mayoría.");

                using (var repo = new GameplayRepository())
                {
                    try
                    {
                        var targetP = await repo.GetPlayerByUsernameAsync(state.TargetUsername);
                        string lobbyCodeForSanction = "";

                        if (targetP != null)
                        {
                            var game = await repo.GetGameByIdAsync(gameId);
                            lobbyCodeForSanction = game?.LobbyCode ?? "UNKNOWN";

                            var playerStats = await repo.GetPlayerWithStatsByIdAsync(targetP.IdPlayer);
                            if (playerStats != null && playerStats.PlayerStat != null)
                            {
                                playerStats.PlayerStat.MatchesPlayed++;
                                playerStats.PlayerStat.MatchesLost++;
                            }

                            targetP.GameIdGame = null;
                            targetP.TurnsSkipped = 0;
                            await repo.SaveChangesAsync();
                        }

                        var sanctionLogic = new SanctionAppService(repo);

                        await sanctionLogic.ProcessKickSanctionAsync(
                            state.TargetUsername,
                            lobbyCodeForSanction,
                            state.Reason
                        );
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Error al ejecutar expulsión y sanción", ex);
                    }
                }
            }
            else
            {
                Log.Info($"Votación contra {state.TargetUsername} rechazada.");
            }
        }

        private void NotifyGameplayKicked(string username, string reason)
        {
            var callback = ConnectionManager.GetGameplayClient(username);
            if (callback != null)
            {
                try
                {
                    callback.OnPlayerKicked(reason);
                }
                catch (CommunicationException) { }
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
                return new RewardResult { Type = "COINS", Amount = coins, Description = $"¡Has encontrado {coins} Monedas de Oro!" };
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

        private async Task ProcessAfkTimeout(int gameId)
        {
            try
            {
                if (!_processingGames.TryAdd(gameId, true)) return;

                var game = await _repository.GetGameByIdAsync(gameId);
                var activePlayers = await _repository.GetPlayersInGameAsync(gameId);
                activePlayers = activePlayers.OrderBy(p => p.IdPlayer).ToList();

                int totalMoves = await _repository.GetMoveCountAsync(gameId);
                int extraTurns = await _repository.GetExtraTurnCountAsync(gameId);
                int effectiveTurns = totalMoves - extraTurns;
                int nextPlayerIndex = effectiveTurns % activePlayers.Count;
                var afkPlayer = activePlayers[nextPlayerIndex];

                int strikes = _afkStrikes.AddOrUpdate(afkPlayer.Username, 1, (key, oldValue) => oldValue + 1);

                if (strikes >= 3)
                {
                    _afkStrikes.TryRemove(afkPlayer.Username, out _);
                    _activeVotes.TryRemove(gameId, out _);

                    NotifyGameplayKicked(afkPlayer.Username, "Expulsado por inactividad (AFK).");

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
                    _lastGameActivity[gameId] = DateTime.Now;
                    return;
                }

                var lastMove = await _repository.GetLastMoveForPlayerAsync(gameId, afkPlayer.IdPlayer);
                int samePos = lastMove?.FinalPosition ?? 0;
                int turnNum = totalMoves + 1;

                var skipMove = new MoveRecord
                {
                    GameIdGame = gameId,
                    PlayerIdPlayer = afkPlayer.IdPlayer,
                    DiceOne = 0,
                    DiceTwo = 0,
                    TurnNumber = turnNum,
                    ActionDescription = $"{afkPlayer.Username} tardó demasiado. Pierde turno ({strikes}/3).",
                    StartPosition = samePos,
                    FinalPosition = samePos
                };

                _repository.AddMove(skipMove);
                await _repository.SaveChangesAsync();

                _lastGameActivity[gameId] = DateTime.Now;
            }
            catch (Exception ex)
            {
                Log.Error($"Error procesando AFK para juego {gameId}", ex);
            }
            finally
            {
                _processingGames.TryRemove(gameId, out _);
            }
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