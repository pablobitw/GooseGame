using GameServer.Chat.Moderation;
using System.Threading.Tasks;

namespace GameServer.Helpers

{

    public interface IChatSpamTracker
    {
        ChatModerationResult Analyze(string lobbyCode, string username);
    }

    public class ChatSpamTrackerWrapper : IChatSpamTracker
    {

        private readonly SpamTracker _tracker;

        public ChatSpamTrackerWrapper()
        {
            _tracker = new SpamTracker();
        }

        public ChatModerationResult Analyze(string lobbyCode, string username)
        {
            return _tracker.Analyze(lobbyCode, username);
        }
    }


    public interface IChatProfanityFilter
    {
        ChatModerationResult Analyze(string message);
    }

    public class ChatProfanityFilterWrapper : IChatProfanityFilter
    {
        public ChatModerationResult Analyze(string message)
        {
            return ProfanityFilter.Analyze(message);
        }
    }

    public interface IChatWarningTracker
    {
        WarningLevel RegisterWarning(string lobbyCode, string username);
        void Reset(string lobbyCode, string username);
    }

    public class ChatWarningTrackerWrapper : IChatWarningTracker
    {
        public WarningLevel RegisterWarning(string lobbyCode, string username)
        {

            return WarningTracker.RegisterWarning(lobbyCode, username);
        }

        public void Reset(string lobbyCode, string username)
        {
            WarningTracker.Reset(lobbyCode, username);
        }
    }
}