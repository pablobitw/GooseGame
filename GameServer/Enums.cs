﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameServer
{
    public enum AccountStatus
    {
        Pending,
        Active,
        Inactive,
        Banned
    }

    public enum FriendshipStatus
    {
        Pending,
        Accepted,
        Blocked
    }

    public enum GameStatus
    {
        WaitingForPlayers,
        InProgress,
        Finished,
        Cancelled
    }

    public enum ItemType
    {
        Skin,
        Gadget,
        Emote
    }

    public enum RarityType
    {
        Common,
        Epic,
        Legendary
    }

    public enum SanctionType
    {
        Kick,
        TemporaryBan,
        PermanentBan
    }
}
