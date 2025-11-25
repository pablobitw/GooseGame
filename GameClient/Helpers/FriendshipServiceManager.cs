using System;
using System.Collections.Generic;
using System.ServiceModel;
using System.Threading.Tasks;
using GameClient.FriendshipServiceReference;

namespace GameClient.Helpers
{
    public class FriendshipServiceManager : IFriendshipServiceCallback
    {
        private FriendshipServiceClient _proxy;
        private readonly string _username;

        public event Action FriendListUpdated; 
        public event Action RequestReceived;  

        public FriendshipServiceManager(string username)
        {
            _username = username;
            InitializeConnection();
        }

        private void InitializeConnection()
        {
            try
            {
                var context = new InstanceContext(this);
                _proxy = new FriendshipServiceClient(context);
                _proxy.Connect(_username);
            }
            catch (CommunicationException)
            {
                DialogHelper.ShowError("No se pudo conectar al servicio de amigos.");
            }
        }

        public void Disconnect()
        {
            try
            {
                if (_proxy != null && _proxy.State == CommunicationState.Opened)
                {
                    _proxy.Disconnect(_username);
                    _proxy.Close();
                }
            }
            catch (Exception)
            {
                _proxy?.Abort();
            }
        }


        public void OnFriendRequestReceived()
        {
            RequestReceived?.Invoke();
        }

        public void OnFriendListUpdated()
        {
            FriendListUpdated?.Invoke();
        }


        public async Task<FriendDto[]> GetFriendListAsync()
        {
            try
            {
                return await _proxy.GetFriendListAsync(_username);
            }
            catch (CommunicationException)
            {
                return new FriendDto[0];
            }
        }

        public async Task<FriendDto[]> GetPendingRequestsAsync()
        {
            try
            {
                return await _proxy.GetPendingRequestsAsync(_username);
            }
            catch (CommunicationException)
            {
                return new FriendDto[0];
            }
        }

        public async Task<bool> SendFriendRequestAsync(string targetUser)
        {
            try
            {
                return await _proxy.SendFriendRequestAsync(_username, targetUser);
            }
            catch (CommunicationException)
            {
                DialogHelper.ShowError("Error de conexión al enviar solicitud.");
                return false;
            }
        }

        public async Task RespondToFriendRequestAsync(string requester, bool accept)
        {
            try
            {
                await _proxy.RespondToFriendRequestAsync(_username, requester, accept);
            }
            catch (CommunicationException)
            {
                DialogHelper.ShowError("Error de conexión al responder.");
            }
        }

        public async Task<bool> RemoveFriendAsync(string friendUsername)
        {
            try
            {
                return await _proxy.RemoveFriendAsync(_username, friendUsername);
            }
            catch (CommunicationException)
            {
                DialogHelper.ShowError("Error de conexión al eliminar amigo.");
                return false;
            }
        }
    }
}