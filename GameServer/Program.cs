using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameServer
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.Title = "Goose Game Server";

            Console.WriteLine("Iniciando servidor del Juego de la Oca...");
            Console.WriteLine("---------------------------------------------");
            Console.WriteLine("El servidor está en espera.");

            // aquí irá el código para iniciar el servicio WCF

            Console.WriteLine("Presiona <Enter> para detener el servidor.");
            Console.ReadLine(); // aqui se detiene el programa

            Console.WriteLine("Cerrando servidor...");
        }
    }
}