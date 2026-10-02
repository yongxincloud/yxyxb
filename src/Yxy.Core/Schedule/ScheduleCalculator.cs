using System;

namespace Yxy.Core.Schedule
{
    public static class ScheduleCalculator
    {
        public static DateTime? NextAfter(string kind, int interval, DateTime fromUtc)
        {
            if (interval < 1)
            {
                interval = 1;
            }

            if (string.Equals(kind, "once", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (string.Equals(kind, "minute", StringComparison.OrdinalIgnoreCase))
            {
                return fromUtc.AddMinutes(interval);
            }

            if (string.Equals(kind, "hour", StringComparison.OrdinalIgnoreCase))
            {
                return fromUtc.AddHours(interval);
            }

            if (string.Equals(kind, "week", StringComparison.OrdinalIgnoreCase))
            {
                return fromUtc.AddDays(7d * interval);
            }

            if (string.Equals(kind, "month", StringComparison.OrdinalIgnoreCase))
            {
                return fromUtc.AddMonths(interval);
            }

            return fromUtc.AddDays(interval);
        }

        public static string Validate(string kind, int interval, DateTime? nextRunUtc)
        {
            if (string.IsNullOrEmpty(kind))
            {
                return "请选择周期。";
            }

            if (interval < 1)
            {
                return "间隔至少为 1。";
            }

            if (!nextRunUtc.HasValue)
            {
                return "请填写下次执行时间。";
            }

            return null;
        }
    }
}
