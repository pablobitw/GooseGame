using GameServer.DTOs.Lobby;
using GameServer.Helpers;
using GameServer.Repositories;
using GameServer;
using log4net;
using System;
using System.Data.Entity.Core;
using System.Data.SqlClient;
using System.Threading.Tasks;

namespace GameServer.Services.Logic
{
    public class SanctionAppService : IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(SanctionAppService));
        private readonly IGameplayRepository _repository;
        private bool _disposed = false;

        public SanctionAppService(IGameplayRepository repository = null)
        {
            _repository = repository ?? new GameplayRepository();
        }

        public async Task ProcessKickAsync(string username, string lobbyCode, string reason, string source)
        {
            Log.InfoFormat("[SanctionHub] Procesando kick para {0}. Origen: {1}. Razón: {2}", username, source, reason);

            try
            {
                var player = await _repository.GetPlayerByUsernameAsync(username);
                if (player != null)
                {
                    player.KickCount++;

                    var stats = await _repository.GetPlayerWithStatsByIdAsync(player.IdPlayer);
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

                    // CORRECCIÓN: Usamos .HasValue y .Value para manejar el tipo int?
                    if (player.Account_IdAccount.HasValue && player.Account_IdAccount.Value != 0)
                    {
                        var game = await _repository.GetGameByLobbyCodeAsync(lobbyCode);

                        // CORRECCIÓN: Casting explícito o valor por defecto para gameId
                        int gameId = 0;
                        if (game != null)
                        {
                            gameId = game.IdGame;
                        }

                        if (gameId == 0 && player.GameIdGame.HasValue)
                        {
                            gameId = player.GameIdGame.Value;
                        }

                        if (gameId != 0)
                        {
                            var sanction = new Sanction
                            {
                                // CORRECCIÓN: Asignación segura de int? a int
                                Account_IdAccount = player.Account_IdAccount.Value,
                                Game_IdGame = gameId,
                                StartDate = DateTime.UtcNow,
                                Reason = $"{source}: {reason}",
                                SanctionType = isBanApplied ? 2 : 1,
                                EndDate = isBanApplied ? DateTime.UtcNow.AddYears(1) : DateTime.UtcNow
                            };
                            _repository.AddSanction(sanction);
                        }
                    }
                    else
                    {
                        Log.InfoFormat("[SanctionHub] Jugador invitado {0} expulsado. Se omite historial de sanciones.", username);
                    }

                    await ProcessGameExitStats(player);

                    player.GameIdGame = null;
                    player.TurnsSkipped = 0;

                    await _repository.SaveChangesAsync();

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

        private async Task ProcessGameExitStats(Player player)
        {
            if (player.GameIdGame.HasValue)
            {
                var stats = await _repository.GetPlayerWithStatsByIdAsync(player.IdPlayer);
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
                catch (Exception ex)
                {
                    Log.Warn($"[SanctionHub] No se pudo notificar al cliente {username}.", ex);
                }
                finally
                {
                    ConnectionManager.UnregisterGameplayClient(username);
                }
            }

            Task.Run(async () =>
            {
                try
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
                }
                catch (Exception ex)
                {
                    Log.Error($"[SanctionHub] Error sacando a {username} del lobby tras kick.", ex);
                }
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
                if (disposing)
                {
                    _repository?.Dispose();
                }
                _disposed = true;
            }
        }
    }
}