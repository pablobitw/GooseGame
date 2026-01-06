using GameServer.DTOs;
using GameServer.Interfaces;
using GameServer.Services.Logic;
using log4net;
using System;
using System.Collections.Generic;
using System.Data.Entity.Core;
using System.Data.SqlClient;
using System.ServiceModel;
using System.Threading.Tasks;

namespace GameServer.Services
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerCall, ConcurrencyMode = ConcurrencyMode.Multiple)]
    public class LeaderboardService : ILeaderboardService
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(LeaderboardService));
        private readonly LeaderboardAppService _logic;

        public LeaderboardService()
        {
            _logic = new LeaderboardAppService();
        }

        public async Task<List<LeaderboardDto>> GetGlobalLeaderboardAsync(string requestingUsername)
        {
            try
            {
                return await _logic.GetGlobalLeaderboardAsync(requestingUsername);
            }
            catch (SqlException ex)
            {
                Log.Error($"Error SQL obteniendo leaderboard para {requestingUsername}", ex);
                throw new FaultException<GameServiceFault>(
                    new GameServiceFault
                    {
                        ErrorType = GameServiceErrorType.DatabaseError,
                        Message = "Error de conexión con la base de datos.",
                        Details = ex.Message
                    },
                    new FaultReason("Error en Base de Datos SQL"));
            }
            catch (EntityException ex)
            {
                Log.Error($"Error EntityFramework obteniendo leaderboard para {requestingUsername}", ex);
                throw new FaultException<GameServiceFault>(
                    new GameServiceFault
                    {
                        ErrorType = GameServiceErrorType.DatabaseError,
                        Message = "Error accediendo a los datos del juego.",
                        Details = "Entity Framework Error"
                    },
                    new FaultReason("Error de Entidad"));
            }
            catch (TimeoutException ex)
            {
                Log.Error($"Timeout obteniendo leaderboard para {requestingUsername}", ex);
                throw new FaultException<GameServiceFault>(
                    new GameServiceFault
                    {
                        ErrorType = GameServiceErrorType.OperationTimeout,
                        Message = "La operación tardó demasiado tiempo."
                    },
                    new FaultReason("Timeout del Servidor"));
            }
            catch (Exception ex)
            {
                Log.Error($"Error inesperado (General) para {requestingUsername}", ex);
                throw new FaultException<GameServiceFault>(
                    new GameServiceFault
                    {
                        ErrorType = GameServiceErrorType.UnknownError,
                        Message = "Ocurrió un error inesperado en el servidor."
                    },
                    new FaultReason("Error Interno del Servidor"));
            }
        }
    }
}