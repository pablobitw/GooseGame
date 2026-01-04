using System;
using System.Collections.Generic;
using System.Linq;

namespace GameServer.Helpers
{
    public static class PlayerSocialLinkHelper
    {
        private const int MAX_SOCIAL_LINKS = 3;

        private static readonly Dictionary<SocialType, string[]> AllowedDomains =
            new Dictionary<SocialType, string[]>
            {
                { SocialType.YouTube, new[] { "youtube.com", "youtu.be" } },
                { SocialType.X, new[] { "x.com", "twitter.com" } },
                { SocialType.Facebook, new[] { "facebook.com", "fb.com" } },
                { SocialType.TikTok, new[] { "tiktok.com" } },
                { SocialType.Instagram, new[] { "instagram.com" } }
            };

        public static bool CanAddSocialLink(
            IEnumerable<SocialType> existingLinks,
            string url,
            out SocialType detectedType,
            out string errorMessage)
        {
            detectedType = default;
            errorMessage = string.Empty;

            if (existingLinks == null)
                existingLinks = Enumerable.Empty<SocialType>();

            var existingList = existingLinks.ToList();

            if (existingList.Count >= MAX_SOCIAL_LINKS)
            {
                errorMessage = "You can only add up to 3 social links.";
                return false;
            }

            if (!TryDetectSocialType(url, out detectedType))
            {
                errorMessage = "The provided URL is not a supported social network.";
                return false;
            }

            if (existingList.Contains(detectedType))
            {
                errorMessage = $"You already have a {detectedType} link.";
                return false;
            }

            return true;
        }

        private static bool TryDetectSocialType(string url, out SocialType socialType)
        {
            socialType = default;

            if (string.IsNullOrWhiteSpace(url))
                return false;

            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
                return false;

            string host = uri.Host.ToLowerInvariant();

            foreach (var pair in AllowedDomains)
            {
                if (pair.Value.Any(domain => host.EndsWith(domain)))
                {
                    socialType = pair.Key;
                    return true;
                }
            }

            return false;
        }
    }
}
