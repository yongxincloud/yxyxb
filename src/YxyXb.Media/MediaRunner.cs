using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using YxyXb.Contracts.Dto;
using YxyXb.Contracts.Time;
using YxyXb.Core;
using YxyXb.Core.Data;
using YxyXb.Core.Schedule;
using YxyXb.Core.Store;
using YxyXb.Media.Disks;

namespace YxyXb.Media
{
    public static class MediaRunner
    {
        private static readonly object Gate = new object();
        private static readonly HashSet<string> Busy = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<long> MountFailLogged = new HashSet<long>();
        private static readonly MediaOfflineLifecycle OfflineLifecycle =
            new MediaOfflineLifecycle(new SystemMediaDiskRuntime());

        public static int ExecuteDue(SqliteDb db)
        {
            ApplyLetterCommands(db);
            SyncOfflineDeadline(db);
            var tasks = MediaTaskStore.ListEnabled(db);
            var n = 0;
            for (var i = 0; i < tasks.Count; i++)
            {
                if (Process(db, tasks[i]))
                {
                    n++;
                }
            }

            return n;
        }

        public static string ExecuteNow(SqliteDb db, long taskId)
        {
            var task = MediaTaskStore.GetById(db, taskId);
            if (task == null)
            {
                return "任务不存在。";
            }

            var nowUtc = DateTime.UtcNow;
            var record = MediaStore.GetById(db, task.MediaId);
            if (record == null)
            {
                return "登记介质已不存在。";
            }

            var live = ResolveLive(record);
            if (live == null)
            {
                return "盘未接入，无法立即执行。";
            }

            RunCopy(db, task, record, live, nowUtc, "manual");
            var entry = LogStore.LatestForTask(db, "media", task.Name);
            if (entry == null)
            {
                return "已提交，但没有写到日志。请到「日志 → 离线介质」查看。";
            }

            var head = entry.Result == "ok" ? "执行成功。" : "执行失败。";
            return head + " " + entry.Message + "\n\n也可在「日志 → 离线介质」查看。隔离只是摘盘符/离线，不会格式化。若 Windows 提示格式化，点取消。";
        }

        private static DateTime NextOpenLocal(DateTime nowLocal, MediaTaskDto task)
        {
            return MediaSchedulePolicy.NextOpenLocal(nowLocal, task);
        }

        private static bool Process(SqliteDb db, MediaTaskDto task)
        {
            var nowUtc = DateTime.UtcNow;
            var nowLocal = ChinaTime.ToChina(nowUtc);
            var due = !task.NextRunUtc.HasValue || task.NextRunUtc.Value <= nowUtc || task.WaitingSinceUtc.HasValue;
            if (!due)
            {
                return false;
            }

            var record = MediaStore.GetById(db, task.MediaId);
            if (record == null)
            {
                FinishFail(db, task, nowUtc, "登记介质已不存在。");
                return true;
            }

            if (!WindowClock.IsInWindow(nowLocal, task.WindowStart, task.WindowEnd))
            {
                if (task.WaitingSinceUtc.HasValue)
                {
                    FinishSkip(db, task, nowUtc, "到介质离线时间仍未就位，跳过本次。");
                    return true;
                }

                var start = NextOpenLocal(nowLocal, task);
                MediaTaskStore.SetNextRun(db, task.Id, DateTime.SpecifyKind(start, DateTimeKind.Local).ToUniversalTime());
                LogStore.Add(db, "media", "auto", "ok", task.Name, "未到任务开始时间，已改到 " + start.ToString("HH:mm", CultureInfo.InvariantCulture) + " 再检查介质。");
                return true;
            }

            var live = DiskEnumerator.Find(record.SerialNumber, record.DiskSignature, record.CapacityGb);
            if (!HasLetter(live))
            {
                string letter;
                var error = MediaService.ShowLetter(record, out letter);
                if (error != null)
                {
                    if (MountFailLogged.Add(record.Id))
                    {
                        LogStore.Add(db, "media", "auto", "error", task.Name, "任务开始后介质未在线，挂载未成功：" + error + " 将继续等待。");
                    }

                    return WaitOrSkip(db, task, nowUtc, nowLocal);
                }

                MountFailLogged.Remove(record.Id);
                LogStore.Add(db, "media", "auto", "ok", task.Name, "任务开始后介质未在线，已挂载 " + letter + "，开始执行。");
                live = ResolveLive(record);
            }
            else
            {
                MountFailLogged.Remove(record.Id);
                LogStore.Add(db, "media", "auto", "ok", task.Name, "任务开始后介质已在线，开始执行。");
            }

            if (!HasLetter(live))
            {
                return WaitOrSkip(db, task, nowUtc, nowLocal);
            }

            MediaStore.SetAutoHeld(db, record.Id, true);
            RunCopy(db, task, record, live, nowUtc, "auto");
            return true;
        }

