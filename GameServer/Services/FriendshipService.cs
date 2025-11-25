using System;
using System.Collections.Generic;
using System.Data.Entity.Core;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using GameServer.Contracts;
using log4net;

namespace GameServer.Services
{
    public class FriendshipService : IFriendshipService
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(FriendshipService));

        public async Task<bool> SendFriendRequest(string senderUsername, string receiverUsername)
        {
            try
            {
                using (var context = new GameDatabase_Container())
                {
                    if (string.IsNullOrEmpty(senderUsername) || string.IsNullOrEmpty(receiverUsername))
                    {
                        return false;
                    }

                    if (senderUsername.Equals(receiverUsername))
                    {
                        return false;
                    }

                    var sender = context.Players.FirstOrDefault(p => p.Username == senderUsername);
                    var receiver = context.Players.FirstOrDefault(p => p.Username == receiverUsername);

                    if (sender == null || receiver == null)
                    {
                        Log.Warn($"Friend request failed. User not found: {senderUsername} or {receiverUsername}");
                        return false;
                    }

                    var existingRequest = context.Friendships.FirstOrDefault(f =>
                        ((f.PlayerIdPlayer == sender.IdPlayer && f.Player1_IdPlayer == receiver.IdPlayer) ||
                         (f.PlayerIdPlayer == receiver.IdPlayer && f.Player1_IdPlayer == sender.IdPlayer))
                        && (f.FriendshipStatus == (int)FriendshipStatus.Pending || f.FriendshipStatus == (int)FriendshipStatus.Accepted));

                    if (existingRequest != null)
                    {
                        Log.Warn($"Duplicate friend request between {senderUsername} and {receiverUsername}");
                        return false;
                    }

                    var newFriendship = new Friendship
                    {
                        PlayerIdPlayer = sender.IdPlayer,     
                        Player1_IdPlayer = receiver.IdPlayer, 
                        FriendshipStatus = (int)FriendshipStatus.Pending, 
                        RequestDate = DateTime.Now
                    };

                    context.Friendships.Add(newFriendship);
                    await context.SaveChangesAsync();

                    Log.Info($"Friend request sent from {senderUsername} to {receiverUsername}");
                    return true;
                }
            }
            catch (SqlException ex)
            {
                Log.Error("Database error in SendFriendRequest", ex);
                return false;
            }
            catch (EntityException ex)
            {
                Log.Error("Entity context error in SendFriendRequest", ex);
                return false;
            }
        }

        public async Task<bool> RespondToFriendRequest(string respondingUsername, string requesterUsername, bool isAccepted)
        {
            try
            {
                using (var context = new GameDatabase_Container())
                {
                    var responder = context.Players.FirstOrDefault(p => p.Username == respondingUsername);
                    var requester = context.Players.FirstOrDefault(p => p.Username == requesterUsername);

                    if (responder == null || requester == null)
                    {
                        return false;
                    }

                    var friendship = context.Friendships.FirstOrDefault(f =>
                        f.PlayerIdPlayer == requester.IdPlayer &&
                        f.Player1_IdPlayer == responder.IdPlayer &&
                        f.FriendshipStatus == (int)FriendshipStatus.Pending);

                    if (friendship == null)
                    {
                        Log.Warn($"Pending request not found between {requesterUsername} and {respondingUsername}");
                        return false;
                    }

                    if (isAccepted)
                    {
                        friendship.FriendshipStatus = (int)FriendshipStatus.Accepted;
                    }
                    else
                    {
                        context.Friendships.Remove(friendship);
                    }

                    await context.SaveChangesAsync();
                    Log.Info($"User {respondingUsername} responded {isAccepted} to request from {requesterUsername}");
                    return true;
                }
            }
            catch (SqlException ex)
            {
                Log.Error("Database error in RespondToFriendRequest", ex);
                return false;
            }
            catch (EntityException ex)
            {
                Log.Error("Entity context error in RespondToFriendRequest", ex);
                return false;
            }
        }

        public async Task<List<FriendDto>> GetFriendList(string username)
        {
            try
            {
                using (var context = new GameDatabase_Container())
                {
                    var player = context.Players.FirstOrDefault(p => p.Username == username);
                    if (player == null)
                    {
                        return new List<FriendDto>();
                    }

                    var acceptedStatus = (int)FriendshipStatus.Accepted;

                    var friendsQuery = from f in context.Friendships
                                       where (f.PlayerIdPlayer == player.IdPlayer || f.Player1_IdPlayer == player.IdPlayer)
                                             && f.FriendshipStatus == acceptedStatus
                                       select f;

                    var friendsList = new List<FriendDto>();

                    foreach (var friendship in friendsQuery)
                    {
                        int friendId = (friendship.PlayerIdPlayer == player.IdPlayer)
                                        ? friendship.Player1_IdPlayer
                                        : friendship.PlayerIdPlayer;

                        var friendEntity = context.Players.FirstOrDefault(p => p.IdPlayer == friendId);
                        if (friendEntity != null)
                        {
                            friendsList.Add(new FriendDto
                            {
                                Username = friendEntity.Username,
                                AvatarPath = friendEntity.Avatar,
                                IsOnline = false 
                            });
                        }
                    }

                    return friendsList;
                }
            }
            catch (SqlException ex)
            {
                Log.Error("Database error in GetFriendList", ex);
                return new List<FriendDto>();
            }
        }

        public async Task<List<FriendDto>> GetPendingRequests(string username)
        {
            try
            {
                using (var context = new GameDatabase_Container())
                {
                    var player = context.Players.FirstOrDefault(p => p.Username == username);
                    if (player == null)
                    {
                        return new List<FriendDto>();
                    }

                    var pendingStatus = (int)FriendshipStatus.Pending;

                    var requests = from f in context.Friendships
                                   where f.Player1_IdPlayer == player.IdPlayer
                                         && f.FriendshipStatus == pendingStatus
                                   select f;

                    var result = new List<FriendDto>();
                    foreach (var req in requests)
                    {
                        var sender = context.Players.FirstOrDefault(p => p.IdPlayer == req.PlayerIdPlayer);
                        if (sender != null)
                        {
                            result.Add(new FriendDto
                            {
                                Username = sender.Username,
                                AvatarPath = sender.Avatar
                            });
                        }
                    }
                    return result;
                }
            }
            catch (SqlException ex)
            {
                Log.Error("Database error in GetPendingRequests", ex);
                return new List<FriendDto>();
            }
        }
    }
}