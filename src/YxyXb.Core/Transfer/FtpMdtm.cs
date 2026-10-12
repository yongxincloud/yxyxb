using System;
using System.Globalization;
using System.IO;

namespace YxyXb.Core.Transfer
{
    /// <summary>
    /// FTP MDTM（213 YYYYMMDDHHMMSS[.fff]）解析为 UTC，并写到本地文件时间。
    /// 指纹只用这个时间，不用本次下载的本地时钟。
    /// </summary>
    public static class FtpMdtm
    {
        public static bool TryParseUtc(string statusDescription, out DateTime utc)
        {
            utc = default(DateTime);
            if (string.IsNullOrEmpty(statusDescription))
            {
                return false;
            }

            var i = 0;
            while (i < statusDescription.Length)
            {
                if (!char.IsDigit(statusDescription[i]))
                {
                    i++;
                    continue;
                }

                var start = i;
                while (i < statusDescription.Length && char.IsDigit(statusDescription[i]))
                {
                    i++;
                }

                var len = i - start;
                for (var off = 0; off + 14 <= len; off++)
                {
                    var token = statusDescription.Substring(start + off, 14);
                    DateTime parsed;
                    if (!DateTime.TryParseExact(
                        token,
                        "yyyyMMddHHmmss",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                        out parsed))
                    {
                        continue;
                    }

                    if (off + 14 == len && i < statusDescription.Length && statusDescription[i] == '.')
                    {
                        parsed = AddFraction(parsed, statusDescription, i);
                    }

                    utc = parsed;
                    return true;
                }
            }

            return false;
        }

        public static void ApplyUtcWriteTime(string localPath, DateTime utc)
        {
            if (utc.Kind == DateTimeKind.Local)
            {
                utc = utc.ToUniversalTime();
            }
            else if (utc.Kind != DateTimeKind.Utc)
            {
                utc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
            }

            File.SetLastWriteTimeUtc(localPath, utc);
        }

        private static DateTime AddFraction(DateTime utc, string text, int dotIndex)
        {
            var ticks = 0L;
            var scale = 1000000;
            var j = dotIndex + 1;
            var digits = 0;
            while (j < text.Length && digits < 7 && char.IsDigit(text[j]))
            {
                ticks += (text[j] - '0') * (long)scale;
                scale /= 10;
                digits++;
                j++;
            }

            if (digits == 0)
            {
                return utc;
            }

            return utc.AddTicks(ticks);
        }
    }
}