        private static void ApplyLetterCommands(SqliteDb db)
        {
            var media = MediaStore.List(db);
            for (var i = 0; i < media.Count; i++)
            {
                var record = media[i];
                if (string.IsNullOrEmpty(record.LetterCmd))
                {
                    continue;
                }

                var message = RunLetterCommand(record);
                MediaStore.FinishLetter(db, record.Id, message);
            }
        }

        private static string RunLetterCommand(MediaRecordDto record)
        {
            var live = DiskEnumerator.Find(record.SerialNumber, record.DiskSignature, record.CapacityGb);
            if (live == null)
            {
                return "盘未接入，无法切换。";
            }

            try
            {
                if (string.Equals(record.LetterCmd, "hide", StringComparison.OrdinalIgnoreCase))
                {
                    var letter = live.DriveLetter;
                    DiskIsolate.HideFromExplorer(live);
                    Thread.Sleep(1200);
                    var again = DiskEnumerator.Find(record.SerialNumber, record.DiskSignature, record.CapacityGb);
                    if (again != null && !string.IsNullOrEmpty(again.DriveLetter) && Directory.Exists(again.DriveLetter.TrimEnd('\\') + "\\"))
                    {
                        return "盘符 " + again.DriveLetter + " 仍可用，离线没有生效。";
                    }

                    return "已卷离线" + (string.IsNullOrEmpty(letter) ? "。" : "，已去掉 " + letter + "。资源管理器里不应再显示，卷也不能被打开。");
                }

                string shown;
                var error = MediaService.ShowLetter(record, out shown);
                return error ?? ("已在线，盘符 " + shown + "。");
            }
            catch (Exception ex)
            {
                return "切换失败：" + ex.Message;
            }
        }

        private static void SyncOfflineDeadline(SqliteDb db)
        {
            var tasks = MediaTaskStore.ListEnabled(db);
            var nowLocal = ChinaTime.Now;
            var bakPath = AppPaths.DisplayBakPath(SettingsStore.Get(db).BakPath);
            var open = new HashSet<long>();
            for (var i = 0; i < tasks.Count; i++)
            {
                if (WindowClock.IsInWindow(nowLocal, tasks[i].WindowStart, tasks[i].WindowEnd))
                {
                    open.Add(tasks[i].MediaId);
                }
            }

            var media = MediaStore.List(db);
            for (var i = 0; i < media.Count; i++)
            {
                var record = media[i];
                if (open.Contains(record.Id) || !record.AutoHeld || !string.IsNullOrEmpty(record.LetterCmd))
                {
                    continue;
                }

                if (IsCopyBusy(record))
                {
                    continue;
                }

                var level = OfflineLevelFor(tasks, record.Id);
                var offline = OfflineLifecycle.Run(record, level, bakPath, false);
                if (offline.Status == MediaOfflineStatus.Refused)
                {
                    LogStore.Add(db, "security", "auto", "error", "介质" + record.Id, offline.Message);
                }
                else if (offline.Succeeded)
                {
                    MediaStore.SetAutoHeld(db, record.Id, false);
                    LogStore.Add(db, "media", "auto", "ok", "介质" + record.Id,
                        "介质离线时间到，" + offline.Message);
                }
                else
                {
                    LogStore.Add(db, "media", "auto", "error", "介质" + record.Id,
                        "介质离线时间到，" + offline.Message);
                }
            }
        }

        private static void ForceOffline(SqliteDb db, MediaRecordDto record, int level, string reason)
        {
            if (record == null)
            {
                return;
            }

            if (level < 1)
            {
                level = 2;
            }

            var bakPath = AppPaths.DisplayBakPath(SettingsStore.Get(db).BakPath);
            var offline = OfflineLifecycle.Run(record, level, bakPath, false);
            if (offline.Status == MediaOfflineStatus.Refused)
            {
                LogStore.Add(db, "security", "auto", "error", "介质" + record.Id, offline.Message);
                return;
            }

            if (offline.Succeeded)
            {
                MediaStore.SetAutoHeld(db, record.Id, false);
                LogStore.Add(db, "media", "auto", "ok", "介质" + record.Id,
                    reason + offline.Message);
            }
            else
            {
                LogStore.Add(db, "media", "auto", "error", "介质" + record.Id,
                    reason + offline.Message);
            }
        }

