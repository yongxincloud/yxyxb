using System;

namespace YxyXb.Contracts.Cloud
{
    public static class CloudEndpoints
    {
        public static string Host(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return "";
            }

            var text = raw.Trim();
            var scheme = text.IndexOf("://", StringComparison.Ordinal);
            if (scheme >= 0)
            {
                text = text.Substring(scheme + 3);
            }

            var at = text.LastIndexOf('@');
            if (at >= 0)
            {
                text = text.Substring(at + 1);
            }

            var cut = text.IndexOfAny(new[] { '/', '?', '#' });
            if (cut >= 0)
            {
                text = text.Substring(0, cut);
            }

            return text.Trim();
        }
    }
}
