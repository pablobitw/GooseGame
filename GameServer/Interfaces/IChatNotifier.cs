namespace GameServer.Interfaces
{
    public interface IChatNotifier
    {
        void SendMessageToClient(string clientKey, string sender, string message);
    }
}