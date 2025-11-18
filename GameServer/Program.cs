using System;
using System.ServiceModel;
using GameServer.Services;
using log4net;

namespace GameServer
{
    internal static class Program
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Program));
        private const string LogSeparator = "===============================";

        static void Main(string[] args)
        {
            Log.Info(LogSeparator);
            Log.Info("Initializing GooseGame Server...");
            Log.Info(LogSeparator);

            try
            {
                using (ServiceHost gameServiceHost = new ServiceHost(typeof(GameService)))
                using (ServiceHost chatServiceHost = new ServiceHost(typeof(ChatService)))
                using (ServiceHost lobbyServiceHost = new ServiceHost(typeof(LobbyService)))
                using (ServiceHost gameplayServiceHost = new ServiceHost(typeof(GameplayService)))   // 🔥 AGREGADO
                {
                    gameServiceHost.Open();
                    Log.Info("GameService is running and listening on:");
                    foreach (var endpoint in gameServiceHost.Description.Endpoints)
                        Log.Info($"-> {endpoint.Address}");

                    chatServiceHost.Open();
                    Log.Info("ChatService is running and listening on:");
                    foreach (var endpoint in chatServiceHost.Description.Endpoints)
                        Log.Info($"-> {endpoint.Address}");

                    lobbyServiceHost.Open();
                    Log.Info("LobbyService is running and listening on:");
                    foreach (var endpoint in lobbyServiceHost.Description.Endpoints)
                        Log.Info($"-> {endpoint.Address}");

                    // 🔥 NUEVO BLOQUE PARA GAMEPLAY SERVICE
                    gameplayServiceHost.Open();
                    Log.Info("GameplayService is running and listening on:");
                    foreach (var endpoint in gameplayServiceHost.Description.Endpoints)
                        Log.Info($"-> {endpoint.Address}");
                    // 🔥 FIN

                    Log.Info(LogSeparator);
                    Log.Warn("Server is fully operational. Press [Enter] to stop.");
                    Log.Info(LogSeparator);

                    Console.ReadLine();
                    Log.Info("Server shutdown requested.");
                }
            }
            catch (InvalidOperationException ex)
            {
                Log.Fatal("A critical error occurred starting WCF services (InvalidOperationException). Check configuration.", ex);
                Console.WriteLine("A fatal configuration error occurred. Press [Enter] to exit.");
                Console.ReadLine();
            }
            catch (CommunicationException ex)
            {
                Log.Fatal($"A critical error occurred starting WCF services (CommunicationException). Is the port in use?", ex);
                Console.WriteLine("A fatal communication error occurred. Press [Enter] to exit.");
                Console.ReadLine();
            }
            catch (TimeoutException ex)
            {
                Log.Fatal("A critical error occurred starting WCF services (TimeoutException).", ex);
                Console.WriteLine("A fatal timeout error occurred. Press [Enter] to exit.");
                Console.ReadLine();
            }
            catch (Exception ex)
            {
                Log.Fatal("An unexpected critical error occurred while starting the server.", ex);
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
