using System;
using System.IO;
using YxyXb.Contracts.Dto;
using YxyXb.Core;

namespace YxyXb.Media.Disks
{
    public static class MediaSafety
    {
        public static string RefuseWrite(LiveDiskDto disk, string bakPath)
        {
            if (disk == null)
            {
                return "未找到介质。";
            }

            if (!ExternalBus.IsAllowed(disk.BusType))
            {
                return "只允许 USB / 1394 / SD（含 USB 桥接的移动硬盘）。";
            }

            if (disk.IsSystem)
            {
                return "拒绝操作系统/启动盘。";
            }

            if (HoldsProtected(disk.DriveLetter, bakPath))
            {
                return "该卷承载程序或备份目录，拒绝写入。";
            }

            return null;
        }

        public static string RefuseIsolate(LiveDiskDto disk, string bakPath, bool registered)
        {
            var write = RefuseWrite(disk, bakPath);
            if (write != null)
            {
                return write;
            }

            if (!registered)
            {
                return "未登记或序列号不符，拒绝隔离。";
            }

            return null;
        }

        public static bool HoldsProtected(string letter, string bakPath)
        {
            if (string.IsNullOrEmpty(letter))
            {
                return false;
            }

            var root = letter.TrimEnd('\\') + "\\";
            if (Starts(AppPaths.InstallDirectory, root) || Starts(AppPaths.DataDirectory, root) || Starts(bakPath, root))
            {
                return true;
            }

            return false;
        }

        private static bool Starts(string path, string root)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            try
            {
                return Path.GetFullPath(path).StartsWith(root, StringComparison.OrdinalIgnoreCase);
            }
            catch (ArgumentException)
            {
                return false;
            }
        }
    }
}
