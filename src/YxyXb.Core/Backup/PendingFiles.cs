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
                var path = items[i] == null ? null : items[i].ZipPath;
                if (!PendingBackup.AllowPack(TryDeleteZip(path)))
                {
                    log.Append(" 删除未传完的包失败：").Append(path).Append("。");
                    return false;
                }

                DiscardQuiet(path, log);
                log.Append(" 已删除未传完的包：").Append(path).Append("。");
            }

            return true;
        }

        public static bool UploadItem(SqliteDb db, PendingItem item, bool doNetwork, bool doFtp, bool doCloud, string provider, string kind, string taskName, StringBuilder log, Action persist)
        {
            var ok = true;
            var channels = PendingBackup.ChannelsToSend(item, doNetwork, doFtp, doCloud);
            for (var i = 0; i < channels.Length; i++)
            {
                var channel = channels[i];
                var sent = false;
                if (channel == "network")
                {
                    sent = DistChannel.UncAndFtp(db, true, false, item.ZipPath, log);
                    if (sent) item.NetworkOk = true;
                }
                else if (channel == "ftp")
                {
                    sent = DistChannel.UncAndFtp(db, false, true, item.ZipPath, log);
                    if (sent) item.FtpOk = true;
                }
                else if (channel == "cloud")
                {
                    sent = DistChannel.Cloud(db, true, provider, kind, taskName, item.ZipPath, log);
                    if (sent) item.CloudOk = true;
                }

                if (sent)
                {
                    if (persist != null) persist();
                }
                else
                {
                    ok = false;
                }
            }

            return ok;
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
