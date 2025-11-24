using System.Runtime.Serialization;
using System.ServiceModel;
using System.Threading.Tasks;

namespace GameServer.Contracts
{
  
    [ServiceContract]
    public interface IUserProfileService
    {
        [OperationContract]
        Task<UserProfileDto> GetUserProfileAsync(string email);

        [OperationContract]
        Task<UsernameChangeResult> ChangeUsernameAsync(string email, string newUsername);

        [OperationContract]
        Task<bool> ChangeAvatarAsync(string email, string avatarName);

        [OperationContract]
        Task<bool> SendPasswordChangeCodeAsync(string email);

        [OperationContract]
        Task<bool> ChangePasswordWithCodeAsync(string email, string code, string newPassword);
    }

    [DataContract]
    public class UserProfileDto
    {
        [DataMember]
        public string Username { get; set; }

        [DataMember]
        public string Email { get; set; }

        [DataMember]
        public string AvatarPath { get; set; }

        [DataMember]
        public int Coins { get; set; }

        [DataMember]
        public int MatchesPlayed { get; set; }

        [DataMember]
        public int MatchesWon { get; set; }

        [DataMember]
        public int UsernameChangeCount { get; set; }
    }

    [DataContract]
    public enum UsernameChangeResult
    {
        [EnumMember]
        Success,
        [EnumMember]
        UsernameAlreadyExists,
        [EnumMember]
        LimitReached,  
        [EnumMember]
        CooldownActive,
        [EnumMember]
        UserNotFound,
        [EnumMember]
        FatalError,
        [EnumMember]
        IncorrectPassword
    }
}