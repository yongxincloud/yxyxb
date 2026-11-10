using System;
using YxyXb.Contracts.Dto;
using YxyXb.Core.Backup;
using YxyXb.Core.Data;
using YxyXb.Core.Store;

namespace YxyXb.MySql
{
    public static class MysqlRunner
    {
        public static int ExecuteDue(SqliteDb db)
        {
            var due = MysqlTaskStore.GetDue(db, DateTime.UtcNow);
            var n = 0;
            for (var i = 0; i < due.Count; i++)
            {
                if (!MysqlTaskStore.TryClaim(db, due[i].Id, DateTime.UtcNow))
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
            var task = MysqlTaskStore.GetById(db, taskId);
            if (task == null)
            {
                return "任务不存在。";
            }

            if (!MysqlTaskStore.TryClaim(db, task.Id, DateTime.UtcNow))
            {
                return "该任务正在执行，请稍候。";
            }

            ExecuteOne(db, task, "manual");
            var entry = LogStore.LatestForTask(db, "mysql", task.Name);
            if (entry == null)
            {
                return "已提交，但没有写到日志。请到「日志 → 数据库备份」查看。";
            }

            return BackupRunSummary.ManualMessage(entry.Result) + " " + entry.Message;
        }

        public static void ExecuteOne(SqliteDb db, MysqlTaskDto task, string mode)
        {
            BackupRunLifecycle.Execute(new BackupRunRequest
            {
                Task = new MysqlBackupTaskState(db, task),
                Source = new MysqlBackupSource(db, task),
                Delivery = new MysqlBackupDelivery(db, task.Name),
                Notification = new MysqlBackupNotification(db, task.DoEmail, task.Name),
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
