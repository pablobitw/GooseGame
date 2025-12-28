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
        private readonly GameplayRepository _repository;

        public SanctionAppService(GameplayRepository repository)
        {
            _repository = repository;
        }

        public async Task ProcessKickSanctionAsync(string username, string lobbyCode, string reason)
        {
            try
            {
                var player = await _repository.GetPlayerByUsernameAsync(username);
                if (player == null)
                {
                    Log.Warn($"[Sanction] Jugador no encontrado: {username}");
                    return;
                }

                if (player.Account_IdAccount == null)
                {
                    Log.Error($"[Sanction] Error crítico: El jugador {username} no tiene Account_IdAccount. No se puede sancionar.");
                    return;
                }

                var game = await _repository.GetGameByLobbyCodeAsync(lobbyCode);
                if (game == null)
                {
                    Log.Error($"[Sanction] No se pudo encontrar el juego {lobbyCode}. La sanción requiere un ID de juego válido.");
                    return;
                }

                var playerWithStats = await _repository.GetPlayerWithStatsByIdAsync(player.IdPlayer);

                if (playerWithStats != null && playerWithStats.PlayerStat != null)
                {
                    playerWithStats.PlayerStat.KicksReceived++;
                    int kicks = playerWithStats.PlayerStat.KicksReceived;

                    if (kicks >= 10 || kicks % 3 == 0)
                    {
                        var sanction = new Sanction
                        {
                            Account_IdAccount = (int)player.Account_IdAccount,

                            Game_IdGame = game.IdGame,

                            StartDate = DateTime.Now,
                            Reason = $"{reason} (Acumulación #{kicks})"
                        };

                        if (kicks >= 10)
                        {
                            sanction.SanctionType = 2;
                            sanction.EndDate = DateTime.Now.AddYears(999);
                            Log.Info($"[Sanction] BAN PERMANENTE (Tipo 2) aplicado a {username}.");
                        }
                        else
                        {
                            sanction.SanctionType = 1;
                            sanction.EndDate = DateTime.Now.AddDays(1);
                            Log.Info($"[Sanction] SUSPENSIÓN (Tipo 1) aplicada a {username}.");
                        }

                        _repository.AddSanction(sanction);
                    }

                    await _repository.SaveChangesAsync();
                }
            }
            catch (EntityException ex)
            {
                Log.Error($"[Sanction] Error de Entity Framework al sancionar a {username}", ex);
            }
            catch (SqlException ex)
            {
                Log.Error($"[Sanction] Error SQL al sancionar a {username}", ex);
            }
            catch (TimeoutException ex)
            {
                Log.Error($"[Sanction] Timeout al guardar sanción de {username}", ex);
            }
            catch (Exception ex)
            {
                Log.Error($"[Sanction] Error general al sancionar a {username}", ex);
            }
        }
    }
}