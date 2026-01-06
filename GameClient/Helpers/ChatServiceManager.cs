using System;
using System.ServiceModel;
using System.Threading.Tasks;
using GameClient.ChatServiceReference; 

namespace GameClient.Helpers
{
    public class ChatServiceManager : IChatServiceCallback
    {
        private ChatServiceClient _proxy;
        private InstanceContext _context;

        public event Action<ChatMessageDto> MessageReceived;

        public ChatServiceManager()
        {
            _context = new InstanceContext(this);
            InitializeProxy();
        }

        private void InitializeProxy()
        {
            if (_proxy != null && _proxy.State == CommunicationState.Opened)
                return;

            _proxy = new ChatServiceClient(_context);
        }

        public async Task<ChatOperationResult> ConnectToChatAsync(string username, string lobbyCode)
        {
            try
            {
                InitializeProxy();

                var request = new JoinChatRequest
                {
                    Username = username,
                    LobbyCode = lobbyCode 
                };

                return await _proxy.JoinLobbyChatAsync(request);
            }
            catch (EndpointNotFoundException)
            {
                return ChatOperationResult.InternalError;
            }
            catch (Exception)
            {
                return ChatOperationResult.GeneralError;
            }
        }

        public async Task<ChatOperationResult> SendMessageAsync(string message, string username, string lobbyCode)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(message)) return ChatOperationResult.Success;

                InitializeProxy();

                var msgDto = new ChatMessageDto
                {
                    Message = message,
                    Sender = username,
                    LobbyCode = lobbyCode, 
                    Timestamp = DateTime.Now, 
                    IsPrivate = false
                };

                return await _proxy.SendLobbyMessageAsync(msgDto);
            }
            catch (CommunicationException)
            {
                return ChatOperationResult.InternalError;
            }
            catch (Exception)
            {
                return ChatOperationResult.GeneralError;
            }
        }

        public async Task DisconnectAsync(string username, string lobbyCode)
        {
            if (_proxy == null) return;

            try
            {
                var request = new JoinChatRequest
                {
                    Username = username,
                    LobbyCode = lobbyCode 
                };

                await _proxy.LeaveLobbyChatAsync(request);
                _proxy.Close();
            }
            catch
            {
                _proxy.Abort();
            }
        }

        public void ReceiveMessage(ChatMessageDto message)
        {
            MessageReceived?.Invoke(message);
        }
    }
}