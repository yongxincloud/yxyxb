using System;
using System.IO;
using NUnit.Framework;
using YxyXb.Contracts.Dto;
using YxyXb.Core.Data;
using YxyXb.Core.Store;

namespace YxyXb.Core.Tests
{
    public class PackageSettingsStoreTests
    {
        [Test]
        public void SettingsAndFileTask_KeepPartSizeBytes()
        {
            var file = Path.Combine(Path.GetTempPath(), "yxy-partsize-" + Guid.NewGuid().ToString("N") + ".db");
            try
            {
                using (var db = new SqliteDb(file))
                {
                    var settings = SettingsStore.Get(db);
                    settings.PartSizeBytes = 8L << 30;
                    SettingsStore.Save(db, settings);
                    Assert.That(SettingsStore.Get(db).PartSizeBytes, Is.EqualTo(8L << 30));

                    var task = new FileTaskDto
                    {
                        Name = "分卷任务",
                        Enabled = true,
                        SourceKind = "local",
                        SourcePath = @"D:\src",
                        RetryMinutes = 5,
                        CycleKind = "day",
                        CycleInterval = 1,
                        NextRunUtc = DateTime.UtcNow,
                        PartSizeBytes = 2L << 30
                    };
                    task.Id = FileTaskStore.Insert(db, task);
                    Assert.That(FileTaskStore.GetById(db, task.Id).PartSizeBytes, Is.EqualTo(2L << 30));

                    var mssql = new MssqlTaskDto
                    {
                        Name = "MSSQL",
                        InstanceId = 1,
                        Enabled = true,
                        DbScope = "user",
                        RetryMinutes = 5,
                        CycleKind = "day",
                        CycleInterval = 1,
                        NextRunUtc = DateTime.UtcNow,
                        PartSizeBytes = 3L << 30
                    };
                    mssql.Id = MssqlTaskStore.Insert(db, mssql);
                    Assert.That(MssqlTaskStore.GetById(db, mssql.Id).PartSizeBytes, Is.EqualTo(3L << 30));

                    var mysql = new MysqlTaskDto
                    {
                        Name = "MySQL",
                        InstanceId = 1,
                        Enabled = true,
                        DbScope = "user",
                        RetryMinutes = 5,
                        CycleKind = "day",
                        CycleInterval = 1,
                        NextRunUtc = DateTime.UtcNow,
                        PartSizeBytes = 4L << 30
                    };
                    mysql.Id = MysqlTaskStore.Insert(db, mysql);
                    Assert.That(MysqlTaskStore.GetById(db, mysql.Id).PartSizeBytes, Is.EqualTo(4L << 30));
                }
            }
            finally
            {
                TryDelete(file);
            }
        }

        private static void TryDelete(string file)
        {
            foreach (var path in new[] { file, file + "-wal", file + "-shm" })
            {
                try { if (File.Exists(path)) File.Delete(path); } catch (IOException) { }
            }
        }
    }
}
