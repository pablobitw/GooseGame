using System;

namespace GameClient.Helpers
{
    public static class GameLogHelper
    {
        private const string LuckyBoxTagPrefix = "[LUCKYBOX:";


        public static string CleanMessage(string raw)
        {
            string clean = raw;
            if (string.IsNullOrEmpty(clean)) return string.Empty;

            if (clean.Contains(LuckyBoxTagPrefix))
            {
                int start = clean.IndexOf(LuckyBoxTagPrefix);
                if (start != -1)
                {
                    int end = clean.IndexOf("]", start);
                    if (end != -1)
                    {
                        string tag = clean.Substring(start, end - start + 1);
                        clean = clean.Replace(tag, "").Trim();
                    }
                }
            }

            return clean.Replace("[EXTRA]", "").Trim();
        }


        public static bool TryParseLuckyBox(string log, out string owner, out string type, out int amount)
        {
            owner = string.Empty;
            type = string.Empty;
            amount = 0;

            if (string.IsNullOrEmpty(log)) return false;

            int start = log.IndexOf(LuckyBoxTagPrefix);
            if (start == -1) return false;

            int end = log.IndexOf("]", start);
            if (end == -1) return false;

            try
            {
                string content = log.Substring(start, end - start + 1)
                                    .Replace(LuckyBoxTagPrefix, "")
                                    .Replace("]", "");

                string[] parts = content.Split(':');
                if (parts.Length != 2) return false;

                owner = parts[0]; 

                string[] rewardParts = parts[1].Split('_'); 
                if (rewardParts.Length != 2) return false;

                type = rewardParts[0];
                amount = int.Parse(rewardParts[1]);

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}