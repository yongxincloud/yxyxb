using System;

namespace Yxy.Baidu
{
    public static class BaiduUploadRetry
    {
        public const int Attempts = 5;

        public static bool IsTransient(Exception ex)
        {
            if (ex == null || string.IsNullOrEmpty(ex.Message))
            {
                return false;
            }

            var m = ex.Message;
            return m.IndexOf("强迫关闭", StringComparison.Ordinal) >= 0
                || m.IndexOf("forcibly closed", StringComparison.OrdinalIgnoreCase) >= 0
                || m.IndexOf("传输连接", StringComparison.Ordinal) >= 0
                || m.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) >= 0
                || m.IndexOf("超时", StringComparison.Ordinal) >= 0;
        }

        public static int SleepMs(int attempt)
        {
            if (attempt < 0)
            {
                attempt = 0;
            }

            return 1000 * (attempt + 1);
        }

        public static int GapMs(int kbps, int bytes, int elapsedMs)
        {
            if (kbps <= 0 || bytes <= 0)
            {
                return 0;
            }

            var need = (int)((bytes * 1000L) / (kbps * 1024L));
            var wait = need - (elapsedMs < 0 ? 0 : elapsedMs);
            return wait < 0 ? 0 : wait;
        }
    }
}
