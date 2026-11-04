using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using YxyXb.Contracts.Cloud;
using YxyXb.Contracts.Dto;
using YxyXb.Core.Cloud;
using YxyXb.Core.Data;
using YxyXb.Core.Store;
using YxyXb.Core.Transfer;

namespace YxyXb.Core.Backup
{
    public sealed class PackageFinishResult
    {
        public bool DataOk { get; set; }
        public bool MailOk { get; set; }
        public bool MailAttempted { get; set; }
        public string ResultCode { get; set; }
    }

    public static class DistChannel
    {
        public static bool UncAndFtp(SqliteDb db, bool doNetwork, bool doFtp, string zipPath, StringBuilder log)
        {
            var speed = SettingsStore.Get(db);
            var up = CloudSpeed.UploadKbps(speed.UploadMinKbps, speed.UploadMaxKbps);
            var ok = true;
            if (doNetwork)
            {
                try
                {
                    var unc = SettingsStore.GetXfer<UncSettingsDto>(db, "unc");
                    UncShare.CopyFile(unc, zipPath, Path.GetFileName(zipPath), up);
                    UncShare.VerifyFile(unc, zipPath, Path.GetFileName(zipPath));
                    log.Append(" 网络传输成功。");
                }
                catch (Exception ex)
                {
                    ok = false;
                    log.Append(" 网络传输失败：").Append(ex.Message).Append("。");
                    NetFail.NoteKept(log, ex);
                }
            }

            if (doFtp)
            {
                try
                {
                    var ftp = SettingsStore.GetXfer<FtpSettingsDto>(db, "ftp");
                    FtpUtil.UploadFile(ftp, zipPath, Path.GetFileName(zipPath), up);
                    FtpUtil.VerifyFile(ftp, zipPath, Path.GetFileName(zipPath));
                    log.Append(" FTP 成功。");
                }
                catch (Exception ex)
                {
                    ok = false;
                    log.Append(" FTP 失败：").Append(ex.Message).Append("。");
                    NetFail.NoteKept(log, ex);
                }
            }

            return ok;
        }

        public static bool Cloud(SqliteDb db, bool doCloud, string provider, string kind, string taskName, string zipPath, StringBuilder log)
        {
            if (!doCloud)
            {
                return true;
            }

            try
            {
                var settings = SettingsStore.Get(db);
                var key = CloudObjectKeys.ForZip(settings.SoftwareId, kind, taskName, zipPath);
                var use = string.IsNullOrWhiteSpace(provider) ? CloudProviders.Qiniu : provider;
                CloudRuntime.Require().Upload(db, use, zipPath, key);
                CloudRuntime.Require().Verify(db, use, zipPath, key);
                log.Append(" 云上传成功 ").Append(key).Append("。");
                if (string.Equals(use, CloudProviders.Baidu, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(use, CloudProviders.Qiniu, StringComparison.OrdinalIgnoreCase))
                {
                    var folder = string.Equals(use, CloudProviders.Qiniu, StringComparison.OrdinalIgnoreCase)
                        ? AppPaths.QiniuUploadDirectory
                        : AppPaths.BaiduUploadDirectory;
                    log.Append(" 分片过程见「日志 → 分片日志」。目录：")
                        .Append(folder)
                        .Append("。");
                }
                return true;
            }
            catch (Exception ex)
            {
                log.Append(" 云上传失败 ").Append(Path.GetFileName(zipPath)).Append("：").Append(ex.Message).Append("。");
                NetFail.NoteKept(log, ex);
                return false;
            }
        }

        public static bool Finish(SqliteDb db, bool doEmail, bool doDelete, string taskName, IList<string> zips, bool distOk, StringBuilder log)
        {
            var mailOk = true;
            var smtp = SettingsStore.GetXfer<SmtpSettingsDto>(db, "smtp");
            if (MailUtil.ShouldSend(smtp, doEmail, distOk))
            {
                try
                {
                    MailUtil.SendResult(smtp, taskName, distOk, log.ToString(), SettingsStore.Get(db).SoftwareId);
                    log.Append(" 已发邮件。");
                }
                catch (Exception ex)
                {
                    mailOk = false;
                    log.Append(" 邮件失败：").Append(ex.Message).Append("。");
                    NetFail.NoteKept(log, ex);
                }
            }

            if (doDelete)
            {
                if (!distOk || !mailOk)
                {
                    throw new InvalidOperationException("已启用的网络/FTP/云/邮件未全部成功，不立即删本地包。");
                }

                for (var i = 0; i < zips.Count; i++)
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(zips[i]) && File.Exists(zips[i]))
                        {
                            File.Delete(zips[i]);
                        }
                    }
                    catch (IOException)
                    {
                    }
                }

                log.Append(" 已立即删本地包。");
            }

            return distOk && mailOk;
        }

        public static PackageFinishResult FinishPackages(
            SqliteDb db,
            bool doEmail,
            bool doDelete,
            string taskName,
            IList<PendingItem> items,
            bool distOk,
            BackupRunStats stats,
            StringBuilder log)
        {
            var dataOk = distOk && stats.SkippedFiles == 0;
            var mailOk = true;
            var mailAttempted = false;
            var smtp = SettingsStore.GetXfer<SmtpSettingsDto>(db, "smtp");
            var packageCount = CountPackages(items);
            long archiveBytes = 0;
            for (var i = 0; items != null && i < items.Count; i++)
            {
                var item = items[i];
                if (item == null) continue;
                if (item.ArchiveBytes > 0)
                {
                    archiveBytes += item.ArchiveBytes;
                }
                else if (!string.IsNullOrEmpty(item.ZipPath) && File.Exists(item.ZipPath))
                {
                    archiveBytes += new FileInfo(item.ZipPath).Length;
                }
            }

            if (MailUtil.ShouldSend(smtp, doEmail, dataOk))
            {
                mailAttempted = true;
                try
                {
                    MailUtil.SendResult(
                        smtp,
                        taskName,
                        dataOk,
                        log == null ? "" : log.ToString(),
                        SettingsStore.Get(db).SoftwareId,
                        BackupRunSummary.ToDatabaseMailStats(
                            stats,
                            archiveBytes,
                            items != null && items.Count > 0,
                            distOk,
                            packageCount,
                            distOk ? packageCount : 0));
                    log.Append(" 已发邮件。");
                }
                catch (Exception ex)
                {
                    mailOk = false;
                    log.Append(" 邮件失败：").Append(ex.Message).Append("。");
                    NetFail.NoteKept(log, ex);
                }
            }

            if (doDelete && dataOk)
            {
                for (var i = 0; items != null && i < items.Count; i++)
                {
                    if (!PendingFiles.TryDeletePackage(items[i]))
                    {
                        dataOk = false;
                        log.Append(" 无法立即删除本地包。");
                        break;
                    }
                }

                if (dataOk) log.Append(" 已立即删本地包。");
            }

            return new PackageFinishResult
            {
                DataOk = dataOk,
                MailOk = mailOk,
                MailAttempted = mailAttempted,
                ResultCode = BackupRunSummary.ResultCode(dataOk, mailOk)
            };
        }

        private static int CountPackages(IList<PendingItem> items)
        {
            var count = 0;
            for (var i = 0; items != null && i < items.Count; i++)
            {
                var item = items[i];
                if (item == null) continue;
                count += item.UploadFiles == null || item.UploadFiles.Length == 0
                    ? 1
                    : Math.Max(1, item.UploadFiles.Length - 1);
            }

            return Math.Max(1, count);
        }
    }
}
