using System;
using System.Collections.Generic;
using System.IO;
using YxyXb.Contracts.Dto;
using YxyXb.Core.Backup;

namespace YxyXb.Media.Sync
{
    public static class MediaPack
    {
        public static string PackToBak(string sourceDir, string bakDir, string softwareId, string taskName, string password, int level, DateTime utc, NameParts parts)
        {
            if (string.IsNullOrWhiteSpace(sourceDir) || !Directory.Exists(sourceDir))
            {
                throw new DirectoryNotFoundException("要打包的文件夹不存在。");
            }

            if (string.IsNullOrWhiteSpace(bakDir))
            {
                throw new InvalidOperationException("请先设置本地备份目录。");
            }

            if (BackupNaming.IsLoop(sourceDir, bakDir))
            {
                throw new InvalidOperationException("打包文件夹不能是本地备份目录，也不能放在它里面。");
            }

            Directory.CreateDirectory(bakDir);
            var skipped = new List<string>();
            var files = SourceWalker.WalkLocal(sourceDir, skipped);
            if (files.Count == 0)
            {
                throw new InvalidOperationException("文件夹里没有可打包的文件。");
            }

            var use = parts ?? NameParts.Default;
            var folder = BackupNaming.FileFolder(bakDir, softwareId, taskName, use);
            var zipPath = Path.Combine(folder, BackupNaming.FileZipName(softwareId, taskName, utc, use));
            ZipPacker.Pack(files, zipPath, password, level);
            return zipPath;
        }

        public static bool RemoveLocalAfterCopy(string zipPath, bool landed)
        {
            if (!landed || string.IsNullOrEmpty(zipPath) || !File.Exists(zipPath))
            {
                return false;
            }

            File.Delete(zipPath);
            return true;
        }
    }
}
