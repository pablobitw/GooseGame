namespace GameServer.Chat.Moderation
{
    public sealed class ChatModerationResult
    {
        public bool IsAllowed { get; }
        public bool IsCensored { get; }
        public bool IsBlocked { get; }
        public bool RequiresKick { get; }

        public string FinalMessage { get; }
        public string SystemNotification { get; }

        private ChatModerationResult(
            bool isAllowed,
            bool isCensored,
            bool isBlocked,
            bool requiresKick,
            string finalMessage,
            string systemNotification)
        {
            IsAllowed = isAllowed;
            IsCensored = isCensored;
            IsBlocked = isBlocked;
            RequiresKick = requiresKick;
            FinalMessage = finalMessage;
            SystemNotification = systemNotification;
        }

        public static ChatModerationResult Allowed(string message)
        {
            return new ChatModerationResult(
                isAllowed: true,
                isCensored: false,
                isBlocked: false,
                requiresKick: false,
                finalMessage: message,
                systemNotification: null);
        }

        public static ChatModerationResult Censored(string censoredMessage)
        {
            return new ChatModerationResult(
                isAllowed: true,
                isCensored: true,
                isBlocked: false,
                requiresKick: false,
                finalMessage: censoredMessage,
                systemNotification: null);
        }

        public static ChatModerationResult Blocked(string systemNotification)
        {
            return new ChatModerationResult(
                isAllowed: false,
                isCensored: false,
                isBlocked: true,
                requiresKick: false,
                finalMessage: null,
                systemNotification: systemNotification);
        }

        public static ChatModerationResult Kick(string systemNotification)
        {
            return new ChatModerationResult(
                isAllowed: false,
                isCensored: false,
                isBlocked: true,
                requiresKick: true,
                finalMessage: null,
                systemNotification: systemNotification);
        }
    }
}
