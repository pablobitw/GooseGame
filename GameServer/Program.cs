using System;
using System.ServiceModel;
using GameServer.Services;


namespace GameServer
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.Title = "Goose Game Server";

            ServiceHost gameHost = new ServiceHost(typeof(GameService));

            ServiceHost chatHost = new ServiceHost(typeof(ChatService));

            try
            {
                gameHost.Open();
                chatHost.Open();

                Console.WriteLine("GooseGame Server is running.");
                Console.WriteLine("Chat service is online.");
                Console.WriteLine("Press <Enter> to stop both services.");
                Console.ReadLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Something is wrong: " + ex.Message);
                Console.ReadLine();
            }
            finally
            {
                if (gameHost.State == CommunicationState.Opened)
                    gameHost.Close();

                if (chatHost.State == CommunicationState.Opened)
                    chatHost.Close();
            }

            Console.WriteLine("Closing server...");
        }
    }
}
