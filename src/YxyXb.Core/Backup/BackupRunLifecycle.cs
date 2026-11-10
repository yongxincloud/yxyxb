using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using YxyXb.Core.Transfer;

namespace YxyXb.Core.Backup
{
    public sealed class BackupSourceInspection
    {
        public PendingDecision Decision { get; set; }

        public BackupRunStats Stats { get; set; }

        public bool TrackPending { get; set; }
    }

    public sealed class BackupPackageSet
    {
        public PendingRecord Record { get; set; }

        public PendingItem[] Packages { get; set; }

        public long ArchiveBytes { get; set; }
    }

    public sealed class BackupRunResult
    {
        public bool Ok { get; set; }

        public bool DataOk { get; set; }

        public bool MailOk { get; set; }

        public bool MailAttempted { get; set; }

        public string ResultCode { get; set; }
    }

    public sealed class BackupRunRequest
    {
        public IBackupRunTask Task { get; set; }

        public IBackupSourceAdapter Source { get; set; }

        public IBackupDelivery Delivery { get; set; }

        public IBackupNotification Notification { get; set; }

        public IBackupPackageStore PackageStore { get; set; }

        public bool DoNetwork { get; set; }

        public bool DoFtp { get; set; }

        public bool DoCloud { get; set; }

        public string CloudProvider { get; set; }

        public bool DoDeleteNow { get; set; }

        public string Mode { get; set; }

        public Func<DateTime> UtcNow { get; set; }

        public Action<StringBuilder> AfterSuccess { get; set; }
    }

    public interface IBackupRunTask
    {
        string LogType { get; }

        string TaskName { get; }

        string LoadPendingJson();

        void SavePendingJson(string json);

        void MarkResult(bool ok, DateTime utcNow);

        void WriteLog(string mode, string result, string message);
    }

    public interface IBackupSourceAdapter : IDisposable
    {
        BackupSourceInspection Inspect(PendingRecord pending, DateTime utcNow, StringBuilder log);

        BackupPackageSet Prepare(PendingRecord pending, PendingDecision decision, DateTime utcNow, StringBuilder log);
    }

    public interface IBackupDelivery
    {
        bool Upload(
            PendingItem item,
            bool doNetwork,
            bool doFtp,
            bool doCloud,
            string provider,
            StringBuilder log,
            Action persist);
    }

    public interface IBackupNotification
    {
        bool ShouldSend(bool dataOk);

        void Send(
            bool dataOk,
            BackupRunStats stats,
            long archiveBytes,
            bool uploadEnabled,
            bool uploadOk,
            int packageCount,
            string detail);
    }

    public interface IBackupPackageStore
    {
        bool DeleteReplacements(PendingItem[] items, StringBuilder log);

        bool DeletePackage(PendingItem item);
    }

    public static class BackupRunLifecycle
    {
        public static BackupRunResult Execute(BackupRunRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }

            if (request.Task == null || request.Source == null || request.Delivery == null
                || request.Notification == null || request.PackageStore == null)
            {
                throw new InvalidOperationException("备份运行依赖不完整。");
            }

