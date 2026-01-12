using GameServer.DTOs.Lobby;
using GameServer.Faults;
using GameServer.Helpers;
using GameServer.Interfaces;
using GameServer.Models;
using GameServer.Repositories;
using GameServer.Services.Common;
using log4net;
using System;
using System.Collections.Generic;
using System.Data.Entity.Core;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;

namespace GameServer.Services.Logic
{
    [ServiceBehavior(ConcurrencyMode = ConcurrencyMode.Multiple, InstanceContextMode = InstanceContextMode.PerCall)]
    public class LobbyAppService : ILobbyService
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(LobbyAppService));
        private readonly ILobbyRepository _repository;
        private readonly ILobbyConnectionManager _connectionManager;
        private readonly IWcfContext _wcfContext;
        private readonly IGameMonitor _gameMonitor;
        private readonly ILobbyCodeGenerator _codeGenerator;

        public LobbyAppService(
            ILobbyRepository repository,
            ILobbyConnectionManager connectionManager = null,
            IWcfContext wcfContext = null,
            IGameMonitor gameMonitor = null,
            ILobbyCodeGenerator codeGenerator = null)
        {
            if (repository == null) throw new ArgumentNullException(nameof(repository));
            _repository = repository;
            _connectionManager = connectionManager ?? new LobbyConnectionManagerWrapper();
            _wcfContext = wcfContext ?? new WcfContextWrapper();
            _gameMonitor = gameMonitor ?? new GameMonitorWrapper();
            _codeGenerator = codeGenerator ?? new LobbyCodeGenerator();
        }

        private void FireAndForgetNotification(List<string> usernames, Action<ILobbyServiceCallback> notificationAction)
        {
            if (usernames != null && usernames.Any())
            {
                var safeList = new List<string>(usernames);
                _ = Task.Run(() =>
                {
                    NotifyUsersSafe(safeList, notificationAction);
                });
            }
        }

        private void NotifyUsersSafe(List<string> usernames, Action<ILobbyServiceCallback> notificationAction)
        {
            foreach (var username in usernames)
            {
                var client = _connectionManager.GetClient(username);
                if (client != null)
                {
                    try
                    {
                        var channel = (ICommunicationObject)client;
                        if (channel.State == CommunicationState.Opened)
                        {
                            notificationAction(client);
                        }
                        else
                        {
                            _connectionManager.UnregisterClient(username);
                        }
                    }
                    catch (Exception)
                    {
                        _connectionManager.UnregisterClient(username);
                    }
                }
            }
        }

        private async Task CleanPlayerStateIfNeeded(Player player)
        {
            if (player.GameIdGame != null)
            {
                try
                {
                    var oldGame = await _repository.GetGameByIdAsync(player.GameIdGame.Value);
                    if (oldGame == null || oldGame.GameStatus == (int)GameStatus.Finished)
                    {
                        player.GameIdGame = null;
                        await _repository.SaveChangesAsync();
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"Error cleaning player state for {player.Username}", ex);
                    throw ExceptionManager.Map(ex);
                }
            }
        }

        public async Task<LobbyCreationResultDto> CreateLobbyAsync(CreateLobbyRequest request)
        {
            var result = new LobbyCreationResultDto();
            try
            {
                if (request?.Settings == null)
                {
                    result.ErrorMessage = "Datos inválidos.";
                    result.ErrorType = LobbyErrorType.InvalidData;
                }
                else if (request.Settings.MaxPlayers < 2 || request.Settings.MaxPlayers > 4)
                {
                    result.ErrorMessage = "Jugadores entre 2 y 4.";
                    result.ErrorType = LobbyErrorType.InvalidData;
                }
                else
                {
                    var callback = _wcfContext.GetCallbackChannel<ILobbyServiceCallback>();
                    var hostPlayer = await _repository.GetPlayerByUsernameAsync(request.HostUsername);

                    if (hostPlayer == null)
                    {
                        result.ErrorMessage = "Jugador anfitrión no encontrado.";
                        result.ErrorType = LobbyErrorType.UserNotFound;
                    }
                    else if (hostPlayer.IsGuest)
                    {
                        result.ErrorMessage = "Los invitados no pueden crear partidas.";
                        result.ErrorType = LobbyErrorType.GuestNotAllowed;
                    }
                    else
                    {
                        if (callback != null)
                        {
                            _connectionManager.RegisterClient(request.HostUsername, callback);
                        }

                        await CleanPlayerStateIfNeeded(hostPlayer);

                        if (hostPlayer.GameIdGame != null)
                        {
                            result.ErrorMessage = "Ya estás en una partida activa.";
                            result.ErrorType = LobbyErrorType.PlayerAlreadyInGame;
                        }
                        else
                        {
                            string newLobbyCode = GenerateLobbyCode();
                            var newGame = new Game
                            {
                                GameStatus = (int)GameStatus.WaitingForPlayers,
                                HostPlayerID = hostPlayer.IdPlayer,
                                Board_idBoard = request.Settings.BoardId,
                                IsPublic = request.Settings.IsPublic,
                                MaxPlayers = request.Settings.MaxPlayers,
                                LobbyCode = newLobbyCode,
                                StartTime = DateTime.UtcNow
                            };

                            _repository.AddGame(newGame);
                            await _repository.SaveChangesAsync();

                            hostPlayer.GameIdGame = newGame.IdGame;
                            await _repository.SaveChangesAsync();

                            result.Success = true;
                            result.LobbyCode = newLobbyCode;
                            result.ErrorType = LobbyErrorType.None;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error CreateLobbyAsync for {request?.HostUsername}", ex);
                throw ExceptionManager.Map(ex);
            }
            return result;
        }

        public async Task<JoinLobbyResultDto> JoinLobbyAsync(JoinLobbyRequest request)
        {
            var result = new JoinLobbyResultDto();
            try
            {
                var callback = _wcfContext.GetCallbackChannel<ILobbyServiceCallback>();
                var player = await _repository.GetPlayerByUsernameAsync(request.Username);

                if (player == null)
                {
                    result.ErrorType = LobbyErrorType.UserNotFound;
                    result.ErrorMessage = "Usuario no encontrado";
                    return result;
                }

                if (callback != null)
                {
                    _connectionManager.RegisterClient(request.Username, callback);
                }

                await CleanPlayerStateIfNeeded(player);

                var game = await _repository.GetGameByCodeAsync(request.LobbyCode);
                if (game == null)
                {
                    result.ErrorType = LobbyErrorType.GameNotFound;
                    result.ErrorMessage = "Partida no encontrada";
                    return result;
                }

                var updatedPlayers = await _repository.GetPlayersInGameAsync(game.IdGame);
                bool isAlreadyInGame = updatedPlayers.Any(p => p.Username == request.Username);

                if (game.GameStatus != (int)GameStatus.WaitingForPlayers)
                {
                    result.ErrorType = LobbyErrorType.GameStarted;
                    result.ErrorMessage = "Partida ya iniciada";
                }
                else if (updatedPlayers.Count >= game.MaxPlayers && !isAlreadyInGame)
                {
                    result.ErrorType = LobbyErrorType.GameFull;
                    result.ErrorMessage = "Partida llena";
                }
                else
                {
                    if (!isAlreadyInGame)
                    {
                        player.GameIdGame = game.IdGame;
                        await _repository.SaveChangesAsync();
                        updatedPlayers = await _repository.GetPlayersInGameAsync(game.IdGame);
                    }

                    result.Success = true;
                    result.BoardId = game.Board_idBoard;
                    result.MaxPlayers = game.MaxPlayers;
                    result.IsHost = (player.IdPlayer == game.HostPlayerID);
                    result.IsPublic = game.IsPublic;
                    result.PlayersInLobby = updatedPlayers.Select(p => new PlayerLobbyDto
                    {
                        Username = p.Username,
                        IsHost = (p.IdPlayer == game.HostPlayerID)
                    }).ToList();

                    var usernamesToNotify = updatedPlayers
                        .Where(p => p.Username != request.Username)
                        .Select(p => p.Username)
                        .ToList();

                    var newPlayerDto = new PlayerLobbyDto { Username = request.Username, IsHost = result.IsHost };

                    FireAndForgetNotification(usernamesToNotify, client => client.OnPlayerJoined(newPlayerDto));
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error JoinLobbyAsync for {request?.Username}", ex);
                throw ExceptionManager.Map(ex);
            }
            return result;
        }

        public async Task<bool> StartGameAsync(string lobbyCode)
        {
            bool result = false;
            try
            {
                var game = await _repository.GetGameByCodeAsync(lobbyCode);
                if (game != null)
                {
                    var players = await _repository.GetPlayersInGameAsync(game.IdGame);
                    if (players.Count >= 2)
                    {
                        game.GameStatus = (int)GameStatus.InProgress;
                        await _repository.SaveChangesAsync();

                        try
                        {
                            _gameMonitor.StartMonitoring(game.IdGame);
                        }
                        catch (Exception gmEx)
                        {
                            Log.Error($"Failed to start monitoring for game {lobbyCode}", gmEx);
                        }

                        var usernames = players.Select(p => p.Username).ToList();
                        FireAndForgetNotification(usernames, client => client.OnGameStarted());

                        result = true;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error StartGameAsync for {lobbyCode}", ex);
                throw ExceptionManager.Map(ex);
            }
            return result;
        }

        public async Task DisbandLobbyAsync(string hostUsername)
        {
            try
            {
                var hostPlayer = await _repository.GetPlayerByUsernameAsync(hostUsername);
                if (hostPlayer != null && hostPlayer.GameIdGame != null)
                {
                    int gameId = hostPlayer.GameIdGame.Value;
                    var game = await _repository.GetGameByIdAsync(gameId);

                    if (game != null)
                    {
                        var players = await _repository.GetPlayersInGameAsync(gameId);
                        var usernamesToNotify = players
                            .Where(p => p.Username != hostUsername)
                            .Select(p => p.Username)
                            .ToList();

                        FireAndForgetNotification(usernamesToNotify, client => client.OnLobbyDisbanded());

                        try { _gameMonitor.StopMonitoring(gameId); } catch { }

                        _repository.DeleteGameAndCleanDependencies(game);
                        await _repository.SaveChangesAsync();
                    }
                    else
                    {
                        hostPlayer.GameIdGame = null;
                        await _repository.SaveChangesAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error DisbandLobbyAsync for {hostUsername}", ex);
                throw ExceptionManager.Map(ex);
            }
        }

        public async Task<bool> LeaveLobbyAsync(string username)
        {
            bool result = false;
            try
            {
                var player = await _repository.GetPlayerByUsernameAsync(username);
                if (player != null && player.GameIdGame != null)
                {
                    int gameId = player.GameIdGame.Value;
                    var game = await _repository.GetGameByIdAsync(gameId);

                    if (game != null && game.HostPlayerID == player.IdPlayer)
                    {
                        await DisbandLobbyAsync(username);
                        result = true;
                    }
                    else
                    {
                        var allPlayers = await _repository.GetPlayersInGameAsync(gameId);
                        var usernamesToNotify = allPlayers
                            .Where(p => p.Username != username)
                            .Select(p => p.Username)
                            .ToList();

                        player.GameIdGame = null;
                        await _repository.SaveChangesAsync();

                        _connectionManager.UnregisterClient(username);

                        bool gameClosed = await HandleGameShutdownIfNeeded(gameId);

                        if (!gameClosed)
                        {
                            FireAndForgetNotification(usernamesToNotify, client => client.OnPlayerLeft(username));
                        }
                        result = true;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error LeaveLobbyAsync for {username}", ex);
                throw ExceptionManager.Map(ex);
            }
            return result;
        }

        public async Task<bool> KickPlayerAsync(KickPlayerRequest request)
        {
            bool result = false;
            try
            {
                if (request != null && request.RequestorUsername != request.TargetUsername)
                {
                    var game = await _repository.GetGameByCodeAsync(request.LobbyCode);
                    if (game != null)
                    {
                        var host = await _repository.GetPlayerByUsernameAsync(request.RequestorUsername);
                        if (host != null && game.HostPlayerID == host.IdPlayer)
                        {
                            var target = await _repository.GetPlayerByUsernameAsync(request.TargetUsername);
                            if (target != null && target.GameIdGame == game.IdGame)
                            {
                                target.KickCount++;
                                bool isBanned = (target.KickCount >= 3);
                                if (isBanned) target.IsBanned = true;

                                target.GameIdGame = null;
                                await _repository.SaveChangesAsync();

                                var remainingPlayers = await _repository.GetPlayersInGameAsync(game.IdGame);
                                var usernamesToNotify = remainingPlayers.Select(p => p.Username).ToList();

                                string msg = isBanned ? "Has sido BANEADO." : "Has sido expulsado.";

                                NotifyClientDirect(request.TargetUsername, c => c.OnPlayerKicked(msg));
                                FireAndForgetNotification(usernamesToNotify, client => client.OnPlayerLeft(request.TargetUsername));

                                result = true;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error KickPlayerAsync for target {request?.TargetUsername}", ex);
                throw ExceptionManager.Map(ex);
            }
            return result;
        }

        public async Task SystemKickPlayerAsync(string lobbyCode, string username, string reason)
        {
            try
            {
                var game = await _repository.GetGameByCodeAsync(lobbyCode);
                var player = await _repository.GetPlayerByUsernameAsync(username);

                if (game != null && player != null && player.GameIdGame == game.IdGame)
                {
                    NotifyClientDirect(username, c => c.OnPlayerKicked(reason));

                    player.GameIdGame = null;
                    await _repository.SaveChangesAsync();

                    var remainingPlayers = await _repository.GetPlayersInGameAsync(game.IdGame);
                    var usernamesToNotify = remainingPlayers.Select(p => p.Username).ToList();
                    FireAndForgetNotification(usernamesToNotify, client => client.OnPlayerLeft(username));
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error SystemKickPlayerAsync for {username}", ex);
                throw ExceptionManager.Map(ex);
            }
        }

        public async Task<LobbyStateDto> GetLobbyStateAsync(string lobbyCode)
        {
            LobbyStateDto result = null;
            try
            {
                var game = await _repository.GetGameByCodeAsync(lobbyCode);
                if (game != null)
                {
                    var players = await _repository.GetPlayersInGameAsync(game.IdGame);
                    result = new LobbyStateDto
                    {
                        IsGameStarted = (game.GameStatus == (int)GameStatus.InProgress),
                        Players = players.Select(p => new PlayerLobbyDto
                        {
                            Username = p.Username,
                            IsHost = (p.IdPlayer == game.HostPlayerID)
                        }).ToList(),
                        BoardId = game.Board_idBoard,
                        MaxPlayers = game.MaxPlayers,
                        IsPublic = game.IsPublic
                    };
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error GetLobbyStateAsync for {lobbyCode}", ex);
                throw ExceptionManager.Map(ex);
            }
            return result;
        }

        public async Task<ActiveMatchDto[]> GetPublicMatchesAsync()
        {
            var matchesList = new List<ActiveMatchDto>();
            try
            {
                var games = await _repository.GetActivePublicGamesAsync();
                foreach (var game in games)
                {
                    int currentCount = await _repository.CountPlayersInGameAsync(game.IdGame);
                    if (currentCount < game.MaxPlayers)
                    {
                        string hostName = await _repository.GetUsernameByIdAsync(game.HostPlayerID);
                        matchesList.Add(new ActiveMatchDto
                        {
                            LobbyCode = game.LobbyCode,
                            HostUsername = hostName,
                            BoardId = game.Board_idBoard,
                            CurrentPlayers = currentCount,
                            MaxPlayers = game.MaxPlayers
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("Error GetPublicMatchesAsync", ex);
                throw ExceptionManager.Map(ex);
            }
            return matchesList.ToArray();
        }

        private async Task<bool> HandleGameShutdownIfNeeded(int gameId)
        {
            bool result = false;
            try
            {
                var game = await _repository.GetGameByIdAsync(gameId);
                if (game == null)
                {
                    result = true;
                }
                else
                {
                    var remainingPlayers = await _repository.GetPlayersInGameAsync(game.IdGame);
                    if (game.GameStatus == (int)GameStatus.InProgress && remainingPlayers.Count < 2)
                    {
                        game.GameStatus = (int)GameStatus.Finished;
                        try { _gameMonitor.StopMonitoring(gameId); } catch { }
                        await _repository.SaveChangesAsync();
                        result = true;
                    }
                    else if (game.GameStatus == (int)GameStatus.WaitingForPlayers && remainingPlayers.Count == 0)
                    {
                        _repository.DeleteGameAndCleanDependencies(game);
                        await _repository.SaveChangesAsync();
                        result = true;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error HandleGameShutdownIfNeeded for game {gameId}", ex);
                throw ExceptionManager.Map(ex);
            }
            return result;
        }

        private void NotifyClientDirect(string username, Action<ILobbyServiceCallback> action)
        {
            var client = _connectionManager.GetClient(username);
            if (client != null)
            {
                try
                {
                    action(client);
                }
                catch
                {
                    _connectionManager.UnregisterClient(username);
                }
            }
        }

        private string GenerateLobbyCode()
        {
            string code;
            do
            {
                code = _codeGenerator.GenerateRandomString(5);
            }
            while (!_repository.IsLobbyCodeUnique(code));
            return code;
        }
    }
}