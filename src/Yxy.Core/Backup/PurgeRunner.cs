using System;
using System.Text;
using Yxy.Contracts.Cloud;
using Yxy.Contracts.Dto;
using Yxy.Core.Cloud;
using Yxy.Core.Data;
using Yxy.Core.Schedule;
using Yxy.Core.Store;
using Yxy.Core.Transfer;

namespace Yxy.Core.Backup
{
    public static class PurgeRunner
    {
        public static bool IsDue(DateTime localNow, string purgeTime, DateTime? lastUtc)
        {
            if (lastUtc.HasValue && lastUtc.Value.ToLocalTime().Date == localNow.Date)
            {
                return false;
            }

            TimeSpan hm;
            if (!WindowClock.TryParseHm(string.IsNullOrEmpty(purgeTime) ? "00:30" : purgeTime, out hm))
            {
                return false;
            }

            return localNow.TimeOfDay >= hm;
        }

        public static bool HasAnyPurge(AppSettingsDto settings)
        {
            if (settings == null)
            {
                return false;
            }

            return settings.PurgeLocal
                || settings.PurgeNetwork
                || settings.PurgeFtp
                || settings.PurgeQiniu
                || settings.PurgeBaidu;
        }

        public static bool ExecuteDue(SqliteDb db)
        {
            var settings = SettingsStore.Get(db);
            var nowLocal = DateTime.Now;
            if (!IsDue(nowLocal, settings.PurgeTime, settings.LastPurgeUtc))
            {
                return false;
            }

            var log = new StringBuilder();
            var ok = true;
            try
            {
                if (settings.PurgeLocal)
                {
                    var bak = string.IsNullOrEmpty(settings.BakPath) ? AppPaths.BakDirectory : settings.BakPath;
                    var n = LocalPurge.DeleteOldZips(bak, settings.LocalRetainDays, DateTime.UtcNow);
                    log.Append("本机删除 ").Append(n).Append(" 个过期 zip。");
                }

                var remote = SiteGate.AllowsChannels();
                if (remote && settings.PurgeNetwork)
                {
                    try
                    {
                        var n = UncShare.DeleteOldZips(SettingsStore.GetXfer<UncSettingsDto>(db, "unc"), settings.NetworkRetainDays, DateTime.UtcNow);
                        log.Append(" 网络删除 ").Append(n).Append("。");
                    }
                    catch (Exception ex)
                    {
                        ok = false;
                        log.Append(" 网络删除失败：").Append(ex.Message).Append("。");
                    }
                }

                if (remote && settings.PurgeFtp)
                {
                    try
                    {
                        var n = FtpUtil.DeleteOldZips(SettingsStore.GetXfer<FtpSettingsDto>(db, "ftp"), settings.FtpRetainDays, DateTime.UtcNow);
                        log.Append(" FTP 删除 ").Append(n).Append("。");
                    }
                    catch (Exception ex)
                    {
                        ok = false;
                        log.Append(" FTP 删除失败：").Append(ex.Message).Append("。");
                    }
                }

                if (remote && settings.PurgeQiniu)
                {
                    try
                    {
                        var prefix = CloudObjectKeys.ProductPrefix(settings.SoftwareId);
                        var older = DateTime.UtcNow.AddDays(-settings.QiniuRetainDays);
                        var n = CloudRuntime.Require().DeleteExpired(db, CloudProviders.Qiniu, prefix, older, delegate(string key)
                        {
                            LogStore.Add(db, "purge", "auto", "ok", "七牛", "删除调用成功：" + key);
                        });
                        log.Append(" 七牛删除调用成功，共 ").Append(n).Append(" 个。");
                    }
                    catch (Exception ex)
                    {
                        ok = false;
                        log.Append(" 七牛删除失败：").Append(ex.Message).Append("。");
                    }
                }

                if (remote && settings.PurgeBaidu)
                {
                    try
                    {
                        var prefix = CloudObjectKeys.ProductPrefix(settings.SoftwareId);
                        var days = settings.BaiduRetainDays <= 0 ? 7 : settings.BaiduRetainDays;
                        var older = DateTime.UtcNow.AddDays(-days);
                        var n = CloudRuntime.Require().DeleteExpired(db, CloudProviders.Baidu, prefix, older, delegate(string key)
                        {
                            LogStore.Add(db, "purge", "auto", "ok", "百度", "删除调用成功：" + key);
                        });
                        log.Append(" 百度删除调用成功，共 ").Append(n).Append(" 个。");
                    }
                    catch (Exception ex)
                    {
                        ok = false;
                        log.Append(" 百度删除失败：").Append(ex.Message).Append("。");
                    }
                }

                var days = settings.LogRetainDays < 1 ? 1 : settings.LogRetainDays;
                var cut = DateTime.UtcNow.AddDays(-days);
                var logRows = LogStore.DeleteOlderThan(db, cut);
                log.Append(" 日志删除 ").Append(logRows).Append(" 条。");
            }
            catch (Exception ex)
            {
                ok = false;
                log.Append(" 失败：").Append(ex.Message);
            }

            SettingsStore.MarkPurged(db, DateTime.UtcNow);
            LogStore.Add(db, "purge", "auto", ok ? "ok" : "error", "自动删除", log.ToString().Trim());
            return true;
        }
    }
}
