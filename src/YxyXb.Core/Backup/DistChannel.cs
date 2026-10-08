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
                    log.Append(" 网络传输成功。");
                }
                catch (Exception ex)
                {
                    ok = false;
                    log.Append(" 网络传输失败：").Append(ex.Message).Append("。");
                }
            }

            if (doFtp)
            {
                try
                {
                    var ftp = SettingsStore.GetXfer<FtpSettingsDto>(db, "ftp");
                    FtpUtil.UploadFile(ftp, zipPath, Path.GetFileName(zipPath), up);
                    log.Append(" FTP 成功。");
                }
                catch (Exception ex)
                {
                    ok = false;
                    log.Append(" FTP 失败：").Append(ex.Message).Append("。");
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
                log.Append(" 云上传成功 ").Append(key).Append("。");
                if (string.Equals(use, CloudProviders.Baidu, StringComparison.OrdinalIgnoreCase))
                {
                    log.Append(" 分片过程见「日志 → 百度分片」。目录：")
                        .Append(AppPaths.BaiduUploadDirectory)
                        .Append("。");
                }
                return true;
            }
            catch (Exception ex)
            {
                log.Append(" 云上传失败：").Append(ex.Message).Append("。");
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
                    MailUtil.SendResult(smtp, taskName, distOk, log.ToString());
                    log.Append(" 已发邮件。");
                }
                catch (Exception ex)
                {
                    mailOk = false;
                    log.Append(" 邮件失败：").Append(ex.Message).Append("。");
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
    }
}