            var now = request.UtcNow == null ? DateTime.UtcNow : request.UtcNow();
            var log = new StringBuilder();
            var result = new BackupRunResult
            {
                Ok = false,
                DataOk = false,
                MailOk = true,
                MailAttempted = false,
                ResultCode = BackupRunSummary.ResultCode(false, true)
            };
            var stats = new BackupRunStats();
            var archiveBytes = 0L;
            var packageCount = 1;
            try
            {
                var pending = PendingBackup.Parse(request.Task.LoadPendingJson());
                var inspection = request.Source.Inspect(pending, now, log);
                if (inspection == null || inspection.Decision == null)
                {
                    throw new InvalidOperationException("备份来源没有返回复用判断。");
                }

                stats = inspection.Stats ?? new BackupRunStats();
                PendingRecord record;
                PendingItem[] packages;
                if (inspection.Decision.Reuse)
                {
                    packages = inspection.Decision.Keep ?? new PendingItem[0];
                    if (packages.Length == 0)
                    {
                        throw new InvalidOperationException("没有可沿用的未传完的备份。");
                    }

                    record = pending;
                    archiveBytes = SumArchiveBytes(packages);
                    log.Append(" 沿用未传完的包 ").Append(PackagePath(packages[0]));
                }
                else
                {
                    if (!request.PackageStore.DeleteReplacements(inspection.Decision.Replace, log))
                    {
                        throw new InvalidOperationException("删除未传完的包失败，已中止。");
                    }

                    record = PendingBackup.RemoveItems(pending, inspection.Decision.Replace);
                    request.Task.SavePendingJson(PendingBackup.ToJson(record));
                    var prepared = request.Source.Prepare(record, inspection.Decision, now, log);
                    if (prepared == null || prepared.Packages == null || prepared.Packages.Length == 0)
                    {
                        throw new InvalidOperationException("备份来源没有产出备份包。");
                    }

                    record = prepared.Record ?? record;
                    packages = prepared.Packages;
                    archiveBytes = prepared.ArchiveBytes > 0
                        ? prepared.ArchiveBytes
                        : SumArchiveBytes(packages);
                }

                packageCount = CountPackages(packages);
                if (inspection.TrackPending)
                {
                    request.Task.SavePendingJson(PendingBackup.ToJson(record));
                }
                else
                {
                    request.Task.SavePendingJson(null);
                }

                var distOk = true;
                if (inspection.TrackPending)
                {
                    for (var i = 0; i < packages.Length; i++)
                    {
                        var item = packages[i];
                        var itemOk = request.Delivery.Upload(
                            item,
                            request.DoNetwork,
                            request.DoFtp,
                            request.DoCloud,
                            request.CloudProvider,
                            log,
                            delegate { request.Task.SavePendingJson(PendingBackup.ToJson(record)); });
                        if (!itemOk)
                        {
                            distOk = false;
                            log.Append(" ").Append(ItemLabel(item, i)).Append(" 未传完。");
                        }
                    }
                }

                log.Append(" 源 ").Append(BackupRunSummary.FormatBytes(stats.SourceBytes))
                    .Append("，压缩后 ").Append(BackupRunSummary.FormatBytes(archiveBytes)).Append("。");

                var dataOk = distOk && stats.SkippedFiles == 0;
                if (request.Notification.ShouldSend(dataOk))
                {
                    result.MailAttempted = true;
                    try
                    {
                        request.Notification.Send(
                            dataOk,
                            stats,
                            archiveBytes,
                            inspection.TrackPending,
                            distOk,
                            packageCount,
                            log.ToString());
                        log.Append(" 已发邮件。");
                    }
                    catch (Exception ex)
                    {
                        result.MailOk = false;
                        log.Append(" 邮件失败：").Append(ex.Message).Append("。");
                        NetFail.NoteKept(log, ex);
                    }
                }

                if (!dataOk)
                {
                    throw new InvalidOperationException(stats.SkippedFiles > 0
                        ? "存在无法读取的文件，本次备份失败。"
                        : "已启用的数据通道未全部成功。");
                }

                if (request.DoDeleteNow)
                {
                    for (var i = 0; i < packages.Length; i++)
                    {
                        if (!request.PackageStore.DeletePackage(packages[i]))
                        {
                            throw new InvalidOperationException("已成功的备份无法立即删除本地包。");
                        }
                    }

                    log.Append(" 已立即删本地包。");
                }

                if (request.AfterSuccess != null)
                {
                    request.AfterSuccess(log);
                }

                request.Task.SavePendingJson(null);
                result.DataOk = true;
                result.Ok = true;
            }
            catch (Exception ex)
            {
                log.Append(" 失败：").Append(ex.Message);
                if (!result.MailAttempted)
                {
                    try
                    {
                        if (request.Notification.ShouldSend(false))
                        {
                            result.MailAttempted = true;
                            request.Notification.Send(
                                false,
                                stats,
                                archiveBytes,
                                false,
                                false,
                                packageCount,
                                log.ToString());
                        }
                    }
                    catch
                    {
                    }
                }
            }
            finally
            {
                request.Source.Dispose();
                result.ResultCode = BackupRunSummary.ResultCode(result.DataOk, result.MailOk);
                request.Task.MarkResult(result.Ok, now);
                request.Task.WriteLog(
                    string.IsNullOrEmpty(request.Mode) ? "auto" : request.Mode,
                    result.ResultCode,
                    log.ToString());
            }

            return result;
        }

        private static long SumArchiveBytes(IList<PendingItem> items)
        {
            long total = 0;
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null)
                {
                    continue;
                }

                if (item.ArchiveBytes > 0)
                {
                    total += item.ArchiveBytes;
                }
                else if (!string.IsNullOrEmpty(item.ZipPath) && File.Exists(item.ZipPath))
                {
                    total += new FileInfo(item.ZipPath).Length;
                }
            }

            return total;
        }

        private static int CountPackages(IList<PendingItem> items)
        {
            var count = 0;
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null)
                {
                    continue;
                }

                count += item.UploadFiles == null || item.UploadFiles.Length == 0
                    ? 1
                    : Math.Max(1, item.UploadFiles.Length - 1);
            }

            return Math.Max(1, count);
        }

        private static string PackagePath(PendingItem item)
        {
            return item == null ? "" : item.PackageDirectory ?? item.ZipPath ?? "";
        }

        private static string ItemLabel(PendingItem item, int index)
        {
            return item == null || string.IsNullOrEmpty(item.Key)
                ? "第" + (index + 1).ToString() + "个包"
                : item.Key;
        }
    }
}