        private static int OfflineLevelFor(IList<MediaTaskDto> tasks, long mediaId)
        {
            var level = 0;
            for (var i = 0; i < tasks.Count; i++)
            {
                if (tasks[i].MediaId == mediaId && tasks[i].IsolateLevel > level)
                {
                    level = tasks[i].IsolateLevel;
                }
            }

            return level < 1 ? 2 : level;
        }

        private static bool IsCopyBusy(MediaRecordDto record)
        {
            var key = record.SerialNumber + "|" + record.DiskSignature;
            lock (Gate)
            {
                return Busy.Contains(key);
            }
        }

        private static LiveDiskDto ResolveLive(MediaRecordDto record)
        {
            var live = DiskEnumerator.Find(record.SerialNumber, record.DiskSignature, record.CapacityGb);
            if (live == null)
            {
                return null;
            }

            if (HasLetter(live))
            {
                return live;
            }

            try
            {
                DiskIsolate.BringOnline(live, record.VolumeGuid);
            }
            catch (InvalidOperationException)
            {
            }

            Thread.Sleep(1500);
            var again = DiskEnumerator.Find(record.SerialNumber, record.DiskSignature, record.CapacityGb);
            if (again != null)
            {
                live = again;
            }

            if (string.IsNullOrEmpty(live.VolumeGuid) && !string.IsNullOrEmpty(record.VolumeGuid))
            {
                live.VolumeGuid = record.VolumeGuid;
            }

            return live;
        }

        private static bool HasLetter(LiveDiskDto live)
        {
            return live != null
                && !string.IsNullOrEmpty(live.DriveLetter)
                && Directory.Exists(live.DriveLetter.TrimEnd('\\') + "\\");
        }

        private static bool WaitOrSkip(SqliteDb db, MediaTaskDto task, DateTime nowUtc, DateTime nowLocal)
        {
            var decision = MediaSchedulePolicy.DecideWait(task, nowUtc, nowLocal);
            if (decision == MediaWaitDecision.SkipNoWait)
            {
                FinishSkip(db, task, nowUtc, "介质未就位（不等待），跳过本次。");
                return true;
            }

            if (decision == MediaWaitDecision.StartWaiting)
            {
                MediaTaskStore.SetWaiting(db, task.Id, nowUtc);
                return false;
            }

            if (decision == MediaWaitDecision.SkipWindowEnded)
            {
                FinishSkip(db, task, nowUtc, "到介质离线时间仍未就位，跳过本次。");
                return true;
            }

            if (decision == MediaWaitDecision.SkipTimedOut)
            {
                FinishSkip(db, task, nowUtc, "等待就位超时，跳过本次。");
                return true;
            }

            return false;
        }

