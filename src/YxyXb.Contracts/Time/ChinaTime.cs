using System;

namespace YxyXb.Contracts.Time
{
    public static class ChinaTime
    {
        private static readonly TimeZoneInfo Zone = FindZone();

        public static DateTime Now
        {
            get { return ToChina(DateTime.UtcNow); }
        }

        public static DateTime ToChina(DateTime utc)
        {
            var value = utc.Kind == DateTimeKind.Utc ? utc : DateTime.SpecifyKind(utc, DateTimeKind.Utc);
            return TimeZoneInfo.ConvertTimeFromUtc(value, Zone);
        }

        public static DateTime ToUtc(DateTime chinaLocal)
        {
            var value = DateTime.SpecifyKind(chinaLocal, DateTimeKind.Unspecified);
            return TimeZoneInfo.ConvertTimeToUtc(value, Zone);
        }

        private static TimeZoneInfo FindZone()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
            }

            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai");
            }
            catch (TimeZoneNotFoundException)
            {
            }

            return TimeZoneInfo.CreateCustomTimeZone(
                "Asia/Shanghai",
                TimeSpan.FromHours(8),
                "Asia/Shanghai",
                "Asia/Shanghai");
        }
    }
}
