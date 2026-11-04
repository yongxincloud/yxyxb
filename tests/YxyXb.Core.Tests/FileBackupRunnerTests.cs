using System;
using System.IO;
using NUnit.Framework;
using YxyXb.Contracts.Dto;
using YxyXb.Core;
using YxyXb.Core.Backup;
using YxyXb.Core.Data;
using YxyXb.Core.Store;

namespace YxyXb.Core.Tests
{
    [NonParallelizable]
    public class FileBackupRunnerTests
    {
        private string _root;
        private string _source;
        private string _bak;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "yxy-run-" + Guid.NewGuid().ToString("N"));
            _source = Path.Combine(_root, "source");
            _bak = Path.Combine(_root, "bak");
            Directory.CreateDirectory(_source);
            Directory.CreateDirectory(_bak);
            AppPaths.SetRootForTests(_root);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }

        [Test]
        public void ExecuteOne_NoSkippedFiles_LogsStatsAndSucceeds()
        {
            File.WriteAllText(Path.Combine(_source, "a.txt"), "123");
            File.WriteAllText(Path.Combine(_source, "b.txt"), "45");

            using (var db = NewDb())
            {
                var task = InsertTask(db);
                FileBackupRunner.ExecuteOne(db, task, "manual");

                var entry = LogStore.LatestForTask(db, "file", task.Name);
                Assert.That(entry.Result, Is.EqualTo("ok"));
                Assert.That(entry.Message, Does.Contain("发现 2，打包 2，跳过 0"));
                Assert.That(entry.Message, Does.Contain("源 5 B"));
            }
        }

        [Test]
        public void ExecuteOne_SkippedFile_FailsButKeepsReadableBackup()
        {
            File.WriteAllText(Path.Combine(_source, "readable.txt"), "1234");
            var locked = Path.Combine(_source, "locked.txt");
            File.WriteAllText(locked, "12");

            using (var db = NewDb())
            using (new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                var task = InsertTask(db);
                FileBackupRunner.ExecuteOne(db, task, "manual");

                var entry = LogStore.LatestForTask(db, "file", task.Name);
                Assert.That(entry.Result, Is.EqualTo("error"));
                Assert.That(entry.Message, Does.Contain("发现 2，打包 1，跳过 1"));
                Assert.That(Directory.GetFiles(_bak, "*.zip", SearchOption.AllDirectories), Has.Length.EqualTo(1));
            }
        }

        [Test]
        public void ExecuteOne_MailFails_DataStillSucceedsWithWarning()
        {
            File.WriteAllText(Path.Combine(_source, "a.txt"), "123");

            using (var db = NewDb())
            {
                SettingsStore.SaveXfer(db, "smtp", new SmtpSettingsDto
                {
                    Host = "127.0.0.1",
                    Port = 1,
                    From = "sender@example.com",
                    To = "receiver@example.com",
                    SendAfterTask = true,
                    SendWhen = "all",
                    Subject = "{结果}",
                    Body = "{文件统计}"
                });
                var task = InsertTask(db);
                task.DoEmail = true;

                FileBackupRunner.ExecuteOne(db, task, "manual");

                var entry = LogStore.LatestForTask(db, "file", task.Name);
                Assert.That(entry.Result, Is.EqualTo("warn"));
                Assert.That(entry.Message, Does.Contain("发现 1，打包 1，跳过 0"));
                Assert.That(entry.Message, Does.Contain("邮件失败"));
            }
        }

        private SqliteDb NewDb()
        {
            var db = new SqliteDb(Path.Combine(_root, "data", "test.db"));
            var settings = SettingsStore.Get(db);
            settings.BakPath = _bak;
            settings.SoftwareId = "testsid";
            SettingsStore.Save(db, settings);
            return db;
        }

        private FileTaskDto InsertTask(SqliteDb db)
        {
            var task = new FileTaskDto
            {
                Name = "统计任务",
                Enabled = true,
                SourceKind = "local",
                SourcePath = _source,
                RetryMinutes = 5,
                SortOrder = 1,
                CycleKind = "day",
                CycleInterval = 1,
                NextRunUtc = DateTime.UtcNow
            };
            task.Id = FileTaskStore.Insert(db, task);
            return task;
        }
    }
}
