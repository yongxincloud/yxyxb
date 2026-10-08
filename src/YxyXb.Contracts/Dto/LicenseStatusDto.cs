using System;

namespace YxyXb.Contracts.Dto
{
    public sealed class LicenseStatusDto
    {
        public DateTime FirstRunUtc { get; set; }

        public int RemainingDays { get; set; }

        public bool IsExpired { get; set; }

        public string Account { get; set; }

        public bool HasCachedLicense { get; set; }
    }
}
