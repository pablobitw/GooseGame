using GameServer.DTOs.Gameplay;
using GameServer.Helpers;
using GameServer.Models;
using GameServer.Repositories;
using log4net;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;

namespace GameServer.Services.Logic
{
    public class VoteLogic
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(VoteLogic));

        private static readonly ConcurrentDictionary<int, VoteState> _activeVotes = new ConcurrentDictionary<int, VoteState>();

        private readonly IGameplayRepository _repository;
        private readonly Func<SanctionAppService> _sanctionServiceFactory;

        public VoteLogic(IGameplayRepository repository)
        {
            _repository = repository;
            _sanctionServiceFactory = () => new SanctionAppService();
        }

        public async Task InitiateVoteAsync(VoteRequestDto request)
        {
            await ValidateVoteRequestAsync(request);

            var player = await _repository.GetPlayerByUsernameAsync(request.Username);
            int gameId = player.GameIdGame.Value;
            var activePlayers = await _repository.GetPlayersInGameAsync(gameId);

            int eligibleVoters = activePlayers.Count - 1;

            var voteState = new VoteState
            {
                TargetUsername = request.TargetUsername,
                InitiatorUsername = request.Username,
                Reason = request.Reason,
                TotalEligibleVoters = eligibleVoters
            };

            voteState.VotesFor.Add(request.Username);

            if (_activeVotes.TryAdd(gameId, voteState))
            {
                Log.InfoFormat("Vote kick initiated in game {0} against {1}. Reason: {2}", gameId, request.TargetUsername, request.Reason);
                NotifyVoteStarted(activePlayers, request.TargetUsername, request.Reason);
            }
        }

        public async Task CastVoteAsync(VoteResponseDto request)
        {
            var player = await _repository.GetPlayerByUsernameAsync(request.Username);
            if (player == null || !player.GameIdGame.HasValue) return;

            int gameId = player.GameIdGame.Value;

            if (!_activeVotes.TryGetValue(gameId, out VoteState state))
                throw new FaultException("No active vote in this match.");

            if (request.Username == state.TargetUsername) return;

            bool isFinished = false;
            string lobbyCode = "UNKNOWN";

            lock (state)
            {
                if (!state.VotesFor.Contains(request.Username) && !state.VotesAgainst.Contains(request.Username))
                {
                    if (request.AcceptKick) state.VotesFor.Add(request.Username);
                    else state.VotesAgainst.Add(request.Username);

                    int totalVotesCast = state.VotesFor.Count + state.VotesAgainst.Count;

                    if (totalVotesCast >= state.TotalEligibleVoters)
                    {
                        isFinished = true;
                    }
                }
            }

            if (isFinished)
            {
                var game = await _repository.GetGameByIdAsync(gameId);
                lobbyCode = game?.LobbyCode ?? "UNKNOWN";
                await ProcessVoteResult(gameId, lobbyCode, state);
            }
        }

        public void CancelVote(int gameId)
        {
            _activeVotes.TryRemove(gameId, out _);
        }

        private async Task ProcessVoteResult(int gameId, string lobbyCode, VoteState state)
        {
            _activeVotes.TryRemove(gameId, out _);

            bool isKicked = state.VotesFor.Count > state.VotesAgainst.Count;

            if (isKicked)
            {
                Log.InfoFormat("Vote successful: {0} kicked from game {1}.", state.TargetUsername, gameId);

                var sanctionService = _sanctionServiceFactory();
                string kickReason = $"Voted out: {state.Reason}";
                await sanctionService.ProcessKickAsync(state.TargetUsername, lobbyCode, kickReason, "VOTE");
            }
            else
            {
                Log.InfoFormat("Vote failed against {0} in game {1}.", state.TargetUsername, gameId);
            }
        }

        private async Task ValidateVoteRequestAsync(VoteRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.TargetUsername))
                throw new FaultException("Target username required.");

            if (request.Username == request.TargetUsername)
                throw new FaultException("You cannot vote to kick yourself.");

            var player = await _repository.GetPlayerByUsernameAsync(request.Username);
            if (player == null || !player.GameIdGame.HasValue)
                throw new FaultException("You are not in a game.");

            int gameId = player.GameIdGame.Value;
            if (_activeVotes.ContainsKey(gameId))
                throw new FaultException("A vote is already in progress.");

            var activePlayers = await _repository.GetPlayersInGameAsync(gameId);
            if (activePlayers.Count < 3)
                throw new FaultException("Minimum 3 players required to start a vote.");

            var targetPlayer = activePlayers.FirstOrDefault(p => p.Username == request.TargetUsername);
            if (targetPlayer == null)
                throw new FaultException("Target player not found in game.");

            var lastMove = await _repository.GetLastMoveForPlayerAsync(gameId, targetPlayer.IdPlayer);
            if (lastMove != null && lastMove.FinalPosition >= 55)
            {
                throw new FaultException("Cannot kick a player near the finish line (Protection Active).");
            }
        }

        private void NotifyVoteStarted(List<Player> players, string targetUsername, string reason)
        {
            foreach (var p in players.Where(p => p.Username != targetUsername))
            {
                var callback = ConnectionManager.GetGameplayClient(p.Username);
                if (callback != null)
                {
                    try
                    {
                        callback.OnVoteKickStarted(targetUsername, reason);
                    }
                    catch (CommunicationException ex)
                    {
                        Log.WarnFormat("Connection error notifying vote to {0}: {1}", p.Username, ex.Message);
                    }
                    catch (TimeoutException)
                    {
                        Log.WarnFormat("Timeout notifying vote to {0}", p.Username);
                    }
                }
            }
        }
    }
}