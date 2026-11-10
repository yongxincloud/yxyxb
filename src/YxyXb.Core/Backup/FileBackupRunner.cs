using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using YxyXb.Contracts.Dto;
using YxyXb.Core.Data;
using YxyXb.Core.Store;

namespace YxyXb.Core.Backup
{
    public static class FileBackupRunner
    {
        public static int ExecuteDue(SqliteDb db)
        {
            var due = FileTaskStore.GetDue(db, DateTime.UtcNow);
            var count = 0;
            for (var i = 0; i < due.Count; i++)
            {
                if (!FileTaskStore.TryClaim(db, due[i].Id, DateTime.UtcNow))
                {
                    continue;
                }

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

            if (!FileTaskStore.TryClaim(db, task.Id, DateTime.UtcNow))
            {
                return "该任务正在执行，请稍候。";
            }

            ExecuteOne(db, task, "manual");
            var entry = LogStore.LatestForTask(db, "file", task.Name);
            if (entry == null)
            {
                return "已提交，但没有写到日志。请到「日志 → 文件备份」查看。";
            }

            return BackupRunSummary.ManualMessage(entry.Result) + " " + entry.Message;
        }

        public static void ExecuteOne(SqliteDb db, FileTaskDto task)
        {
            ExecuteOne(db, task, "auto");
        }

        public static void ExecuteOne(SqliteDb db, FileTaskDto task, string mode)
        {
            BackupRunLifecycle.Execute(new BackupRunRequest
            {
                Task = new FileBackupTaskState(db, task),
                Source = new FileBackupSource(db, task),
                Delivery = new FileBackupDelivery(db, task.Name),
                Notification = new FileBackupNotification(db, task.DoEmail, task.Name),
                PackageStore = new BackupPackageStore(),
                DoNetwork = task.DoNetwork,
                DoFtp = task.DoFtp,
                DoCloud = task.DoCloud,
                CloudProvider = task.CloudProvider,
                DoDeleteNow = task.DoDeleteNow,
                Mode = mode,
                AfterSuccess = delegate(StringBuilder log)
                {
                    if (!string.IsNullOrEmpty(task.PostProgram) && File.Exists(task.PostProgram))
                    {
                        Process.Start(new ProcessStartInfo(task.PostProgram) { UseShellExecute = true });
                        log.Append(" 已运行后续程序。");
                    }
                }
            });
        }
    }
}
