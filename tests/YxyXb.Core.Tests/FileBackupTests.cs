using System;
using System.IO;
using NUnit.Framework;
using YxyXb.Contracts.Dto;
using YxyXb.Core;
using YxyXb.Core.Backup;
using YxyXb.Core.Data;
using YxyXb.Core.Schedule;
using YxyXb.Core.Store;

namespace YxyXb.Core.Tests
{
    [TestFixture]
    public sealed class FileBackupTests
    {
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "yxy-b-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            AppPaths.SetRootForTests(_root);
        }

        [TearDown]
        public void TearDown()
        {
            AppPaths.SetRootForTests(null);
            try
            {
                if (Directory.Exists(_root))
                {
                    Directory.Delete(_root, true);
                }
            }
            catch (IOException)
            {
            }
        }

        [Test]
        public void Schedule_Once_HasNoNext()
        {
            Assert.That(ScheduleCalculator.NextAfter("once", 1, DateTime.UtcNow), Is.Null);
            Assert.That(ScheduleCalculator.NextAfter("day", 1, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
                Is.EqualTo(new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc)));
        }

        [Test]
        public void Naming_SkipsUncheckedParts()
        {
            var parts = NameParts.FromFlags(NameParts.FileTask | NameParts.DirKind);
            var folder = BackupNaming.FileFolder(@"D:\bak", "用心云循备", "日报", parts);
            Assert.That(folder, Is.EqualTo(@"D:\bak\文件备份"));
            var zip = BackupNaming.FileZipName("用心云循备", "日报", new DateTime(2026, 9, 24, 2, 0, 0, DateTimeKind.Utc), parts);
            Assert.That(zip, Is.EqualTo("日报.zip"));
            var dbFolder = BackupNaming.MssqlFolder(@"D:\bak", "用心云循备", "日报", "实例A", parts);
            Assert.That(dbFolder, Is.EqualTo(@"D:\bak\库备份"));
        }

        [Test]
        public void Naming_DetectsLoop()
        {
            var bak = Path.Combine(_root, "bak");
            Directory.CreateDirectory(bak);
            Assert.That(BackupNaming.IsLoop(bak, bak), Is.True);
            Assert.That(BackupNaming.IsLoop(Path.Combine(bak, "sub"), bak), Is.True);
            Assert.That(BackupNaming.IsLoop(Path.Combine(_root, "src"), bak), Is.False);
        }

        [Test]
        public void Runner_CreatesZip_FromLocalFolder()
        {
            var src = Path.Combine(_root, "src");
            Directory.CreateDirectory(src);
            File.WriteAllText(Path.Combine(src, "a.txt"), "hello");
            AppPaths.EnsureDirectories();
            using (var db = SqliteDb.OpenDefault())
            {
                var task = new FileTaskDto
                {
                    Name = "测试任务",
                    Enabled = true,
                    SourceKind = "local",
                    SourcePath = src,
                    RetryMinutes = 5,
                    CycleKind = "once",
                    CycleInterval = 1,
                    NextRunUtc = DateTime.UtcNow.AddMinutes(-1)
                };
                task.Id = FileTaskStore.Insert(db, task);
                FileBackupRunner.ExecuteDue(db);
                var zips = Directory.GetFiles(AppPaths.BakDirectory, "*.zip", SearchOption.AllDirectories);
                Assert.That(zips.Length, Is.EqualTo(1));
                var logs = LogStore.List(db, "file", 10);
                Assert.That(logs.Count, Is.EqualTo(1));
                Assert.That(logs[0].Result, Is.EqualTo("ok"));
            }
        }

        [Test]
        public void TryClaim_BlocksSecondRunUntilRelease()
        {
            AppPaths.EnsureDirectories();
            using (var db = SqliteDb.OpenDefault())
            {
                var task = new FileTaskDto
                {
                    Name = "认领",
                    Enabled = true,
                    SourceKind = "local",
                    SourcePath = _root,
                    CycleKind = "day",
                    CycleInterval = 1,
                    NextRunUtc = DateTime.UtcNow.AddMinutes(-1)
                };
                task.Id = FileTaskStore.Insert(db, task);
                var now = DateTime.UtcNow;
                Assert.That(FileTaskStore.TryClaim(db, task.Id, now), Is.True);
                Assert.That(FileTaskStore.TryClaim(db, task.Id, now), Is.False);
                Assert.That(FileTaskStore.GetDue(db, now).Count, Is.EqualTo(0));
                FileTaskStore.MarkResult(db, FileTaskStore.GetById(db, task.Id), true, now);
                Assert.That(FileTaskStore.GetDue(db, now).Count, Is.EqualTo(0));
            }
        }

        [Test]
        public void Delete_RemovesFileAndMssqlTask()
        {
            AppPaths.EnsureDirectories();
            using (var db = SqliteDb.OpenDefault())
            {
                var file = new FileTaskDto
                {
                    Name = "待删文件",
                    Enabled = true,
                    SourceKind = "local",
                    SourcePath = _root,
                    CycleKind = "day",
                    CycleInterval = 1,
                    NextRunUtc = DateTime.UtcNow
                };
                file.Id = FileTaskStore.Insert(db, file);
                FileTaskStore.Delete(db, file.Id);
                Assert.That(FileTaskStore.List(db).Count, Is.EqualTo(0));

                var instanceId = MssqlInstanceStore.Insert(db, new MssqlInstanceDto
                {
                    Name = "本机",
                    Server = "localhost",
                    AuthKind = "windows"
                }, null);
                var mssql = new MssqlTaskDto
                {
                    Name = "待删库",
                    InstanceId = instanceId,
                    Enabled = true,
                    DbScope = "user",
                    CycleKind = "day",
                    CycleInterval = 1,
                    NextRunUtc = DateTime.UtcNow
                };
                mssql.Id = MssqlTaskStore.Insert(db, mssql);
                MssqlTaskStore.Delete(db, mssql.Id);
                Assert.That(MssqlTaskStore.List(db).Count, Is.EqualTo(0));
                Assert.That(MssqlInstanceStore.List(db).Count, Is.EqualTo(1));
            }
        }
    }
}
