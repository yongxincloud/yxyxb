using System;
using YxyXb.Core.Data;
using YxyXb.Core.Store;

namespace YxyXb.Core.Device
{
    public static class DeviceReport
    {
        public static string TaskStatus(SqliteDb db, DateTime utcNow)
        {
            if (db == null)
            {
                return "空闲";
            }

            if (TaskRunLock.AnyActive(db, utcNow))
            {
                return "运行中";
            }

            var latest = LogStore.ListAny(db, new[] { "file", "mssql", "mysql", "media" }, 1);
            if (latest.Count == 0)
            {
                return "空闲";
            }

            var log = latest[0];
            var text = TypeName(log.Type) + NamePart(log.TaskName) + " · " + ResultName(log.Result) + " · " + WhenLocal(log.CreatedUtc);
            return text.Length <= 80 ? text : text.Substring(0, 80);
        }

        private static string TypeName(string type)
        {
            if (type == "mssql") return "MSSQL";
            if (type == "mysql") return "MySQL";
            if (type == "media") return "离线介质";
            return "文件备份";
        }

        private static string ResultName(string result)
        {
            if (result == "ok") return "成功";
            if (result == "error") return "失败";
            return string.IsNullOrEmpty(result) ? "完成" : result;
        }

        private static string NamePart(string taskName)
        {
            return string.IsNullOrEmpty(taskName) ? "" : " · " + taskName;
        }

        private static string WhenLocal(DateTime utc)
        {
            var local = utc.Kind == DateTimeKind.Utc ? utc.ToLocalTime() : utc;
            return local.ToString("yyyy-MM-dd HH:mm");
        }
    }
}
