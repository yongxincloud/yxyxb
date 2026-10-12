using System;
using System.IO;
using NUnit.Framework;
using YxyXb.Contracts.Dto;
using YxyXb.Core.Data;
using YxyXb.Core.Store;

namespace YxyXb.Core.Tests
{
    public class PendingStoreTests
    {
        [Test]
        public void Update_KeepsPendingJson()
        {
            var file = Path.Combine(Path.GetTempPath(), "yxy-pending-" + Guid.NewGuid().ToString("N") + ".db");
            try
            {
                using (var db = new SqliteDb(file))
                {
                    var fileId = FileTaskStore.Insert(db, NewFile());
                    FileTaskStore.SavePending(db, fileId, "{\"Items\":[]}");
                    var fileTask = FileTaskStore.GetById(db, fileId);
                    fileTask.Name = "改名";
                    FileTaskStore.Update(db, fileTask);
                    var fileAgain = FileTaskStore.GetById(db, fileId);
                    Assert.That(fileAgain.Name, Is.EqualTo("改名"));
                    Assert.That(fileAgain.PendingJson, Is.EqualTo("{\"Items\":[]}"));

                    var mssqlId = MssqlTaskStore.Insert(db, NewMssql());
                    MssqlTaskStore.SavePending(db, mssqlId, "{\"InstanceId\":1}");
                    var mssql = MssqlTaskStore.GetById(db, mssqlId);
                    mssql.Name = "库改名";
                    MssqlTaskStore.Update(db, mssql);
                    Assert.That(MssqlTaskStore.GetById(db, mssqlId).PendingJson, Is.EqualTo("{\"InstanceId\":1}"));

                    var mysqlId = MysqlTaskStore.Insert(db, NewMysql());
                    MysqlTaskStore.SavePending(db, mysqlId, "{\"InstanceId\":2}");
                    var mysql = MysqlTaskStore.GetById(db, mysqlId);
                    mysql.Name = "mysql改名";
                    MysqlTaskStore.Update(db, mysql);
                    Assert.That(MysqlTaskStore.GetById(db, mysqlId).PendingJson, Is.EqualTo("{\"InstanceId\":2}"));
                    Assert.That(MysqlTaskStore.ListByInstance(db, 2).Count, Is.EqualTo(1));
                }
            }
            finally
            {
                TryDeleteDb(file);
            }
        }

        private static FileTaskDto NewFile()
        {
            return new FileTaskDto
            {
                Name = "文件",
                Enabled = true,
                SourceKind = "local",
                SourcePath = @"D:\src",
                RetryMinutes = 5,
                CycleKind = "day",
                CycleInterval = 1,
                NextRunUtc = DateTime.UtcNow
            };
        }

        private static MssqlTaskDto NewMssql()
        {
            return new MssqlTaskDto
            {
                Name = "库",
                InstanceId = 1,
                Enabled = true,
                DbScope = "user",
                RetryMinutes = 5,
                CycleKind = "day",
                CycleInterval = 1,
                NextRunUtc = DateTime.UtcNow
            };
        }

        private static MysqlTaskDto NewMysql()
        {
            return new MysqlTaskDto
            {
                Name = "mysql",
                InstanceId = 2,
                Enabled = true,
                DbScope = "user",
                RetryMinutes = 5,
                CycleKind = "day",
                CycleInterval = 1,
                NextRunUtc = DateTime.UtcNow
            };
        }

        private static void TryDeleteDb(string file)
        {
            try
            {
                if (File.Exists(file)) File.Delete(file);
                if (File.Exists(file + "-wal")) File.Delete(file + "-wal");
                if (File.Exists(file + "-shm")) File.Delete(file + "-shm");
            }
            catch (IOException)
            {
            }
        }
    }
}
