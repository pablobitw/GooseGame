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
                if (!ValidatePlayer(player, username)) return;

                var game = await _repository.GetGameByLobbyCodeAsync(lobbyCode);
                if (!ValidateGame(game, lobbyCode)) return;

                var playerWithStats = await _repository.GetPlayerWithStatsByIdAsync(player.IdPlayer);
                if (playerWithStats?.PlayerStat == null) return;

                playerWithStats.PlayerStat.KicksReceived++;
                int kicks = playerWithStats.PlayerStat.KicksReceived;

                if (kicks >= 10 || kicks % 3 == 0)
                {
                    ApplySanction(player, game, reason, kicks);
                }

                await _repository.SaveChangesAsync();
            }
            catch (EntityException ex)
            {
                Log.ErrorFormat("[Sanction] Error de Entity Framework al sancionar a {0}", username, ex);
            }
            catch (SqlException ex)
            {
                Log.ErrorFormat("[Sanction] Error SQL al sancionar a {0}", username, ex);
            }
            catch (TimeoutException ex)
            {
                Log.ErrorFormat("[Sanction] Timeout al guardar sanción de {0}", username, ex);
            }
            catch (Exception ex)
            {
                Log.ErrorFormat("[Sanction] Error general al sancionar a {0}", username, ex);
            }
        }

        private bool ValidatePlayer(Player player, string username)
        {
            if (player == null)
            {
                Log.WarnFormat("[Sanction] Jugador no encontrado: {0}", username);
                return false;
            }

            if (player.Account_IdAccount == null)
            {
                Log.ErrorFormat("[Sanction] Error crítico: El jugador {0} no tiene Account_IdAccount. No se puede sancionar.", username);
                return false;
            }

            return true;
        }

        private bool ValidateGame(Game game, string lobbyCode)
        {
            if (game == null)
            {
                Log.ErrorFormat("[Sanction] No se pudo encontrar el juego {0}. La sanción requiere un ID de juego válido.", lobbyCode);
                return false;
            }
            return true;
        }

        private void ApplySanction(Player player, Game game, string reason, int kicks)
        {
            var sanction = new Sanction
            {
                Account_IdAccount = (int)player.Account_IdAccount,
                Game_IdGame = game.IdGame,
                StartDate = DateTime.UtcNow,
                Reason = string.Format("{0} (Acumulación #{1})", reason, kicks)
            };

            if (kicks >= 10)
            {
                sanction.SanctionType = 2;
                sanction.EndDate = DateTime.UtcNow.AddYears(999);
                Log.InfoFormat("[Sanction] BAN PERMANENTE (Tipo 2) aplicado a {0}.", player.Username);
            }
            else
            {
                sanction.SanctionType = 1;
                sanction.EndDate = DateTime.UtcNow.AddDays(1);
                Log.InfoFormat("[Sanction] SUSPENSIÓN (Tipo 1) aplicada a {0}.", player.Username);
            }

            _repository.AddSanction(sanction);
        }
    }
}
