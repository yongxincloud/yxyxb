using System;
using System.Net;
using System.Net.Sockets;

namespace YxyXb.Core
{
    public static class NetFail
    {
        public static bool IsUnreachable(Exception ex)
        {
            for (var cur = ex; cur != null; cur = cur.InnerException)
            {
                var web = cur as WebException;
                if (web != null)
                {
                    return web.Response == null;
                }

                if (cur is SocketException)
                {
                    return true;
                }
            }

            var text = ex == null ? "" : (ex.Message ?? "");
            return text.IndexOf("无法连接", StringComparison.Ordinal) >= 0
                || text.IndexOf("超时", StringComparison.Ordinal) >= 0
                || text.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static void NoteKept(System.Text.StringBuilder log, Exception ex)
        {
            if (log == null || !IsUnreachable(ex))
            {
                return;
            }

            if (log.ToString().IndexOf("无网络，本地包保留", StringComparison.Ordinal) >= 0)
            {
                return;
            }

            log.Append(" 无网络，本地包保留。");
        }
    }
}
