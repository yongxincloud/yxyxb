using System;

namespace Yxy.Contracts.Dto
{
    public sealed class CloudAccountDto
    {
        public string Provider { get; set; }

        public bool Enabled { get; set; }

        public string Bucket { get; set; }

        public string AccessKey { get; set; }

        public string SecretKey { get; set; }

        public string Zone { get; set; }

        public string AccessToken { get; set; }

        public string RefreshToken { get; set; }

        public DateTime? TokenExpiryUtc { get; set; }

        public string DisplayName { get; set; }

        public string MaskedAccessKey
        {
            get { return CloudMask.Mask(AccessKey); }
        }

        public bool IsReady
        {
            get
            {
                return Enabled
                    && !string.IsNullOrWhiteSpace(Bucket)
                    && !string.IsNullOrWhiteSpace(AccessKey)
                    && !string.IsNullOrWhiteSpace(SecretKey);
            }
        }

        public bool IsBaiduReady
        {
            get { return Enabled && !string.IsNullOrWhiteSpace(RefreshToken); }
        }
    }

    public static class CloudMask
    {
        public static string Mask(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "";
            }

            if (value.Length <= 8)
            {
                return value.Substring(0, 1) + "****";
            }

            return value.Substring(0, 4) + "****" + value.Substring(value.Length - 4);
        }
    }
}
