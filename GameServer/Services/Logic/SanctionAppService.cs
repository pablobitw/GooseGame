using GameServer.DTOs.Lobby;
using GameServer.Helpers;
using GameServer.Interfaces;
using GameServer.Models;
using GameServer.Repositories;
using GameServer.Services.Common;
using log4net;
using System;
using System.Data.Entity.Core;
using System.Data.SqlClient;
using System.Threading.Tasks;

namespace GameServer.Services.Logic
{
    public class SanctionAppService : ISanctionAppService, IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(SanctionAppService));
        private readonly IGameplayRepository _repository;
        private readonly IGameplayConnectionManager _connectionManager;
        private readonly ISanctionServiceFactory _serviceFactory; 
        private bool _disposed = false;

        private const int MaxKicksAllowed = 3;
        private const int BanDurationYears = 1;
        private const int SanctionTypeKick = 1;
        private const int SanctionTypeBan = 2;

        public SanctionAppService(
            IGameplayRepository repository = null,
            IGameplayConnectionManager connectionManager = null,
            ISanctionServiceFactory serviceFactory = null)
        {
            _repository = repository ?? new GameplayRepository();
            _connectionManager = connectionManager ?? new GameplayConnectionManagerWrapper();

            _serviceFactory = serviceFactory ?? new SanctionServiceFactory();
        }

        public async Task ProcessKickAsync(string username, string lobbyCode, string reason, string source)
        {
            Log.Info($"[SanctionHub] Processing kick for {username}. Source: {source}. Reason: {reason}");

            try
            {
                var player = await _repository.GetPlayerByUsernameAsync(username);
                if (player == null)
                {
                    Log.Warn($"[SanctionHub] Player {username} not found. Kick processing aborted.");
                    return;
                }

                int gameId = 0;
                var game = await _repository.GetGameByLobbyCodeAsync(lobbyCode);
                if (game != null) gameId = game.IdGame;
                if (gameId == 0 && player.GameIdGame.HasValue) gameId = player.GameIdGame.Value;

                await UpdatePlayerStatsAsync(player);
                string finalReason = EvaluateSanction(player, reason, source, gameId, out bool isBanApplied);
                await _repository.SaveChangesAsync();
                NotifyAndDisconnect(username, lobbyCode, finalReason);
            }
            catch (SqlException ex) { Log.Error("[SanctionHub] Critical SQL error.", ex); }
            catch (EntityException ex) { Log.Error("[SanctionHub] Critical Entity error.", ex); }
            catch (Exception ex) { Log.Error("[SanctionHub] General error.", ex); }
        }

        private async Task UpdatePlayerStatsAsync(Player player)
        {
            player.KickCount++;
            var stats = await _repository.GetPlayerWithStatsByIdAsync(player.IdPlayer);
            if (stats?.PlayerStat != null)
            {
                stats.PlayerStat.KicksReceived++;
                if (player.GameIdGame.HasValue)
                {
                    stats.PlayerStat.MatchesPlayed++;
                    stats.PlayerStat.MatchesLost++;
                }
            }
            player.GameIdGame = null;
            player.TurnsSkipped = 0;
        }

        private string EvaluateSanction(Player player, string originalReason, string source, int gameId, out bool isBanApplied)
        {
            isBanApplied = false;
            string finalReason = originalReason;

            if (player.KickCount >= MaxKicksAllowed)
            {
                player.IsBanned = true;
                isBanApplied = true;
                finalReason = $"[AUTO-BAN] Accumulated {MaxKicksAllowed} faults. Last: {originalReason}";
                Log.Info($"[SanctionHub] Player {player.Username} BANNED.");
            }

            if (player.Account_IdAccount.HasValue && player.Account_IdAccount.Value != 0)
                CreateSanctionRecord(player, gameId, source, finalReason, isBanApplied);
            else
                Log.Info($"[SanctionHub] Guest player {player.Username} kicked. No record.");

            return finalReason;
        }

        private void CreateSanctionRecord(Player player, int gameId, string source, string reason, bool isBan)
        {
            if (gameId == 0) return;

            var sanction = new Sanction
            {
                Account_IdAccount = player.Account_IdAccount.Value,
                Game_IdGame = gameId,
                StartDate = DateTime.UtcNow,
                Reason = $"{source}: {reason}",
                SanctionType = isBan ? SanctionTypeBan : SanctionTypeKick,
                EndDate = isBan ? DateTime.UtcNow.AddYears(BanDurationYears) : DateTime.UtcNow
            };
            _repository.AddSanction(sanction);
        }

        private void NotifyAndDisconnect(string username, string lobbyCode, string reason)
        {
            var client = _connectionManager.GetGameplayClient(username);
            if (client != null)
            {
                try { client.OnPlayerKicked(reason); }
                catch (Exception ex) { Log.Warn($"Could not notify {username}.", ex); }
                finally { _connectionManager.UnregisterGameplayClient(username); }
            }

            Task.Run(async () =>
            {
                try
                {
                    using (var lobbyService = _serviceFactory.CreateLobbyService())
                    {
                        await lobbyService.SystemKickPlayerAsync(lobbyCode, username, reason);
                    }
                }
                catch (Exception ex) { Log.Error($"[SanctionHub] Error removing {username} from lobby.", ex); }
            });
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing) _repository?.Dispose();
                _disposed = true;
            }
        }
    }
}