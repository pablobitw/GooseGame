using System;
using System.Data.Entity.Core;
using System.Data.SqlClient;
using System.ServiceModel;

namespace GameServer.Faults
{
    public static class ExceptionManager
    {
        public static FaultException<ServiceFault> Map(Exception ex)
        {
            if (ex is EntityException && ex.InnerException is SqlException)
            {
                ex = ex.InnerException;
            }

            if (ex is SqlException sqlEx)
            {
                switch (sqlEx.Number)
                {
                    case 547: 
                        return Faults.Create(Faults.Types.BusinessRule, "Error_DataIntegrity", "No se puede eliminar porque tiene datos relacionados.");

                    case 2601:
                    case 2627:
                        return Faults.Create(Faults.Types.BusinessRule, "Error_DuplicateRecord", "El registro ya existe.");

                    case 1205:
                        return Faults.Create(Faults.Types.Infrastructure, "Error_ServerBusy", "El servidor está saturado (Deadlock), intente de nuevo.");

                    case 4060:
                    case 18456:
                    case 2:
                    case 53:
                        return Faults.Create(Faults.Types.Infrastructure, "Error_DatabaseDown", "No se puede conectar a la base de datos.");

                    default:
                        return Faults.Create(Faults.Types.Infrastructure, "Error_DatabaseGeneric", $"Error SQL: {sqlEx.Number}");
                }
            }

            if (ex is NullReferenceException)
            {
                return Faults.Create(Faults.Types.Critical, "Error_InternalData", "Se encontraron datos corruptos en el servidor.");
            }

            if (ex is TimeoutException)
            {
                return Faults.Create(Faults.Types.Infrastructure, "Error_ServerTimeout", "La operación interna tardó demasiado.");
            }

            return Faults.Create(Faults.Types.Critical, "Error_Unknown", ex.Message);
        }
    }
}