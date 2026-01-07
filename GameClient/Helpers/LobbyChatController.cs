using GameClient.ChatServiceReference;
using System;
using System.ServiceModel;
using System.Threading.Tasks; 
using System.Windows.Threading;

namespace GameClient.Helpers
{
    internal class LobbyChatController : IChatServiceCallback
    {
        private ChatServiceClient _chatClient;
        private readonly string _username;
        private readonly string _lobbyCode;
        private readonly Dispatcher _dispatcher;

        public event Action<string, string> MessageReceived;
        public event Action<string> SystemMessage;

        public LobbyChatController(string username, string lobbyCode, Dispatcher dispatcher)
        {
            _username = username;
            _lobbyCode = lobbyCode;
            _dispatcher = dispatcher;
        }

        public void Connect()
        {
            try
            {
                var context = new InstanceContext(this);
                _chatClient = new ChatServiceClient(context);

                var request = new JoinChatRequest
                {
                    Username = _username,
                    LobbyCode = _lobbyCode
                };

                _chatClient.JoinLobbyChat(request);
            }
            catch (Exception ex)
            {
                _dispatcher.Invoke(() => SystemMessage?.Invoke("Error conectando al chat: " + ex.Message));
            }
        }

        public void SendMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message) || _chatClient == null)
                return;

            Task.Run(() =>
            {
                try
                {
                    var dto = new ChatMessageDto
                    {
                        Sender = _username,
                        LobbyCode = _lobbyCode,
                        Message = message
                    };

                    _chatClient.SendLobbyMessage(dto);
                }
                catch (Exception ex)
                {
                    _dispatcher.Invoke(() => SystemMessage?.Invoke("Error enviando mensaje: " + ex.Message));
                }
            });
        }

        public void ReceiveMessage(ChatMessageDto message)
        {
            _dispatcher.InvokeAsync(() =>
            {
                MessageReceived?.Invoke(message.Sender, message.Message);
            });
        }

        public void Close()
        {
            if (_chatClient == null)
                return;

            try
            {
                if (_chatClient.State == CommunicationState.Opened)
                    _chatClient.Close();
                else
                    _chatClient.Abort();
            }
            catch
            {
                _chatClient.Abort();
            }
        }
    }
}