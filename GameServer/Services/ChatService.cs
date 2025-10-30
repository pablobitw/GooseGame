using GameServer.Contracts;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using log4net;

namespace GameServer.Services
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple)]
    public class ChatService : GameServer.Contracts.IChatService
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(ChatService));


        private readonly ConcurrentDictionary<string, IChatCallback> _clients
            = new ConcurrentDictionary<string, IChatCallback>();


        public void JoinChat(string username)
        {
            if (string.IsNullOrEmpty(username))
            {
                Log.Warn("A user tried to join with an empty username.");
            }
            else
            {
                IChatCallback callback = OperationContext.Current.GetCallbackChannel<IChatCallback>();

                _clients.AddOrUpdate(username, callback, (key, oldCallback) => {
                    Log.Warn($"User '{username}' already existed. Updating callback channel.");
                    return callback;
                });

                Log.Info($"User '{username}' joined the chat. Total clients: {_clients.Count}");
            }
        }

        public void Leave(string username)
        {
            if (string.IsNullOrEmpty(username))
            {
                Log.Warn("A user tried to leave with an empty username.");
            }
            else
            {
                if (_clients.TryRemove(username, out _))
                {
                    Log.Info($"User '{username}' left the chat. Total clients: {_clients.Count}");
                }
                else
                {
                    Log.Warn($"User '{username}' tried to leave but was not found in the list.");
                }
            }
        }

        public void SendMessage(string username, string message)
        {
            var formattedMessage = message.Trim();

            if (string.IsNullOrEmpty(formattedMessage))
            {
                Log.Warn($"User '{username}' sent an empty message.");
            }
            else
            {
                Log.Debug($"Received message from '{username}': {formattedMessage}");

                var clientsToRemove = new List<string>();

                foreach (var client in _clients.ToList())
                {
                    if (client.Key == username)
                    {
                        continue;
                    }

                    try
                    {
                        client.Value.ReceiveMessage(username, formattedMessage);
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"Failed to send message to '{client.Key}'. Marking for removal.", ex);
                        clientsToRemove.Add(client.Key);
                    }
                }

                foreach (var clientKeyToRemove in clientsToRemove)
                {
                    Leave(clientKeyToRemove);
                }
            }
        }
    }
}