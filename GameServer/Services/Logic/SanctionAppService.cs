using GameServer.Repositories;
using log4net;
using System;
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
                if (player == null) return;

                var playerWithStats = await _repository.GetPlayerWithStatsByIdAsync(player.IdPlayer);

                if (playerWithStats != null && playerWithStats.PlayerStat != null)
                {
                    playerWithStats.PlayerStat.KicksReceived++;
                    int kicks = playerWithStats.PlayerStat.KicksReceived;

                    if (kicks >= 10 || kicks % 3 == 0)
                    {
                        var game = await _repository.GetGameByLobbyCodeAsync(lobbyCode);
                        int gameId = game != null ? game.IdGame : 0;

                        var sanction = new Sanction
                        {
                            Account_IdAccount = (int)player.Account_IdAccount,
                            Game_IdGame = gameId,
                            StartDate = DateTime.Now,
                            Reason = $"{reason} (Acumulación #{kicks})"
                        };

                        if (kicks >= 10)
                        {
                            sanction.SanctionType = 2;
                            sanction.EndDate = DateTime.Now.AddYears(999);
                        }
                        else
                        {
                            sanction.SanctionType = 1;
                            sanction.EndDate = DateTime.Now.AddDays(1);
                        }

                        _repository.AddSanction(sanction);
                    }

                    await _repository.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error aplicando sanción a {username}", ex);
            }
        }
    }
}