using System;
using YxyXb.Contracts.Time;

namespace YxyXb.Core.Schedule
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

        public static DateTime? NextPlannedAfter(string kind, int interval, DateTime anchorUtc, DateTime afterUtc)
        {
            if (interval < 1)
            {
                interval = 1;
            }

            if (anchorUtc > afterUtc)
            {
                return anchorUtc;
            }

            if (string.Equals(kind, "once", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (string.Equals(kind, "month", StringComparison.OrdinalIgnoreCase))
            {
                var anchorLocal = ChinaTime.ToChina(anchorUtc);
                var afterLocal = ChinaTime.ToChina(afterUtc);
                var steps = ((afterLocal.Year - anchorLocal.Year) * 12L + afterLocal.Month - anchorLocal.Month) / interval;
                if (steps < 0)
                {
                    steps = 0;
                }

                var candidate = anchorLocal.AddMonths(checked((int)(steps * interval)));
                while (candidate <= afterLocal)
                {
                    steps++;
                    candidate = anchorLocal.AddMonths(checked((int)(steps * interval)));
                }

                return ChinaTime.ToUtc(candidate);
            }

            long stepTicks;
            if (string.Equals(kind, "minute", StringComparison.OrdinalIgnoreCase))
            {
                stepTicks = TimeSpan.FromMinutes(interval).Ticks;
            }
            else if (string.Equals(kind, "hour", StringComparison.OrdinalIgnoreCase))
            {
                stepTicks = TimeSpan.FromHours(interval).Ticks;
            }
            else if (string.Equals(kind, "week", StringComparison.OrdinalIgnoreCase))
            {
                stepTicks = TimeSpan.FromDays(7d * interval).Ticks;
            }
            else
            {
                stepTicks = TimeSpan.FromDays(interval).Ticks;
            }

            var jumps = (afterUtc.Ticks - anchorUtc.Ticks) / stepTicks + 1;
            return anchorUtc.AddTicks(checked(jumps * stepTicks));
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
