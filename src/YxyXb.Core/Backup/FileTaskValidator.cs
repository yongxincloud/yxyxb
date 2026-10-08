using System;
using System.IO;
using YxyXb.Contracts.Dto;
using YxyXb.Core.Schedule;

namespace YxyXb.Core.Backup
{
    public static class FileTaskValidator
    {
        public static string Validate(FileTaskDto task, string bakPath)
        {
            if (task == null)
            {
                return "任务为空。";
            }

            if (string.IsNullOrWhiteSpace(task.Name))
            {
                return "请填写任务名称。";
            }

            if (string.IsNullOrWhiteSpace(task.SourcePath))
            {
                return "请填写源路径。";
            }

            var kind = task.SourceKind ?? "local";
            if (kind == "local")
            {
                if (!Directory.Exists(task.SourcePath))
                {
                    return "源目录不存在。";
                }

                if (BackupNaming.IsLoop(task.SourcePath, bakPath))
                {
                    return "源路径不能回环到备份目录。";
                }
            }
            else if (kind == "unc")
            {
                if (!task.SourcePath.StartsWith(@"\\", StringComparison.Ordinal))
                {
                    return "UNC 源请填写 \\\\服务器\\共享\\目录。";
                }

                if (BackupNaming.IsLoop(task.SourcePath, bakPath))
                {
                    return "源路径不能回环到备份目录。";
                }
            }
            else if (kind == "ftp")
            {
                if (string.IsNullOrWhiteSpace(task.SourcePath))
                {
                    return "请填写 FTP 远程目录。";
                }
            }
            else
            {
                return "未知源类型。";
            }

            return ScheduleCalculator.Validate(task.CycleKind, task.CycleInterval, task.NextRunUtc);
        }
    }
}
