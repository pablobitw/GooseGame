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
            Console.Title = "GooseGame Server";

            Log.Info(LogSeparator);
            Log.Info("Initializing GooseGame Server...");
            Log.Info(LogSeparator);

            try
            {
                using (ServiceHost gameServiceHost = new ServiceHost(typeof(GameService)))
                using (ServiceHost chatServiceHost = new ServiceHost(typeof(ChatService)))
                using (ServiceHost lobbyServiceHost = new ServiceHost(typeof(LobbyService)))
                using (ServiceHost gameplayServiceHost = new ServiceHost(typeof(GameplayService)))
                using (ServiceHost userProfileServiceHost = new ServiceHost(typeof(UserProfileService)))
                using (ServiceHost friendshipServiceHost = new ServiceHost(typeof(FriendshipService)))
                using (ServiceHost leaderboardServiceHost = new ServiceHost(typeof(LeaderboardService))) // <--- AGREGADO
                {
                    gameServiceHost.Open();
                    LogServices(gameServiceHost, "GameService");

                    chatServiceHost.Open();
                    LogServices(chatServiceHost, "ChatService");

                    lobbyServiceHost.Open();
                    LogServices(lobbyServiceHost, "LobbyService");

                    gameplayServiceHost.Open();
                    LogServices(gameplayServiceHost, "GameplayService");

                    userProfileServiceHost.Open();
                    LogServices(userProfileServiceHost, "UserProfileService");

                    friendshipServiceHost.Open();
                    LogServices(friendshipServiceHost, "FriendshipService");

                    leaderboardServiceHost.Open();
                    LogServices(leaderboardServiceHost, "LeaderboardService"); 

                    Log.Info(LogSeparator);
                    Log.Warn(" SERVER IS FULLY OPERATIONAL. Press [Enter] to stop.");
                    Log.Info(LogSeparator);

                    Console.ReadLine();

                    Log.Info("Stopping server...");
                }
            }
            catch (AddressAccessDeniedException ex)
            {
                Log.Fatal("❌ ERROR CRÍTICO DE PERMISOS: No se puede abrir el puerto.", ex);
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n[ERROR] ACCESO DENEGADO. Necesitas ejecutar Visual Studio como ADMINISTRADOR.");
                Console.WriteLine($"Detalle: {ex.Message}");
                Console.ResetColor();
                Console.WriteLine("\nPresiona [Enter] para salir...");
                Console.ReadLine();
            }
            catch (InvalidOperationException ex)
            {
                Log.Fatal("❌ CRITICAL ERROR: Configuration invalid (InvalidOperationException).", ex);
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n[ERROR] Configuración inválida (Revisa App.config o cadenas de conexión).");
                Console.WriteLine($"Detalle: {ex.Message}");
                Console.ResetColor();
                Console.WriteLine("\nPresiona [Enter] para salir...");
                Console.ReadLine();
            }
            catch (CommunicationException ex)
            {
                Log.Fatal("❌ CRITICAL ERROR: Communication error (Port in use?).", ex);
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n[ERROR] Error de comunicación (¿Puerto en uso o dirección incorrecta?).");
                Console.WriteLine($"Detalle: {ex.Message}");
                Console.ResetColor();
                Console.WriteLine("\nPresiona [Enter] para salir...");
                Console.ReadLine();
            }
            catch (TimeoutException ex)
            {
                Log.Fatal("❌ CRITICAL ERROR: Service start timeout.", ex);
                Console.WriteLine("\n[ERROR] Timeout al iniciar servicios.");
                Console.WriteLine($"Detalle: {ex.Message}");
                Console.WriteLine("\nPresiona [Enter] para salir...");
                Console.ReadLine();
            }
            catch (Exception ex)
            {
                Log.Fatal("❌ UNEXPECTED CRITICAL ERROR (Crash).", ex);
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n[CRASH] Ocurrió un error inesperado que detuvo el servidor.");
                Console.WriteLine($"Tipo: {ex.GetType().Name}");
                Console.WriteLine($"Mensaje: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                Console.ResetColor();
                Console.WriteLine("\nPresiona [Enter] para salir...");
                Console.ReadLine();
            }
            finally
            {
                Log.Info("Server shutdown complete.");
            }
        }

        private static void LogServices(ServiceHost host, string serviceName)
        {
            Log.Info($"{serviceName} is running and listening on:");
            foreach (var endpoint in host.Description.Endpoints)
            {
                Log.Info($"-> {endpoint.Address}");
            }
        }
    }
}