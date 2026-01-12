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
                if (_chatClient != null)
                {
                    try { _chatClient.Abort(); } catch { }
                    _chatClient = null;
                }

                var context = new InstanceContext(this);
                _chatClient = new ChatServiceClient(context);

                var request = new JoinChatRequest
                {
                    Username = _username,
                    LobbyCode = _lobbyCode
                };

                _chatClient.JoinLobbyChat(request);
            }
            catch (FaultException<ServiceFault> fault)
            {
                string msg = fault.Detail.Message ?? "Error de conexión al chat.";
                _dispatcher.InvokeAsync(() => SystemMessage?.Invoke($"[Sistema] {msg}"));
            }
            catch (EndpointNotFoundException)
            {
                _dispatcher.InvokeAsync(() => SystemMessage?.Invoke("[Sistema] No se puede conectar al servicio de chat."));
            }
            catch (CommunicationException)
            {
                _dispatcher.InvokeAsync(() => SystemMessage?.Invoke("[Sistema] Error de comunicación con el chat."));
            }
            catch (Exception ex)
            {
                _dispatcher.InvokeAsync(() => SystemMessage?.Invoke($"[Sistema] Error interno: {ex.Message}"));
            }
        }

        public void SendMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            Task.Run(() =>
            {
                try
                {
                    EnsureConnection();

                    var dto = new ChatMessageDto
                    {
                        Sender = _username,
                        LobbyCode = _lobbyCode,
                        Message = message
                    };

                    _chatClient.SendLobbyMessage(dto);
                }
                catch (FaultException<ServiceFault> fault)
                {
                    _dispatcher.InvokeAsync(() => SystemMessage?.Invoke($"[Sistema] {fault.Detail.Message}"));
                }
                catch (Exception ex) when (ex is CommunicationException || ex is TimeoutException || ex is ObjectDisposedException)
                {
                    try
                    {
                        Connect();
                        var dto = new ChatMessageDto
                        {
                            Sender = _username,
                            LobbyCode = _lobbyCode,
                            Message = message
                        };
                        _chatClient.SendLobbyMessage(dto);
                    }
                    catch (Exception)
                    {
                        _dispatcher.InvokeAsync(() => SystemMessage?.Invoke("[Sistema] No se pudo enviar el mensaje (Red inestable)."));
                    }
                }
                catch (Exception)
                {
                    _dispatcher.InvokeAsync(() => SystemMessage?.Invoke("[Sistema] Error desconocido al enviar mensaje."));
                }
            });
        }

        private void EnsureConnection()
        {
            if (_chatClient == null || _chatClient.State == CommunicationState.Faulted || _chatClient.State == CommunicationState.Closed)
            {
                Connect();
            }
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
            if (_chatClient == null) return;

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
            finally
            {
                _chatClient = null;
            }
        }
    }
}