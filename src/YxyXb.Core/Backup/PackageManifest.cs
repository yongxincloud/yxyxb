using System;
using System.IO;
using System.Security.Cryptography;
using YxyXb.Core.Security;

namespace YxyXb.Core.Backup
{
    public sealed class PackagePartManifest
    {
        public int Index { get; set; }

        public string FileName { get; set; }

        public string PayloadEntryName { get; set; }

        public long PayloadBytes { get; set; }

        public string PayloadSha256 { get; set; }

        public long WrappedBytes { get; set; }

        public string WrappedSha256 { get; set; }
    }

    public sealed class BackupPackageManifest
    {
        public int FormatVersion { get; set; }

        public string BackupId { get; set; }

        public string PackageName { get; set; }

        public string TaskName { get; set; }

        public string CreatedUtc { get; set; }

        public long PartSizeLimitBytes { get; set; }

        public long ArchiveBytes { get; set; }

        public string ArchiveSha256 { get; set; }

        public int SourceFileCount { get; set; }

        public int PackedFileCount { get; set; }

        public int SkippedFileCount { get; set; }

        public long SourceBytes { get; set; }

        public PackagePartManifest[] Parts { get; set; }
    }

    public static class PackageManifestCodec
    {
        public static string ToJson(BackupPackageManifest value)
        {
            return JsonUtil.ToJson(value);
        }

        public static BackupPackageManifest Parse(string json)
        {
            var value = JsonUtil.FromJson<BackupPackageManifest>(json);
            if (value == null || value.FormatVersion != 1 || value.Parts == null || value.Parts.Length == 0)
            {
                throw new InvalidOperationException("分卷清单格式不支持。");
            }

            return value;
        }

        public static string NewBackupId(DateTime utcNow)
        {
            return utcNow.ToUniversalTime().ToString("yyyyMMdd-HHmmss") + "-"
                + Guid.NewGuid().ToString("N").Substring(0, 8);
        }

        public static string FileSha256(string path)
        {
            using (var sha = SHA256.Create())
            using (var input = File.OpenRead(path))
            {
                return ToHex(sha.ComputeHash(input));
            }
        }

        public static string ToHex(byte[] bytes)
        {
            var text = new System.Text.StringBuilder(bytes.Length * 2);
            for (var i = 0; i < bytes.Length; i++)
            {
                text.Append(bytes[i].ToString("x2"));
            }

            return text.ToString();
        }
    }

    public static class PackageLimits
    {
        public const long DefaultBytes = 4L * 1024 * 1024 * 1024;
        public const long MinimumBytes = 64L * 1024 * 1024;
        public const long MaximumBytes = 1024L * 1024 * 1024 * 1024;
        public const long ManifestReserveBytes = 1024L * 1024;

        public static long Resolve(long globalBytes, long taskBytes)
        {
            if (taskBytes > 0)
            {
                return NormalizeTask(taskBytes);
            }

            return globalBytes > 0 ? NormalizeGlobal(globalBytes) : DefaultBytes;
        }

        public static long NormalizeGlobal(long bytes)
        {
            if (bytes < MinimumBytes)
            {
                return MinimumBytes;
            }

            return bytes > MaximumBytes ? MaximumBytes : bytes;
        }

        public static long NormalizeTask(long bytes)
        {
            if (bytes == 0)
            {
                return 0;
            }

            if (bytes < MinimumBytes || bytes > MaximumBytes)
            {
                throw new ArgumentOutOfRangeException("bytes", "分卷大小必须在 64 MiB 到 1 TiB 之间。");
            }

            return bytes;
        }
    }
}

