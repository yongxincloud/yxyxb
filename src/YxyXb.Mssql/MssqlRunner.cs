using System;
using YxyXb.Contracts.Dto;
using YxyXb.Core.Backup;
using YxyXb.Core.Data;
using YxyXb.Core.Store;

namespace YxyXb.Mssql
{
    public static class MssqlRunner
    {
        public static int ExecuteDue(SqliteDb db)
        {
            var due = MssqlTaskStore.GetDue(db, DateTime.UtcNow);
            var n = 0;
            for (var i = 0; i < due.Count; i++)
            {
                if (!MssqlTaskStore.TryClaim(db, due[i].Id, DateTime.UtcNow))
                {
                    continue;
                }

                ExecuteOne(db, due[i], "auto");
                n++;
            }

            return n;
        }

        public static string ExecuteNow(SqliteDb db, long taskId)
        {
            var task = MssqlTaskStore.GetById(db, taskId);
            if (task == null)
            {
                return "任务不存在。";
            }

            if (!MssqlTaskStore.TryClaim(db, task.Id, DateTime.UtcNow))
            {
                return "该任务正在执行，请稍候。";
            }

            ExecuteOne(db, task, "manual");
            var entry = LogStore.LatestForTask(db, "mssql", task.Name);
            if (entry == null)
            {
                return "已提交，但没有写到日志。请到「日志 → 数据库备份」查看。";
            }

            return BackupRunSummary.ManualMessage(entry.Result) + " " + entry.Message;
        }

        public static void ExecuteOne(SqliteDb db, MssqlTaskDto task, string mode)
        {
            BackupRunLifecycle.Execute(new BackupRunRequest
            {
                Task = new MssqlBackupTaskState(db, task),
                Source = new MssqlBackupSource(db, task),
                Delivery = new MssqlBackupDelivery(db, task.Name),
                Notification = new MssqlBackupNotification(db, task.DoEmail, task.Name),
                PackageStore = new BackupPackageStore(),
                DoNetwork = task.DoNetwork,
                DoFtp = task.DoFtp,
                DoCloud = task.DoCloud,
                CloudProvider = task.CloudProvider,
                DoDeleteNow = task.DoDeleteNow,
                Mode = mode
            });
        }
    }
}
