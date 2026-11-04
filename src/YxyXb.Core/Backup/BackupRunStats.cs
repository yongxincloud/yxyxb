using System.Globalization;
using YxyXb.Core.Transfer;

namespace YxyXb.Core.Backup
{
    public sealed class BackupRunStats
    {
        public int DiscoveredFiles { get; set; }

        public int PackedFiles { get; set; }

        public int SkippedFiles { get; set; }

        public long SourceBytes { get; set; }
    }

    public static class BackupRunSummary
    {
        public static MailRunStats ToMailStats(
            BackupRunStats stats,
            long archiveBytes,
            bool uploadEnabled,
            bool uploadOk)
        {
            return ToMailStats(stats, archiveBytes, uploadEnabled, uploadOk, 1, uploadOk ? 1 : 0);
        }

        public static MailRunStats ToMailStats(
            BackupRunStats stats,
            long archiveBytes,
            bool uploadEnabled,
            bool uploadOk,
            int packageCount,
            int uploadedCount)
        {
            var value = stats ?? new BackupRunStats();
            if (packageCount < 1)
            {
                packageCount = 1;
            }
            var fileStats = "发现 " + value.DiscoveredFiles
                + "，打包 " + value.PackedFiles
                + "，跳过 " + value.SkippedFiles;
            var packageStats = !uploadEnabled
                ? "共 " + packageCount + " 个" + (packageCount > 1 ? "分卷" : "包") + "，未启用数据上传"
                : "共 " + packageCount + " 个" + (packageCount > 1 ? "分卷" : "包")
                    + "，上传成功 " + uploadedCount + "/" + packageCount;
            var uploadedBytes = uploadEnabled && uploadOk ? archiveBytes : 0;
            var sizeStats = "源 " + FormatBytes(value.SourceBytes)
                + "，压缩后 " + FormatBytes(archiveBytes)
                + "，已上传 " + FormatBytes(uploadedBytes);
            return new MailRunStats
            {
                FileStats = fileStats,
                PackageStats = packageStats,
                SizeStats = sizeStats
            };
        }

        public static string ResultCode(bool dataOk, bool mailOk)
        {
            if (!dataOk)
            {
                return "error";
            }

            return mailOk ? "ok" : "warn";
        }

        public static string ManualMessage(string result)
        {
            if (result == "warn")
            {
                return "执行成功，通知发送失败。";
            }

            return result == "ok" ? "执行成功。" : "执行失败。";
        }

        public static string FormatBytes(long bytes)
        {
            if (bytes < 1024)
            {
                return bytes.ToString(CultureInfo.InvariantCulture) + " B";
            }

            double value = bytes;
            var units = new[] { "KB", "MB", "GB", "TB" };
            for (var i = 0; i < units.Length; i++)
            {
                value /= 1024d;
                if (value < 1024d || i == units.Length - 1)
                {
                    return value.ToString("0.0", CultureInfo.InvariantCulture) + " " + units[i];
                }
            }

            return bytes.ToString(CultureInfo.InvariantCulture) + " B";
        }
    }
}

