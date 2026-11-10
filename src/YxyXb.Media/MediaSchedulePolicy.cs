using System;
using YxyXb.Contracts.Dto;
using YxyXb.Contracts.Time;
using YxyXb.Core.Schedule;

namespace YxyXb.Media
{
    internal enum MediaWaitDecision
    {
        StartWaiting,
        ContinueWaiting,
        SkipNoWait,
        SkipWindowEnded,
        SkipTimedOut
    }

    internal static class MediaSchedulePolicy
    {
        public static DateTime NextOpenLocal(DateTime nowLocal, MediaTaskDto task)
        {
            var start = WindowClock.NextWindowStartLocal(nowLocal, task.WindowStart, task.WindowEnd);
            if (!task.NextRunUtc.HasValue)
            {
                return start;
            }

            var chosen = task.NextRunUtc.Value.Kind == DateTimeKind.Utc
                ? ChinaTime.ToChina(task.NextRunUtc.Value)
                : task.NextRunUtc.Value;
            if (!WindowClock.IsInWindow(chosen, task.WindowStart, task.WindowEnd))
            {
                return start;
            }

            var sameClock = start.Date.Add(chosen.TimeOfDay);
            if (sameClock < nowLocal)
            {
                sameClock = sameClock.AddDays(1);
            }

            return WindowClock.IsInWindow(sameClock, task.WindowStart, task.WindowEnd)
                ? sameClock
                : start;
        }

        public static MediaWaitDecision DecideWait(
            MediaTaskDto task,
            DateTime nowUtc,
            DateTime nowLocal)
        {
            if (task.WaitMinutes <= 0)
            {
                return MediaWaitDecision.SkipNoWait;
            }

            if (!task.WaitingSinceUtc.HasValue)
            {
                return MediaWaitDecision.StartWaiting;
            }

            if (nowLocal >= WindowClock.WindowEndLocal(nowLocal, task.WindowStart, task.WindowEnd))
            {
                return MediaWaitDecision.SkipWindowEnded;
            }

            return (nowUtc - task.WaitingSinceUtc.Value).TotalMinutes >= task.WaitMinutes
                ? MediaWaitDecision.SkipTimedOut
                : MediaWaitDecision.ContinueWaiting;
        }
    }
}
