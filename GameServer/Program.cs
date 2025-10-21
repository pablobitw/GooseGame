using System;
using System.ServiceModel;
using GameServer.Services;
using log4net;

namespace GameServer
{
    internal class Program
    {
        
        private static readonly ILog Log = LogManager.GetLogger(typeof(Program));

        static void Main(string[] args)
        {
            

          
            Log.Info("========================================");
            Log.Info("Initializing GooseGame Server...");
            Log.Info("========================================");

            try
            {
                using (ServiceHost gameServiceHost = new ServiceHost(typeof(GameService)))
                using (ServiceHost chatServiceHost = new ServiceHost(typeof(ChatService)))
                {
                    // abrir el servicio de juego
                    gameServiceHost.Open();
                    Log.Info("GameService is running and listening on:");
                    foreach (var endpoint in gameServiceHost.Description.Endpoints)
                    {
                        Log.Info($"-> {endpoint.Address}");
                    }

                    // abrir el servicio de chat
                    chatServiceHost.Open();
                    Log.Info("ChatService is running and listening on:");
                    foreach (var endpoint in chatServiceHost.Description.Endpoints)
                    {
                        Log.Info($"-> {endpoint.Address}");
                    }

                    Log.Info("========================================");
                    Log.Warn("Server is fully operational. Press [Enter] to stop."); 
                    Log.Info("========================================");

                    Console.ReadLine(); // mantiene el servidor vivo

                    Log.Info("Server shutdown requested.");
                }
            }
            catch (Exception ex)
            {
                // es para capturar errores fatales
                Log.Fatal("A critical error occurred while starting the server.", ex);
                Console.WriteLine("A fatal error occurred. Press [Enter] to exit.");
                Console.ReadLine();
            }
            finally
            {
                Log.Info("Server shutdown complete.");
            }
        }
    }
}