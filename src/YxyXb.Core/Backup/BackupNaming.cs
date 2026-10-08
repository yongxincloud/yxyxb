using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using YxyXb.Contracts.Dto;
using YxyXb.Core.IO;

namespace YxyXb.Core.Backup
{
    public static class BackupNaming
    {
        public static string FileFolder(string bakRoot, string softwareId, string taskName)
        {
            return FileFolder(bakRoot, softwareId, taskName, NameParts.Default);
        }

        public static string FileFolder(string bakRoot, string softwareId, string taskName, NameParts parts)
        {
            return BuildFolder(bakRoot, parts, softwareId, "文件备份", null, taskName);
        }

        public static string FileZipName(string softwareId, string taskName, DateTime utc)
        {
            return FileZipName(softwareId, taskName, utc, NameParts.Default);
        }

        public static string FileZipName(string softwareId, string taskName, DateTime utc, NameParts parts)
        {
            return BuildZipName(parts, softwareId, taskName, null, utc);
        }

        public static string MssqlFolder(string bakRoot, string softwareId, string taskName)
        {
            return MssqlFolder(bakRoot, softwareId, taskName, null, NameParts.Default);
        }

        public static string MssqlFolder(string bakRoot, string softwareId, string taskName, string instanceName, NameParts parts)
        {
            return BuildFolder(bakRoot, parts, softwareId, "库备份", instanceName, taskName);
        }

        public static string MssqlZipName(string softwareId, string taskName, string dbName, DateTime utc)
        {
            return MssqlZipName(softwareId, taskName, dbName, utc, NameParts.Default);
        }

        public static string MssqlZipName(string softwareId, string taskName, string dbName, DateTime utc, NameParts parts)
        {
            return BuildZipName(parts, softwareId, taskName, dbName, utc);
        }

        public static string Preview(string bakRoot, string softwareId, string taskName)
        {
            return Path.Combine(FileFolder(bakRoot, softwareId, taskName), FileZipName(softwareId, taskName, DateTime.UtcNow));
        }

        public static string Preview(string bakRoot, string softwareId, NameParts parts)
        {
            var use = parts ?? NameParts.Default;
            var when = DateTime.UtcNow;
            var file = Path.Combine(
                FileFolder(bakRoot, softwareId, "示例任务", use),
                FileZipName(softwareId, "示例任务", when, use));
            var db = Path.Combine(
                MssqlFolder(bakRoot, softwareId, "示例任务", "示例实例", use),
                MssqlZipName(softwareId, "示例任务", "示例库", when, use));
            var text = new StringBuilder();
            text.Append("文件备份：").Append(file);
            text.Append("\n库备份：").Append(db);
            if ((use.ToFlags() & NameParts.FileMask) == 0)
            {
                text.Append("\n文件名至少勾选一项，否则不能保存。");
            }
            else if (!use.UseFileTask && !use.UseDirTask)
            {
                text.Append("\n未包含任务名，不同任务会写到同一位置。");
            }

            return text.ToString();
        }

        private static string BuildFolder(string bakRoot, NameParts parts, string softwareId, string kind, string instanceName, string taskName)
        {
            var use = parts ?? NameParts.Default;
            var path = string.IsNullOrEmpty(bakRoot) ? "" : bakRoot;
            if (use.UseDirSoftwareId)
            {
                path = Path.Combine(path, LongPath.SafeName(softwareId));
            }

            if (use.UseDirKind)
            {
                path = Path.Combine(path, kind);
            }

            if (use.UseDirInstance && !string.IsNullOrEmpty(instanceName))
            {
                path = Path.Combine(path, LongPath.SafeName(instanceName));
            }

            if (use.UseDirTask)
            {
                path = Path.Combine(path, LongPath.SafeName(taskName));
            }

            return path;
        }

        private static string BuildZipName(NameParts parts, string softwareId, string taskName, string dbName, DateTime utc)
        {
            var use = parts ?? NameParts.Default;
            var bits = new List<string>();
            if (use.UseFileSoftwareId)
            {
                bits.Add(LongPath.SafeName(softwareId));
            }

            if (use.UseFileTask)
            {
                bits.Add(LongPath.SafeName(taskName));
            }

            if (!string.IsNullOrEmpty(dbName))
            {
                bits.Add(LongPath.SafeName(dbName));
            }

            if (use.UseFileTime)
            {
                bits.Add(utc.ToLocalTime().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));
            }

            if (bits.Count == 0)
            {
                bits.Add("backup");
            }

            return string.Join("_", bits.ToArray()) + ".zip";
        }

        public static bool IsLoop(string sourcePath, string bakRoot)
        {
            if (string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(bakRoot))
            {
                return false;
            }

            try
            {
                var source = Path.GetFullPath(sourcePath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                var bak = Path.GetFullPath(bakRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                return source.StartsWith(bak, StringComparison.OrdinalIgnoreCase)
                    || bak.StartsWith(source, StringComparison.OrdinalIgnoreCase);
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
        }
    }
}
