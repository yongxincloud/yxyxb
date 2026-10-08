using System;
using System.Globalization;

namespace YxyXb.Core.Schedule
{
    public static class WindowClock
    {
        public static bool TryParseHm(string text, out TimeSpan time)
        {
            return TimeSpan.TryParseExact(text, @"hh\:mm", CultureInfo.InvariantCulture, out time)
                || TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out time);
        }

        public static string Validate(string startText, string endText)
        {
            TimeSpan start;
            TimeSpan end;
            if (!TryParseHm(startText, out start) || !TryParseHm(endText, out end))
            {
                return "任务开始、介质离线请填 HH:mm。";
            }

            if (start == end)
            {
                return "任务开始与介质离线不能相同。";
            }

            return null;
        }

        public static bool IsInWindow(DateTime localNow, string startText, string endText)
        {
            TimeSpan start;
            TimeSpan end;
            if (!TryParseHm(startText, out start) || !TryParseHm(endText, out end))
            {
                return false;
            }

            var t = localNow.TimeOfDay;
            if (start < end)
            {
                return t >= start && t < end;
            }

            return t >= start || t < end;
        }

        public static DateTime WindowEndLocal(DateTime localNow, string startText, string endText)
        {
            TimeSpan start;
            TimeSpan end;
            TryParseHm(startText, out start);
            TryParseHm(endText, out end);
            var date = localNow.Date;
            if (start < end)
            {
                return date + end;
            }

            if (localNow.TimeOfDay >= start)
            {
                return date.AddDays(1) + end;
            }

            return date + end;
        }

        public static DateTime NextWindowStartLocal(DateTime localNow, string startText, string endText)
        {
            TimeSpan start;
            TryParseHm(startText, out start);
            if (IsInWindow(localNow, startText, endText))
            {
                return localNow;
            }

            var today = localNow.Date + start;
            if (localNow < today)
            {
                return today;
            }

            return today.AddDays(1);
        }
    }
}