        private static void RunCopy(SqliteDb db, MediaTaskDto task, MediaRecordDto record, LiveDiskDto live, DateTime nowUtc, string mode)
        {
            var key = record.SerialNumber + "|" + record.DiskSignature;
            lock (Gate)
            {
                if (Busy.Contains(key))
                {
                    return;
                }

                Busy.Add(key);
            }

            var log = new StringBuilder();
            var settings = SettingsStore.Get(db);
            var source = (task.SourcePath ?? "").Trim();
            var bakDir = AppPaths.DisplayBakPath(settings.BakPath);
            try
            {
                if (string.IsNullOrEmpty(bakDir))
                {
                    throw new InvalidOperationException("未设置备份目录。请先在「设置 → 备份存储」选择一个已有磁盘上的文件夹。");
                }

                var refuse = MediaSafety.RefuseWrite(live, bakDir);
                if (refuse != null)
                {
                    LogStore.Add(db, "security", "auto", "error", task.Name, refuse);
                    throw new InvalidOperationException(refuse);
                }

                var letter = DiskMount.EnsureLetter(live);
                var root = string.IsNullOrEmpty(letter) ? null : letter.TrimEnd('\\') + "\\";
                if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
                {
                    throw new InvalidOperationException("没有可用盘符，本次不写入。");
                }

                MediaStore.SetAutoHeld(db, record.Id, true);
                log.Append("任务开始 ").Append(task.WindowStart).Append("，介质离线 ").Append(task.WindowEnd);
                log.Append("，序列号 ").Append(record.SerialNumber);
                log.Append("，挂载 ").Append(letter).Append("，打包文件夹 ").Append(source).Append("。");
                var marker = VolumeMarkerStore.Read(root);
                if (marker == null)
                {
                    throw new InvalidOperationException("标记缺失，拒绝写入。");
                }

                if (!string.Equals(marker.Serial, record.SerialNumber, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("序列号不符，拒绝写入。");
                }

                MediaStore.Touch(db, record.Id, live.VolumeGuid);
                var destRoot = Path.Combine(root, string.IsNullOrEmpty(task.TargetFolder) ? "用心云循备" : task.TargetFolder);
                var filesRoot = Path.Combine(destRoot, "files");
                var versionsRoot = Path.Combine(destRoot, "versions");
                Directory.CreateDirectory(filesRoot);
                Directory.CreateDirectory(versionsRoot);

                var packageRun = new MediaPackageLifecycle(new FileSystemMediaPackageRuntime(settings))
                    .Run(task, source, bakDir, filesRoot, versionsRoot, nowUtc, record.TamperPending);
                if (packageRun.TamperPending)
                {
                    MediaStore.SetTamper(db, record.Id, true);
                    log.Append(" 因疑似篡改未清理。");
                }

                record = MediaStore.GetById(db, record.Id);
                log.Append(" 已打包 ").Append(packageRun.ZipPath).Append("。");
                log.Append(" 复制 ").Append(packageRun.Copied).Append("，跳过 ").Append(packageRun.Skipped)
                    .Append("，字节 ").Append(packageRun.Bytes).Append("。");
                if (packageRun.LocalRemoved)
                    log.Append(" 已删除本地压缩包。");
                if (!packageRun.TamperPending)
                    log.Append(" 删除版本 ").Append(packageRun.DeletedVersions).Append("。");

                var inPeriod = WindowClock.IsInWindow(ChinaTime.ToChina(nowUtc), task.WindowStart, task.WindowEnd);
                if (inPeriod)
                {
                    MediaStore.SetAutoHeld(db, record.Id, true);
                }

                if (task.IsolateAfter)
                {
                    var offline = OfflineLifecycle.Run(record, task.IsolateLevel, bakDir, true);
                    if (!string.IsNullOrEmpty(offline.Warning))
                        log.Append(" ").Append(offline.Warning);
                    if (offline.Status == MediaOfflineStatus.Refused)
                    {
                        LogStore.Add(db, "security", mode, "error", task.Name, offline.Message);
                        log.Append(" 未隔离：").Append(offline.Message);
                    }
                    else if (!offline.Succeeded)
                    {
                        throw new InvalidOperationException(offline.Message);
                    }
                    else
                    {
                        log.Append(" 任务完成，").Append(offline.Message);
                    }
                }
                else if (inPeriod)
                {
                    log.Append(" 未勾选完成后立即离线，留到介质离线时间再离线。");
                }

                LogStore.Add(db, "media", mode, "ok", task.Name, log.ToString());
                MediaTaskStore.MarkResult(db, task, true, nowUtc);
            }
            catch (Exception ex)
            {
                log.Append(" 失败：").Append(ex.Message);
                LogStore.Add(db, "media", mode, "error", task.Name, log.ToString());
                MediaTaskStore.MarkResult(db, task, false, nowUtc);
                if (!WindowClock.IsInWindow(ChinaTime.Now, task.WindowStart, task.WindowEnd))
                {
                    ForceOffline(db, record, task.IsolateLevel, "任务失败且已到介质离线时间，");
                }
            }
            finally
            {
                lock (Gate)
                {
                    Busy.Remove(key);
                }
            }
        }

        private static void FinishSkip(SqliteDb db, MediaTaskDto task, DateTime nowUtc, string message)
        {
            LogStore.Add(db, "media", "auto", "ok", task.Name, message);
            MediaTaskStore.MarkResult(db, task, true, nowUtc);
        }

        private static void FinishFail(SqliteDb db, MediaTaskDto task, DateTime nowUtc, string message)
        {
            LogStore.Add(db, "media", "auto", "error", task.Name, message);
            MediaTaskStore.MarkResult(db, task, false, nowUtc);
        }
    }
}
