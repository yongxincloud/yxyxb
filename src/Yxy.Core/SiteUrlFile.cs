using System;
using System.Text.RegularExpressions;

namespace Yxy.Core
{
    public static class SiteUrlFile
    {
        public const string DefaultSite = "https://backup.yongxincloud.cn";

        public static string Read(string raw, string fallback)
        {
            if (string.IsNullOrWhiteSpace(fallback))
            {
                fallback = DefaultSite;
            }

            if (string.IsNullOrWhiteSpace(raw))
            {
                return fallback;
            }

            var text = raw.Trim();
            var named = Regex.Match(text, @"(?im)^\s*URL\s*=\s*(\S+)");
            if (named.Success)
            {
                text = named.Groups[1].Value.Trim().Trim('"', '\'');
            }

            if (!StartsHttp(text))
            {
                var found = Regex.Match(text, @"https?://[^\s\r\n]+", RegexOptions.IgnoreCase);
                if (found.Success)
                {
                    text = found.Value.Trim();
                }
            }

            text = text.Trim().TrimEnd('/');
            return StartsHttp(text) ? text : fallback;
        }

        private static bool StartsHttp(string text)
        {
            return text.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || text.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        }
    }
}
