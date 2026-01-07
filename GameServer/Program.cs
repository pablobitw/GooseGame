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
        private const string PressEnterToExit = "\nPresiona [Enter] para salir...";

        static void Main(string[] args)
        {
            Console.Title = "GooseGame Server";

            Log.Info($"{LogSeparator}\nInitializing GooseGame Server...\n{LogSeparator}");

            try
            {
                using (ServiceHost gameServiceHost = new ServiceHost(typeof(AuthService)))
                using (ServiceHost chatServiceHost = new ServiceHost(typeof(ChatService)))
                using (ServiceHost lobbyServiceHost = new ServiceHost(typeof(LobbyService)))
                using (ServiceHost gameplayServiceHost = new ServiceHost(typeof(GameplayService)))
                using (ServiceHost userProfileServiceHost = new ServiceHost(typeof(UserProfileService)))
                using (ServiceHost friendshipServiceHost = new ServiceHost(typeof(FriendshipService)))
                using (ServiceHost leaderboardServiceHost = new ServiceHost(typeof(LeaderboardService)))
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
                Console.WriteLine(PressEnterToExit);
                Console.ReadLine();
            }
            catch (InvalidOperationException ex)
            {
                Log.Fatal("❌ CRITICAL ERROR: Configuration invalid (InvalidOperationException).", ex);
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n[ERROR] Configuración inválida (Revisa App.config o cadenas de conexión).");
                Console.WriteLine($"Detalle: {ex.Message}");
                Console.ResetColor();
                Console.WriteLine(PressEnterToExit);
                Console.ReadLine();
            }
            catch (CommunicationException ex)
            {
                Log.Fatal("❌ CRITICAL ERROR: Communication error (Port in use?).", ex);
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n[ERROR] Error de comunicación (¿Puerto en uso o dirección incorrecta?).");
                Console.WriteLine($"Detalle: {ex.Message}");
                Console.ResetColor();
                Console.WriteLine(PressEnterToExit);
                Console.ReadLine();
            }
            catch (TimeoutException ex)
            {
                Log.Fatal("❌ CRITICAL ERROR: Service start timeout.", ex);
                Console.WriteLine("\n[ERROR] Timeout al iniciar servicios.");
                Console.WriteLine($"Detalle: {ex.Message}");
                Console.WriteLine(PressEnterToExit);
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
                Console.WriteLine(PressEnterToExit);
                Console.ReadLine();
            }
            finally
            {
                Log.Info("Server shutdown complete.");
            }
        }

        private static void LogServices(ServiceHost host, string serviceName)
        {
            Log.InfoFormat("{0} is running and listening on:", serviceName);
            foreach (var endpoint in host.Description.Endpoints)
            {
                Log.InfoFormat("-> {0}", endpoint.Address);
            }
        }
    }
}