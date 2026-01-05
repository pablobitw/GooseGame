using GameServer.DTOs.Lobby;
using GameServer.Helpers;
using GameServer.Models;
using GameServer.Repositories;
using log4net;
using System;
using System.Data.Entity.Core;
using System.Data.SqlClient;
using System.Threading.Tasks;

namespace GameServer.Services.Logic
{
    public class SanctionAppService
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(SanctionAppService));

        public SanctionAppService()
        {
        }

        public async Task ProcessKickAsync(string username, string lobbyCode, string reason, string source)
        {
            Log.InfoFormat("[SanctionHub] Procesando kick para {0}. Origen: {1}. Razón: {2}", username, source, reason);

            using (var repo = new GameplayRepository())
            {
                try
                {
                    var player = await repo.GetPlayerByUsernameAsync(username);
                    if (player != null)
                    {
                        player.KickCount++;

                        var stats = await repo.GetPlayerWithStatsByIdAsync(player.IdPlayer);
                        if (stats?.PlayerStat != null)
                        {
                            stats.PlayerStat.KicksReceived++;
                        }

                        bool isBanApplied = false;
                        if (player.KickCount >= 3)
                        {
                            player.IsBanned = true;
                            isBanApplied = true;
                            reason = $"[AUTO-BAN] Acumulación de 3 faltas. Última: {reason}";
                            Log.InfoFormat("[SanctionHub] Jugador {0} BANEADO por acumulación de faltas.", username);
                        }

                        if (player.Account_IdAccount != null)
                        {
                            var game = await repo.GetGameByLobbyCodeAsync(lobbyCode);
                            int gameId = game?.IdGame ?? 0;

                            if (gameId == 0 && player.GameIdGame.HasValue)
                            {
                                gameId = player.GameIdGame.Value;
                            }

                            if (gameId != 0)
                            {
                                var sanction = new Sanction
                                {
                                    Account_IdAccount = player.Account_IdAccount.Value,
                                    Game_IdGame = gameId,
                                    StartDate = DateTime.UtcNow,
                                    Reason = $"{source}: {reason}",
                                    SanctionType = isBanApplied ? 2 : 1, 
                                    EndDate = isBanApplied ? DateTime.UtcNow.AddYears(1) : DateTime.UtcNow
                                };
                                repo.AddSanction(sanction);
                            }
                        }
                        else
                        {
                            Log.InfoFormat("[SanctionHub] Jugador invitado {0} expulsado. Se omite historial de sanciones.", username);
                        }

                        await ProcessGameExitStats(repo, player);
                        player.GameIdGame = null;
                        player.TurnsSkipped = 0;

                        await repo.SaveChangesAsync();

                        NotifyAndDisconnect(username, lobbyCode, reason);
                    }
                }
                catch (SqlException ex)
                {
                    Log.Error("[SanctionHub] Error SQL crítico al procesar kick.", ex);
                }
                catch (EntityException ex)
                {
                    Log.Error("[SanctionHub] Error Entity crítico al procesar kick.", ex);
                }
                catch (Exception ex)
                {
                    Log.Error("[SanctionHub] Error general al procesar kick.", ex);
                }
            }
        }

        private async Task ProcessGameExitStats(GameplayRepository repo, Player player)
        {
            if (player.GameIdGame.HasValue)
            {
                var stats = await repo.GetPlayerWithStatsByIdAsync(player.IdPlayer);
                if (stats?.PlayerStat != null)
                {
                    stats.PlayerStat.MatchesPlayed++;
                    stats.PlayerStat.MatchesLost++; 
                }
            }
        }

        private void NotifyAndDisconnect(string username, string lobbyCode, string reason)
        {
            var client = ConnectionManager.GetGameplayClient(username);
            if (client != null)
            {
                try
                {
                    client.OnPlayerKicked(reason);
                }
                catch (Exception)
                {

                }
                finally
                {
                    ConnectionManager.UnregisterGameplayClient(username);
                }
            }

            Task.Run(async () =>
            {
                using (var lobbyRepo = new LobbyRepository())
                {
                    var lobbyLogic = new LobbyAppService(lobbyRepo);
                    var req = new KickPlayerRequest
                    {
                        LobbyCode = lobbyCode,
                        TargetUsername = username,
                        RequestorUsername = "SYSTEM"
                    };
                    await lobbyLogic.KickPlayerAsync(req);
                }
            });
        }
    }
}