using GameServer.Helpers;
using GameServer.Interfaces;
using GameServer;
using System;
using System.Security.Cryptography;
using System.Linq;
using System.ServiceModel;

namespace GameServer.Services.Common
{

    public interface ILobbyConnectionManager
    {
        ILobbyServiceCallback GetClient(string username);
        void RegisterClient(string username, ILobbyServiceCallback callback);
        void UnregisterClient(string username);
    }

    public class LobbyConnectionManagerWrapper : ILobbyConnectionManager
    {
        public ILobbyServiceCallback GetClient(string username) => ConnectionManager.GetLobbyClient(username);
        public void RegisterClient(string username, ILobbyServiceCallback callback) => ConnectionManager.RegisterLobbyClient(username, callback);
        public void UnregisterClient(string username) => ConnectionManager.UnregisterLobbyClient(username);
    }

    public interface IWcfContext
    {
        T GetCallbackChannel<T>();
    }

    public class WcfContextWrapper : IWcfContext
    {
        public T GetCallbackChannel<T>()
        {
            return OperationContext.Current != null
                ? OperationContext.Current.GetCallbackChannel<T>()
                : default(T);
        }
    }


    public interface IGameMonitor
    {
        void StartMonitoring(int gameId);
        void StopMonitoring(int gameId);
        void UpdateActivity(int gameId);
    }

    public class GameMonitorWrapper : IGameMonitor
    {
        public void StartMonitoring(int gameId) => GameManager.Instance.StartMonitoring(gameId);
        public void StopMonitoring(int gameId) => GameManager.Instance.StopMonitoring(gameId);
        public void UpdateActivity(int gameId) => GameManager.Instance.UpdateActivity(gameId);
    }


    public interface ILobbyCodeGenerator
    {
        string GenerateRandomString(int length);
    }

    public class LobbyCodeGenerator : ILobbyCodeGenerator
    {
        private const string Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        public string GenerateRandomString(int length)
        {
            byte[] randomBytes = new byte[length];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomBytes);
            }
            return new string(randomBytes.Select(b => Chars[b % Chars.Length]).ToArray());
        }
    }
}