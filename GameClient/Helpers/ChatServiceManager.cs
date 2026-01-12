using System;
using System.ServiceModel;
using System.Threading.Tasks;
using GameClient.ChatServiceReference;

namespace GameClient.Helpers
{
    public class ChatServiceManager : IChatServiceCallback
    {
        private ChatServiceClient _proxy;
        private readonly InstanceContext _context;

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

            try
            {
                if (_proxy != null)
                {
                    try { _proxy.Abort(); } catch { }
                }
                _proxy = new ChatServiceClient(_context);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ChatManager] Error initializing proxy: {ex.Message}");
            }
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

            catch (FaultException<ServiceFault>)
            {
                return ChatOperationResult.InternalError;
            }

            catch (EndpointNotFoundException)
            {
                return ChatOperationResult.InternalError;
            }
            catch (CommunicationException)
            {
                ForceInvalidateProxy();
                return ChatOperationResult.InternalError;
            }
            catch (TimeoutException)
            {
                return ChatOperationResult.InternalError;
            }
            catch (Exception)
            {
                return ChatOperationResult.InternalError;
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
            catch (FaultException<ServiceFault>)
            {
                return ChatOperationResult.InternalError;
            }
            catch (CommunicationException)
            {
                ForceInvalidateProxy();
                try
                {
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
                catch
                {
                    return ChatOperationResult.InternalError;
                }
            }
            catch (TimeoutException)
            {
                return ChatOperationResult.InternalError;
            }
            catch (Exception)
            {
                return ChatOperationResult.InternalError;
            }
        }

        public async Task DisconnectAsync(string username, string lobbyCode)
        {
            if (_proxy == null) return;

            try
            {
                if (_proxy.State == CommunicationState.Opened)
                {
                    var request = new JoinChatRequest
                    {
                        Username = username,
                        LobbyCode = lobbyCode
                    };

                    await _proxy.LeaveLobbyChatAsync(request);
                    _proxy.Close();
                }
                else
                {
                    _proxy.Abort();
                }
            }
            catch
            {
                _proxy.Abort();
            }
            finally
            {
                _proxy = null;
            }
        }

        public void ReceiveMessage(ChatMessageDto message)
        {
            MessageReceived?.Invoke(message);
        }

        private void ForceInvalidateProxy()
        {
            if (_proxy != null)
            {
                try { _proxy.Abort(); } catch { }
                _proxy = null;
            }
        }
    }
}