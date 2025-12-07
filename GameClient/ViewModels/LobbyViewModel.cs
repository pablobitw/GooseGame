using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Windows.Threading;
using GameClient.ChatServiceReference;
using GameClient.LobbyServiceReference;
using GameClient.Helpers;

namespace GameClient.ViewModels
{
    public class LobbyViewModel : IChatServiceCallback
    {
        private LobbyServiceClient _lobbyClient;
        private ChatServiceClient _chatClient;
        private DispatcherTimer _pollingTimer;

        public string Username { get; private set; }
        public string LobbyCode { get; private set; }
        public bool IsHost { get; private set; }
        public int BoardId { get; set; } = 1;
        public int MaxPlayers { get; set; } = 4;
        public bool IsPublic { get; set; } = true;
        public bool IsLobbyCreated { get; private set; } = false;

        public event Action<string, string> MessageReceived;
        public event Action<LobbyStateDTO> StateUpdated;
        public event Action GameStarted;

        public LobbyViewModel(string username)
        {
            Username = username;
            IsHost = true;
            InitializeClients();
        }

        public LobbyViewModel(string username, string lobbyCode, JoinLobbyResultDTO joinData)
        {
            Username = username;
            LobbyCode = lobbyCode;
            IsHost = false;
            IsLobbyCreated = true;

            BoardId = joinData.BoardId;
            MaxPlayers = joinData.MaxPlayers;
            IsPublic = joinData.IsPublic;

            InitializeClients();
            InitializeTimer();
            ConnectToChat();
        }

        private void InitializeClients()
        {
            _lobbyClient = new LobbyServiceClient();
        }

        public async Task<LobbyCreationResultDTO> CreateLobbyAsync()
        {
            var settings = new LobbySettingsDTO
            {
                IsPublic = IsPublic,
                MaxPlayers = MaxPlayers,
                BoardId = BoardId
            };

            var request = new CreateLobbyRequest
            {
                Settings = settings,
                HostUsername = Username
            };

            var result = await _lobbyClient.CreateLobbyAsync(request);

            if (result.Success)
            {
                LobbyCode = result.LobbyCode;
                IsLobbyCreated = true;
                InitializeTimer();
                ConnectToChat();
            }

            return result;
        }

        public async Task DisbandLobbyAsync()
        {
            _pollingTimer?.Stop();
            if (IsLobbyCreated && IsHost)
            {
                await _lobbyClient.DisbandLobbyAsync(Username);
            }
            await CloseClientsAsync();
        }

        public async Task LeaveLobbyAsync()
        {
            _pollingTimer?.Stop();

            if (_chatClient != null && _chatClient.State == CommunicationState.Opened)
            {
                try
                {
                    var request = new JoinChatRequest { Username = Username, LobbyCode = LobbyCode };
                    await _chatClient.LeaveLobbyChatAsync(request);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[LobbyVM] Error al salir del chat: {ex.Message}");
                    _chatClient.Abort();
                }
            }

            await CloseClientsAsync();
        }

        public async Task<bool> StartGameAsync()
        {
            bool started = await _lobbyClient.StartGameAsync(LobbyCode);
            if (started) _pollingTimer?.Stop();
            return started;
        }

        private void ConnectToChat()
        {
            try
            {
                var context = new InstanceContext(this);
                _chatClient = new ChatServiceClient(context);

                var request = new JoinChatRequest { Username = Username, LobbyCode = LobbyCode };
                _chatClient.JoinLobbyChat(request);
            }
            catch (Exception ex)
            {
                MessageReceived?.Invoke("[Sistema]", "Error chat: " + ex.Message);
                Console.WriteLine($"[LobbyVM] Error conectando al chat: {ex.Message}");
            }
        }

        public void SendChatMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message) || _chatClient == null) return;

            try
            {
                var dto = new ChatMessageDto
                {
                    Sender = Username,
                    LobbyCode = LobbyCode,
                    Message = message
                };
                _chatClient.SendLobbyMessage(dto);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LobbyVM] Error enviando mensaje: {ex.Message}");
            }
        }

        public void ReceiveMessage(string username, string message)
        {
            MessageReceived?.Invoke(username, message);
        }

        private void InitializeTimer()
        {
            _pollingTimer = new DispatcherTimer();
            _pollingTimer.Interval = TimeSpan.FromSeconds(3);
            _pollingTimer.Tick += async (s, e) => await PollLobbyState();
            _pollingTimer.Start();
        }

        public void StopTimer() => _pollingTimer?.Stop();

        private async Task PollLobbyState()
        {
            _pollingTimer.Stop();
            bool shouldContinue = true;

            try
            {
                if (_lobbyClient.State == CommunicationState.Faulted || _lobbyClient.State == CommunicationState.Closed)
                    _lobbyClient = new LobbyServiceClient();

                var state = await _lobbyClient.GetLobbyStateAsync(LobbyCode);

                if (state != null)
                {
                    if (state.IsGameStarted)
                    {
                        shouldContinue = false;
                        GameStarted?.Invoke();
                    }
                    else
                    {
                        if (!IsHost)
                        {
                            MaxPlayers = state.MaxPlayers;
                            BoardId = state.BoardId;
                            IsPublic = state.IsPublic;
                        }
                        StateUpdated?.Invoke(state);
                    }
                }
            }
            catch (EndpointNotFoundException ex)
            {
                Console.WriteLine($"[LobbyVM] Servidor no encontrado (Polling): {ex.Message}");
            }
            catch (CommunicationException ex)
            {
                Console.WriteLine($"[LobbyVM] Error de comunicación (Polling): {ex.Message}");
                _lobbyClient.Abort();
            }
            catch (TimeoutException ex)
            {
                Console.WriteLine($"[LobbyVM] Timeout (Polling): {ex.Message}");
            }
            finally
            {
                if (shouldContinue) _pollingTimer.Start();
            }
        }

        private async Task CloseClientsAsync()
        {
            if (_chatClient != null)
            {
                try
                {
                    if (_chatClient.State == CommunicationState.Opened) _chatClient.Close();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[LobbyVM] Error cerrando ChatClient: {ex.Message}");
                    _chatClient.Abort(); 
                }
            }

            if (_lobbyClient != null)
            {
                try
                {
                    if (_lobbyClient.State == CommunicationState.Opened) _lobbyClient.Close();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[LobbyVM] Error cerrando LobbyClient: {ex.Message}");
                    _lobbyClient.Abort();
                }
            }

            await Task.CompletedTask;
        }

        public void SendInvitation(string friendUsername)
        {
            try
            {
                FriendshipServiceManager.Instance.SendGameInvitation(friendUsername, this.LobbyCode);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LobbyVM] Error enviando invitación: {ex.Message}");
            }
        }

        public async Task LeaveOrDisbandAsync()
        {
            StopTimer();

            try
            {
                if (IsLobbyCreated && IsHost)
                {
                    await DisbandLobbyAsync();
                }
                else
                {
                    await LeaveLobbyAsync();
                }
            }
            catch (CommunicationException ex)
            {
                Console.WriteLine($"[LobbyVM] Error de red al salir/disolver: {ex.Message}");
            }
            catch (TimeoutException ex)
            {
                Console.WriteLine($"[LobbyVM] Timeout al salir/disolver: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LobbyVM] Error general al salir/disolver: {ex.Message}");
            }
            finally
            {
                await CloseClientsAsync();
            }
        }
    }
}