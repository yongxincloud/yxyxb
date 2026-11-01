using System;
using System.IO;
using NUnit.Framework;
using YxyXb.Core.Data;
using YxyXb.Core.Device;
using YxyXb.Core.Store;

namespace YxyXb.Core.Tests
{
    public class DeviceReportTests
    {
        [Test]
        public void TaskStatus_Idle_WhenNoLogs()
        {
            var file = Path.Combine(Path.GetTempPath(), "yxy-report-" + Guid.NewGuid().ToString("N") + ".db");
            try
            {
                using (var db = new SqliteDb(file))
                {
                    Assert.That(DeviceReport.TaskStatus(db, DateTime.UtcNow), Is.EqualTo("空闲"));
                }
            }
            finally
            {
                TryDelete(file);
            }
        }

        [Test]
        public void TaskStatus_UsesLatestBackupLog()
        {
            var file = Path.Combine(Path.GetTempPath(), "yxy-report-" + Guid.NewGuid().ToString("N") + ".db");
            try
            {
                using (var db = new SqliteDb(file))
                {
                    LogStore.Add(db, "file", "auto", "ok", "每晚全量", "完成");
                    var text = DeviceReport.TaskStatus(db, DateTime.UtcNow);
                    Assert.That(text, Does.Contain("文件备份"));
                    Assert.That(text, Does.Contain("每晚全量"));
                    Assert.That(text, Does.Contain("成功"));
                }
            }
            finally
            {
                TryDelete(file);
            }
        }

        private static void TryDelete(string file)
        {
            try { File.Delete(file); } catch { }
            try { File.Delete(file + "-journal"); } catch { }
        }
    }
}
