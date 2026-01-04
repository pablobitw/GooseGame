using GameServer.Repositories;
using GameServer.Services.Logic;
using log4net;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GameServer.Helpers
{
    public class GameManager
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(GameManager));
        private static GameManager _instance;
        private static readonly object _lock = new object();

        private readonly ConcurrentDictionary<int, DateTime> _activeGames = new ConcurrentDictionary<int, DateTime>();

        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        private const int CheckIntervalMs = 1000; 
        private const int TurnTimeLimitSeconds = 20; 

        private GameManager()
        {
            Task.Run(() => GameLoop(_cancellationTokenSource.Token));
        }

        public static GameManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                            _instance = new GameManager();
                    }
                }
                return _instance;
            }
        }


        public void StartMonitoring(int gameId)
        {
            _activeGames.AddOrUpdate(gameId, DateTime.UtcNow, (key, oldValue) => DateTime.UtcNow);
            Log.Info($"[GameManager] Monitoreo iniciado para partida {gameId}");
        }

        public void StopMonitoring(int gameId)
        {
            _activeGames.TryRemove(gameId, out _);
            Log.Info($"[GameManager] Monitoreo detenido para partida {gameId}");
        }

        public void UpdateActivity(int gameId)
        {
            if (_activeGames.ContainsKey(gameId))
            {
                _activeGames[gameId] = DateTime.UtcNow;
            }
        }

        private async Task GameLoop(CancellationToken token)
        {
            Log.Info("[GameManager] Game Loop de servidor iniciado.");

            while (!token.IsCancellationRequested)
            {
                try
                {
                    await CheckAllGamesAsync();
                }
                catch (Exception ex)
                {
                    Log.Error("[GameManager] Error crítico en el ciclo de juego", ex);
                }

                await Task.Delay(CheckIntervalMs, token);
            }
        }

        private async Task CheckAllGamesAsync()
        {
            var now = DateTime.UtcNow;
            var gamesIds = _activeGames.Keys.ToArray();

            foreach (var gameId in gamesIds)
            {
                if (_activeGames.TryGetValue(gameId, out DateTime lastActivity))
                {
                    if ((now - lastActivity).TotalSeconds > TurnTimeLimitSeconds)
                    {
                        Log.Info($"[GameManager] TIMEOUT detectado en partida {gameId}. Forzando cambio de turno...");

                        UpdateActivity(gameId);

                        await ExecuteServerTimeout(gameId);
                    }
                }
            }
        }

        private async Task ExecuteServerTimeout(int gameId)
        {
            try
            {
                using (var repository = new GameplayRepository())
                {
                    var logicService = new GameplayAppService(repository);

                   
                    await logicService.ProcessAfkTimeout(gameId);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[GameManager] Error al ejecutar timeout forzado en partida {gameId}", ex);
            }
        }
    }
}