using System;
using System.ServiceModel; // Importante: Añade esta referencia para WCF

namespace GameServer
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.Title = "Goose Game Server";

            // 'using' se asegura de que el servicio se cierre correctamente al final
            using (ServiceHost host = new ServiceHost(typeof(GameService)))
            {
                try
                {
                    // Esta línea inicia el servicio y lo pone a escuchar
                    host.Open();

                    Console.WriteLine("Servidor del Juego de la Oca iniciado y en línea.");
                    Console.WriteLine("Presiona <Enter> para detener el servidor.");
                    Console.ReadLine();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Ocurrió un error al iniciar el servidor: " + ex.Message);
                    Console.ReadLine();
                }
            }

            Console.WriteLine("Cerrando servidor...");
        }
    }
}