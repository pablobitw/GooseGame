using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
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

    public enum SocialType : byte
    {
        YouTube = 1,
        X = 2,
        Facebook = 3,
        TikTok = 4,
        Instagram = 5
    }
    [DataContract(Name = "LobbyErrorType")]
    public enum LobbyErrorType
    {
        [EnumMember] None,              
        [EnumMember] Unknown,             
        [EnumMember] DatabaseError,       
        [EnumMember] ServerTimeout,       
        [EnumMember] InvalidData,         
        [EnumMember] UserNotFound,        
        [EnumMember] GuestNotAllowed,     
        [EnumMember] PlayerAlreadyInGame,  
        [EnumMember] GameNotFound,         
        [EnumMember] GameStarted,         
        [EnumMember] GameFull
    }
    public enum GameplayErrorType
    {
        None,               
        DatabaseError,      
        GameNotFound,       
        NotYourTurn,        
        GameFinished,      
        PlayerKicked,       
        Timeout,            
        Unknown             
    }

}
