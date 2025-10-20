using GameServer.Contracts;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;

namespace GameServer.Services
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple)]
    public class ChatService : GameServer.Contracts.IChatService
    {
        private readonly ConcurrentDictionary<string, IChatCallback> _clients
            = new ConcurrentDictionary<string, IChatCallback>();

        public void JoinChat(string username)
        {
            IChatCallback callback = OperationContext.Current.GetCallbackChannel<IChatCallback>();
            _clients.AddOrUpdate(username, callback, (key, oldValue) => callback);
            Console.WriteLine($"{username} joined the chat.");
        }

        // --- ADD THIS METHOD ---
        public void Leave(string username)
        {
            if (_clients.TryRemove(username, out _))
            {
                Console.WriteLine($"{username} left the chat.");
            }
        }

        public void SendMessage(string username, string message)
        {
            foreach (var client in _clients)
            {
                
                if (client.Key != username)
                {
                    try
                    {
                        client.Value.ReceiveMessage(username, message);
                    }
                    catch
                    {
                       
                        Leave(client.Key);
                    }
                }
            }
        }
    }
}