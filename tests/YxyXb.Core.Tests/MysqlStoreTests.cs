using System;
using System.IO;
using NUnit.Framework;
using YxyXb.Contracts.Dto;
using YxyXb.Core;
using YxyXb.Core.Data;
using YxyXb.Core.Store;

namespace YxyXb.Core.Tests
{
    [TestFixture]
    public sealed class MysqlStoreTests
    {
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "yxy-mysql-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            AppPaths.SetRootForTests(_root);
            AppPaths.EnsureDirectories();
        }

        [TearDown]
        public void TearDown()
        {
            AppPaths.SetRootForTests(null);
            try
            {
                if (Directory.Exists(_root)) Directory.Delete(_root, true);
            }
            catch (IOException)
            {
            }
        }

        [Test]
        public void Instance_And_Task_Roundtrip_Skips_System_Databases()
        {
            using (var db = new SqliteDb(AppPaths.DatabaseFile))
            {
                var id = MysqlInstanceStore.Insert(db, new MysqlInstanceDto
                {
                    Name = "本机",
                    Server = "127.0.0.1",
                    Port = 3306,
                    Username = "root",
                    TimeoutSeconds = 15,
                    Enabled = true
                }, "secret");
                var taskId = MysqlTaskStore.Insert(db, new MysqlTaskDto
                {
                    Name = "每天",
                    InstanceId = id,
                    Enabled = true,
                    DbScope = "user",
                    CycleKind = "day",
                    CycleInterval = 1,
                    NextRunUtc = DateTime.UtcNow
                });
                Assert.That(MysqlInstanceStore.List(db).Count, Is.EqualTo(1));
                Assert.That(MysqlTaskStore.GetById(db, taskId).Name, Is.EqualTo("每天"));
                MysqlInstanceStore.Delete(db, id);
                Assert.That(MysqlTaskStore.List(db).Count, Is.EqualTo(0));
            }
        }
    }
}
