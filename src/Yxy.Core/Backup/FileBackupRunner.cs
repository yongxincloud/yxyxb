using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Yxy.Contracts.Dto;
using Yxy.Core.Data;
using Yxy.Core.Store;
using Yxy.Core.Transfer;

namespace Yxy.Core.Backup
{
    public static class FileBackupRunner
    {
        public static int ExecuteDue(SqliteDb db)
        {
            var due = FileTaskStore.GetDue(db, DateTime.UtcNow);
            var count = 0;
            for (var i = 0; i < due.Count; i++)
            {
                ExecuteOne(db, due[i]);
                count++;
            }

            return count;
        }

        public static string ExecuteNow(SqliteDb db, long taskId)
        {
            var task = FileTaskStore.GetById(db, taskId);
            if (task == null)
            {
                return "任务不存在。";
            }

            ExecuteOne(db, task, "manual");
            var entry = LogStore.LatestForTask(db, "file", task.Name);
            if (entry == null)
            {
                return "已提交，但没有写到日志。请到「日志 → 文件备份」查看。";
            }

            return (entry.Result == "ok" ? "执行成功。" : "执行失败。") + " " + entry.Message;
        }

        public static void ExecuteOne(SqliteDb db, FileTaskDto task)
        {
            ExecuteOne(db, task, "auto");
        }

        public static void ExecuteOne(SqliteDb db, FileTaskDto task, string mode)
        {
            var now = DateTime.UtcNow;
            var log = new StringBuilder();
            var ok = false;
            string zipPath = null;
            string tempRoot = null;
            try
            {
                var settings = SettingsStore.Get(db);
                var bak = string.IsNullOrEmpty(settings.BakPath) ? AppPaths.BakDirectory : settings.BakPath;
                Directory.CreateDirectory(bak);

                var error = FileTaskValidator.Validate(task, bak);
                if (error != null && task.SourceKind == "local")
                {
                    throw new InvalidOperationException(error);
                }

                var sourceRoot = PrepareSource(db, task, log, out tempRoot);
                if (BackupNaming.IsLoop(sourceRoot, bak))
                {
                    throw new InvalidOperationException("源路径不能回环到备份目录。");
                }

                var skipped = new List<string>();
                var files = SourceWalker.WalkLocal(sourceRoot, skipped);
                log.Append("文件 ").Append(files.Count).Append("，跳过 ").Append(skipped.Count).Append("。");

                var flags = settings.NameFlags == 0 ? NameParts.DefaultFlags : settings.NameFlags;
                var parts = NameParts.FromFlags(flags);
                var folder = BackupNaming.FileFolder(bak, settings.SoftwareId, task.Name, parts);
                zipPath = Path.Combine(folder, BackupNaming.FileZipName(settings.SoftwareId, task.Name, now, parts));
                ZipPacker.Pack(files, zipPath, settings.ZipPassword, settings.ZipLevel);
                log.Append(" 产出 ").Append(zipPath);
                log.Append(string.IsNullOrEmpty(settings.ZipPassword) ? "。" : "，已用压缩密码。");

                var distOk = DistChannel.UncAndFtp(db, task.DoNetwork, task.DoFtp, zipPath, log);
                if (!DistChannel.Cloud(db, task.DoCloud, task.CloudProvider, "file", task.Name, zipPath, log))
                {
                    distOk = false;
                }

                if (!DistChannel.Finish(db, task.DoEmail, task.DoDeleteNow, task.Name, new List<string> { zipPath }, distOk, log))
                {
                    throw new InvalidOperationException(log.ToString());
                }

                if (!string.IsNullOrEmpty(task.PostProgram) && File.Exists(task.PostProgram))
                {
                    Process.Start(new ProcessStartInfo(task.PostProgram) { UseShellExecute = true });
                    log.Append(" 已运行后续程序。");
                }

                ok = true;
            }
            catch (Exception ex)
            {
                log.Append(" 失败：").Append(ex.Message);
                if (task.DoEmail)
                {
                    try
                    {
                        MailUtil.SendResult(SettingsStore.GetXfer<SmtpSettingsDto>(db, "smtp"), task.Name, false, log.ToString());
                    }
                    catch
                    {
                    }
                }
            }
            finally
            {
                TryDeleteDir(tempRoot);
                FileTaskStore.MarkResult(db, task, ok, now);
                LogStore.Add(db, "file", string.IsNullOrEmpty(mode) ? "auto" : mode, ok ? "ok" : "error", task.Name, log.ToString());
            }
        }

        private static string PrepareSource(SqliteDb db, FileTaskDto task, StringBuilder log, out string tempRoot)
        {
            tempRoot = null;
            var kind = task.SourceKind ?? "local";
            if (kind == "unc")
            {
                var unc = SettingsStore.GetXfer<UncSettingsDto>(db, "unc");
                unc.Path = task.SourcePath;
                UncShare.Test(unc);
                return task.SourcePath;
            }

            if (kind == "ftp")
            {
                var ftp = SettingsStore.GetXfer<FtpSettingsDto>(db, "ftp");
                ftp.RemoteDir = task.SourcePath;
                tempRoot = Path.Combine(Path.GetTempPath(), "yxy-ftp-" + Guid.NewGuid().ToString("N"));
                FtpUtil.DownloadTree(ftp, task.SourcePath, tempRoot);
                log.Append("已下载 FTP 源。");
                return tempRoot;
            }

            return task.SourcePath;
        }

        private static void TryDeleteDir(string path)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                return;
            }

            try
            {
                Directory.Delete(path, true);
            }
            catch (IOException)
            {
            }
        }
    }
}
