using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using YxyXb.Contracts.Dto;
using YxyXb.Core;
using YxyXb.Core.Data;
using YxyXb.Core.Schedule;
using YxyXb.Core.Security;
using YxyXb.Core.Store;
using YxyXb.Media.Disks;
using YxyXb.Media.Sync;

namespace YxyXb.Media
{
    public static class MediaRunner
    {
        private static readonly object Gate = new object();
        private static readonly HashSet<string> Busy = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<long> MountFailLogged = new HashSet<long>();

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

        private static bool Process(SqliteDb db, MediaTaskDto task)
        {
            var nowUtc = DateTime.UtcNow;
            var nowLocal = nowUtc.ToLocalTime();
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

                var start = WindowClock.NextWindowStartLocal(nowLocal, task.WindowStart, task.WindowEnd);
                MediaTaskStore.SetNextRun(db, task.Id, start.ToUniversalTime());
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
            var nowLocal = DateTime.Now;
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
                var live = DiskEnumerator.Find(record.SerialNumber, record.DiskSignature, record.CapacityGb);
                if (HasLetter(live))
                {
                    try
                    {
                        DiskIsolate.Isolate(live, level);
                        LogStore.Add(db, "media", "auto", "ok", "介质" + record.Id, "介质离线时间到，介质仍在线，已再离线 L" + level + "。");
                        MediaStore.SetAutoHeld(db, record.Id, false);
                    }
                    catch (Exception ex)
                    {
                        MediaService.HideLetter(record);
                        var again = DiskEnumerator.Find(record.SerialNumber, record.DiskSignature, record.CapacityGb);
                        if (!HasLetter(again))
                        {
                            MediaStore.SetAutoHeld(db, record.Id, false);
                            LogStore.Add(db, "media", "auto", "ok", "介质" + record.Id, "介质离线时间到，隔离未完成，已去掉盘符。");
                        }
                        else
                        {
                            LogStore.Add(db, "media", "auto", "error", "介质" + record.Id, "介质离线时间到，离线失败：" + ex.Message);
                        }
                    }
                }
                else
                {
                    LogStore.Add(db, "media", "auto", "ok", "介质" + record.Id, "介质离线时间到，介质已不在线。");
                    MediaStore.SetAutoHeld(db, record.Id, false);
                }
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
            if (task.WaitMinutes <= 0)
            {
                FinishSkip(db, task, nowUtc, "介质未就位（不等待），跳过本次。");
                return true;
            }

            if (!task.WaitingSinceUtc.HasValue)
            {
                MediaTaskStore.SetWaiting(db, task.Id, nowUtc);
                return false;
            }

            if (nowLocal >= WindowClock.WindowEndLocal(nowLocal, task.WindowStart, task.WindowEnd))
            {
                FinishSkip(db, task, nowUtc, "到介质离线时间仍未就位，跳过本次。");
                return true;
            }

            if ((nowUtc - task.WaitingSinceUtc.Value).TotalMinutes >= task.WaitMinutes)
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
            var bak = !string.IsNullOrWhiteSpace(task.SourcePath)
                ? task.SourcePath.Trim()
                : (string.IsNullOrEmpty(settings.BakPath) ? AppPaths.BakDirectory : settings.BakPath);
            try
            {
                var refuse = MediaSafety.RefuseWrite(live, bak);
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

                log.Append("任务开始 ").Append(task.WindowStart).Append("，介质离线 ").Append(task.WindowEnd);
                log.Append("，序列号 ").Append(record.SerialNumber);
                log.Append("，挂载 ").Append(letter).Append("，扫描源 ").Append(bak).Append("。");
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

                var tamper = Inspection.LooksTampered(filesRoot, versionsRoot, task.UseSha256);
                if (tamper)
                {
                    MediaStore.SetTamper(db, record.Id, true);
                    log.Append(" 疑似篡改，暂停清理。");
                }

                record = MediaStore.GetById(db, record.Id);
                var zips = PackageSync.ListBakZips(bak);
                var copied = 0;
                var skipped = 0;
                long bytes = 0;
                var stamps = new List<FileStamp>();
                for (var i = 0; i < zips.Count; i++)
                {
                    var rel = PackageSync.RelFromBak(bak, zips[i]);
                    var dest = Path.Combine(filesRoot, rel);
                    var srcStamp = PackageSync.Stamp(zips[i], rel, task.UseSha256);
                    FileStamp destStamp = null;
                    if (File.Exists(dest))
                    {
                        destStamp = PackageSync.Stamp(dest, rel, task.UseSha256);
                    }

                    if (destStamp != null && PackageSync.Same(srcStamp, destStamp, task.UseSha256))
                    {
                        skipped++;
                        stamps.Add(destStamp);
                        continue;
                    }

                    PackageSync.CopyPart(zips[i], dest, task.ConflictMode);
                    var after = PackageSync.Stamp(dest, rel, task.UseSha256);
                    stamps.Add(after);
                    copied++;
                    bytes += after.Size;
                }

                log.Append(" 复制 ").Append(copied).Append("，跳过 ").Append(skipped).Append("，字节 ").Append(bytes).Append("。");

                var stampName = nowUtc.ToLocalTime().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
                var manifest = new VersionManifest
                {
                    Time = stampName,
                    Files = stamps,
                    Copied = copied,
                    Skipped = skipped,
                    Bytes = bytes
                };
                File.WriteAllText(Path.Combine(versionsRoot, stampName + ".json"), JsonUtil.ToJson(manifest));

                var deleted = 0;
                if (record.TamperPending)
                {
                    log.Append(" 因疑似篡改未清理。");
                }
                else
                {
                    deleted = VersionPurge.Apply(filesRoot, versionsRoot, task.RetainVersions, task.RetainDays, true);
                    log.Append(" 删除版本 ").Append(deleted).Append("。");
                }

                var inPeriod = WindowClock.IsInWindow(nowUtc.ToLocalTime(), task.WindowStart, task.WindowEnd);
                if (inPeriod)
                {
                    MediaStore.SetAutoHeld(db, record.Id, true);
                }

                if (task.IsolateAfter)
                {
                    try
                    {
                        DiskIsolate.FlushLockDismount(letter);
                    }
                    catch (Exception flushEx)
                    {
                        log.Append(" 刷新未完成：").Append(flushEx.Message).Append("。");
                    }

                    var isoRefuse = MediaSafety.RefuseIsolate(live, bak, true);
                    if (isoRefuse != null)
                    {
                        LogStore.Add(db, "security", mode, "error", task.Name, isoRefuse);
                        log.Append(" 未隔离：").Append(isoRefuse);
                    }
                    else
                    {
                        var level = task.IsolateLevel < 1 ? 2 : task.IsolateLevel;
                        DiskIsolate.Isolate(live, level);
                        log.Append(" 任务完成，已立即离线。");
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
