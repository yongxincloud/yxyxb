using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using YxyXb.Core.Cloud;
using YxyXb.Core.Data;

namespace YxyXb.Core.Backup
{
    public static class PendingFiles
    {
        public static void SetSplitItem(PendingItem item, SplitPackageResult split, BackupRunStats stats)
        {
            if (item == null)
            {
                throw new ArgumentNullException("item");
            }

            if (split == null)
            {
                throw new ArgumentNullException("split");
            }

            item.ZipPath = null;
            item.PackageDirectory = split.PackageDirectory;
            item.ManifestPath = split.ManifestPath;
            item.ArchiveBytes = split.ArchiveBytes;
            var files = new List<PendingUploadFile>();
            for (var i = 0; i < split.PartPaths.Length; i++)
            {
                files.Add(new PendingUploadFile { LocalPath = split.PartPaths[i] });
            }

            files.Add(new PendingUploadFile { LocalPath = split.ManifestPath });
            item.UploadFiles = files.ToArray();
        }

        public static PendingUploadFile[] PendingFilesFor(PendingItem item)
        {
            if (item == null)
            {
                return new PendingUploadFile[0];
            }

            if (item.UploadFiles == null || item.UploadFiles.Length == 0)
            {
                item.UploadFiles = string.IsNullOrEmpty(item.ZipPath)
                    ? new PendingUploadFile[0]
                    : new[] { new PendingUploadFile { LocalPath = item.ZipPath } };
            }

            return item.UploadFiles;
        }

        public static bool AnyPending(PendingItem item, string channel)
        {
            var files = PendingFilesFor(item);
            for (var i = 0; i < files.Length; i++)
            {
                if (!IsChannelOk(files[i], channel))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool PackageExists(PendingItem item, Func<string, bool> fileExists)
        {
            if (item == null || fileExists == null)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(item.PackageDirectory) && !Directory.Exists(item.PackageDirectory))
            {
                return false;
            }

            var files = PendingFilesFor(item);
            if (files.Length == 0)
            {
                return false;
            }

            for (var i = 0; i < files.Length; i++)
            {
                if (string.IsNullOrEmpty(files[i].LocalPath) || !fileExists(files[i].LocalPath))
                {
                    return false;
                }
            }

            return true;
        }

        public static bool TryDeleteZip(string zipPath)
        {
            if (string.IsNullOrEmpty(zipPath))
            {
                return true;
            }

            try
            {
                var part = zipPath + ".yxypart";
                if (File.Exists(part))
                {
                    File.Delete(part);
                }

                if (File.Exists(zipPath))
                {
                    File.Delete(zipPath);
                }

                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        public static string DeleteUnpublished(string json, bool doNetwork, bool doFtp, bool doCloud)
        {
            var record = PendingBackup.Parse(json);
            if (record == null || record.Items == null)
            {
                return null;
            }

            var doomed = new List<PendingItem>();
            for (var i = 0; i < record.Items.Length; i++)
            {
                if (PendingBackup.NeedsUpload(record.Items[i], doNetwork, doFtp, doCloud))
                {
                    doomed.Add(record.Items[i]);
                }
            }

            var log = new StringBuilder();
            if (!DeleteReplacements(doomed.ToArray(), log))
            {
                return "未传完的本地包删不掉，任务未删除。" + log.ToString();
            }

            return null;
        }

        public static bool DeleteReplacements(PendingItem[] items, StringBuilder log)
        {
            if (items == null)
            {
                return true;
            }

            for (var i = 0; i < items.Length; i++)
            {
                var path = items[i] == null ? null : items[i].PackageDirectory ?? items[i].ZipPath;
                if (!PendingBackup.AllowPack(TryDeletePackage(items[i])))
                {
                    log.Append(" 删除未传完的包失败：").Append(path).Append("。");
                    return false;
                }

                var packageFiles = PendingFilesFor(items[i]);
                for (var j = 0; j < packageFiles.Length; j++)
                {
                    DiscardQuiet(packageFiles[j].LocalPath, log);
                }
                log.Append(" 已删除未传完的包：").Append(path).Append("。");
            }

            return true;
        }

        public static bool UploadItem(SqliteDb db, PendingItem item, bool doNetwork, bool doFtp, bool doCloud, string provider, string kind, string taskName, StringBuilder log, Action persist)
        {
            return UploadPackageFiles(
                item,
                doNetwork,
                doFtp,
                doCloud,
                persist,
                delegate(string channel, PendingUploadFile file)
                {
                    if (channel == "network")
                    {
                        return DistChannel.UncAndFtp(db, true, false, file.LocalPath, log);
                    }

                    if (channel == "ftp")
                    {
                        return DistChannel.UncAndFtp(db, false, true, file.LocalPath, log);
                    }

                    return DistChannel.Cloud(db, true, provider, kind, taskName, file.LocalPath, log);
                });
        }

        public static bool UploadPackageFiles(
            PendingItem item,
            bool doNetwork,
            bool doFtp,
            bool doCloud,
            Action persist,
            Func<string, PendingUploadFile, bool> uploadOne)
        {
            var ok = true;
            var channels = PendingBackup.ChannelsToSend(item, doNetwork, doFtp, doCloud);
            var files = PendingFilesFor(item);
            for (var i = 0; i < channels.Length; i++)
            {
                var channel = channels[i];
                var allSent = true;
                for (var j = 0; j < files.Length; j++)
                {
                    if (IsChannelOk(files[j], channel))
                    {
                        continue;
                    }

                    var sent = uploadOne != null && uploadOne(channel, files[j]);
                    if (sent)
                    {
                        SetChannelOk(files[j], channel);
                    }

                    if (sent && persist != null)
                    {
                        persist();
                    }

                    allSent = allSent && sent;
                }

                if (allSent)
                {
                    if (channel == "network") item.NetworkOk = true;
                    else if (channel == "ftp") item.FtpOk = true;
                    else if (channel == "cloud") item.CloudOk = true;
                    if (persist != null) persist();
                }
                else
                {
                    ok = false;
                }
            }

            return ok;
        }

        private static void SetChannelOk(PendingUploadFile file, string channel)
        {
            if (channel == "network") file.NetworkOk = true;
            else if (channel == "ftp") file.FtpOk = true;
            else if (channel == "cloud") file.CloudOk = true;
        }

        public static bool TryDeletePackage(PendingItem item)
        {
            if (item == null)
            {
                return true;
            }

            var files = PendingFilesFor(item);
            if (files.Length == 0)
            {
                return TryDeleteZip(item.ZipPath);
            }

            for (var i = 0; i < files.Length; i++)
            {
                if (!TryDeleteZip(files[i].LocalPath))
                {
                    return false;
                }
            }

            if (!string.IsNullOrEmpty(item.PackageDirectory) && Directory.Exists(item.PackageDirectory))
            {
                try
                {
                    Directory.Delete(item.PackageDirectory, true);
                }
                catch (IOException)
                {
                    return false;
                }
                catch (UnauthorizedAccessException)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsChannelOk(PendingUploadFile file, string channel)
        {
            if (file == null)
            {
                return false;
            }

            if (channel == "network") return file.NetworkOk;
            if (channel == "ftp") return file.FtpOk;
            return file.CloudOk;
        }

        private static void DiscardQuiet(string path, StringBuilder log)
        {
            var handler = CloudRuntime.Handler;
            if (handler == null || string.IsNullOrEmpty(path))
            {
                return;
            }

            try
            {
                handler.DiscardUpload(path);
            }
            catch (Exception ex)
            {
                log.Append(" 百度续传文件未删除：").Append(ex.Message).Append("。");
            }
        }
    }
}
