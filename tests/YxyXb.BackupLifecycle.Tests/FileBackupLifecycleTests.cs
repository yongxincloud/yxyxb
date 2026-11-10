using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using YxyXb.Contracts.Dto;
using YxyXb.Core;
using YxyXb.Core.Backup;
using YxyXb.Core.Data;
using YxyXb.Core.Store;

namespace YxyXb.BackupLifecycle.Tests
{
    [NonParallelizable]
    public class FileBackupLifecycleTests
    {
        private string _root;
        private string _source;
        private string _backup;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "yxy-lifecycle-" + Guid.NewGuid().ToString("N"));
            _source = Path.Combine(_root, "source");
            _backup = Path.Combine(_root, "backup");
            Directory.CreateDirectory(_source);
            Directory.CreateDirectory(_backup);
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
        public void FileBackup_RetriesOnlyTheIncompletePackage_AfterFailure()
        {
            var sourceFile = Path.Combine(_source, "data.txt");
            File.WriteAllText(sourceFile, "content");
            using (var db = NewDb())
            {
                var task = NewTask(db, "retry", 0);
                var delivery = new FakeDelivery { Succeed = false };
                var first = Run(db, task, delivery, new FakeNotification());

                Assert.That(first.DataOk, Is.False);
                Assert.That(task.PendingJson, Is.Not.Null.And.Not.Empty);
                Assert.That(delivery.Calls, Is.EqualTo(1));
                Assert.That(Directory.GetFiles(_backup, "*.zip", SearchOption.AllDirectories), Has.Length.EqualTo(1));
                var packagePath = Directory.GetFiles(_backup, "*.zip", SearchOption.AllDirectories)[0];

                delivery.Succeed = true;
                var second = Run(db, task, delivery, new FakeNotification());

                Assert.That(second.Ok, Is.True);
                Assert.That(second.DataOk, Is.True);
                Assert.That(delivery.Calls, Is.EqualTo(2));
                Assert.That(task.PendingJson, Is.Null);
                Assert.That(Directory.GetFiles(_backup, "*.zip", SearchOption.AllDirectories), Has.Length.EqualTo(1));
                Assert.That(Directory.GetFiles(_backup, "*.zip", SearchOption.AllDirectories)[0], Is.EqualTo(packagePath));
            }
        }

        [Test]
        public void FileBackup_MailFailure_IsWarningButDataSucceeds()
        {
            File.WriteAllText(Path.Combine(_source, "data.txt"), "content");
            using (var db = NewDb())
            {
                var task = NewTask(db, "mail", 0);
                var result = Run(db, task, new FakeDelivery { Succeed = true }, new FakeNotification
                {
                    Enabled = true,
                    SendError = new InvalidOperationException("smtp unavailable")
                });

                Assert.That(result.Ok, Is.True);
                Assert.That(result.DataOk, Is.True);
                Assert.That(result.MailOk, Is.False);
                Assert.That(result.ResultCode, Is.EqualTo("warn"));
                Assert.That(LogStore.LatestForTask(db, "file", task.Name).Result, Is.EqualTo("warn"));
            }
        }

        [Test]
        public void FileBackup_SplitPackage_IsReusedWithoutRepacking()
        {
            var sourceFile = Path.Combine(_source, "large.bin");
            using (var output = File.Create(sourceFile))
            {
                output.SetLength(PackageLimits.MinimumBytes + 1024 * 1024);
            }

            using (var db = NewDb())
            {
                var task = NewTask(db, "split", PackageLimits.MinimumBytes);
                var delivery = new FakeDelivery { Succeed = false };
                var first = Run(db, task, delivery, new FakeNotification());
                Assert.That(first.DataOk, Is.False);

                var pending = PendingBackup.Parse(task.PendingJson);
                Assert.That(pending, Is.Not.Null);
                Assert.That(pending.Items[0].UploadFiles, Is.Not.Null);
                Assert.That(pending.Items[0].UploadFiles.Length, Is.GreaterThan(1));
                var partCount = Directory.GetFiles(_backup, "*.part*.zip", SearchOption.AllDirectories).Length;
                Assert.That(partCount, Is.GreaterThanOrEqualTo(2));

                delivery.Succeed = true;
                var second = Run(db, task, delivery, new FakeNotification());

                Assert.That(second.DataOk, Is.True);
                Assert.That(Directory.GetFiles(_backup, "*.part*.zip", SearchOption.AllDirectories), Has.Length.EqualTo(partCount));
            }
        }

        [Test]
        public void FileBackup_StrictFailure_WhenSourceReportsSkippedFiles()
        {
            using (var db = NewDb())
            {
                var task = NewTask(db, "skip", 0);
                var source = new FakeSource
                {
                    Stats = new BackupRunStats { DiscoveredFiles = 2, PackedFiles = 1, SkippedFiles = 1 }
                };
                var result = BackupRunLifecycle.Execute(new BackupRunRequest
                {
                    Task = new FileBackupTaskState(db, task),
                    Source = source,
                    Delivery = new FakeDelivery { Succeed = true },
                    Notification = new FakeNotification(),
                    PackageStore = new BackupPackageStore(),
                    DoNetwork = false,
                    DoFtp = false,
                    DoCloud = false,
                    Mode = "manual"
                });

                Assert.That(result.DataOk, Is.False);
                Assert.That(source.Disposed, Is.True);
            }
        }

        [Test]
        public void FileBackupRunner_UsesLifecycleAndWritesSuccessLog()
        {
            File.WriteAllText(Path.Combine(_source, "data.txt"), "content");
            using (var db = NewDb())
            {
                var task = NewTask(db, "runner", 0, false);

                FileBackupRunner.ExecuteOne(db, task, "manual");

                var entry = LogStore.LatestForTask(db, "file", task.Name);
                Assert.That(entry.Result, Is.EqualTo("ok"));
                Assert.That(Directory.GetFiles(_backup, "*.zip", SearchOption.AllDirectories), Has.Length.EqualTo(1));
                Assert.That(task.PendingJson, Is.Null);
            }
        }

        private SqliteDb NewDb()
        {
            var db = new SqliteDb(Path.Combine(_root, "data", "test.db"));
            var settings = SettingsStore.Get(db);
            settings.BakPath = _backup;
            settings.SoftwareId = "testsid";
            settings.ZipLevel = 0;
            SettingsStore.Save(db, settings);
            return db;
        }

        private FileTaskDto NewTask(SqliteDb db, string name, long partSizeBytes, bool doNetwork = true)
        {
            var task = new FileTaskDto
            {
                Name = name,
                Enabled = true,
                SourceKind = "local",
                SourcePath = _source,
                RetryMinutes = 5,
                CycleKind = "day",
                CycleInterval = 1,
                NextRunUtc = DateTime.UtcNow,
                PartSizeBytes = partSizeBytes,
                DoNetwork = doNetwork
            };
            task.Id = FileTaskStore.Insert(db, task);
            return task;
        }

        private BackupRunResult Run(SqliteDb db, FileTaskDto task, FakeDelivery delivery, FakeNotification notification)
        {
            return BackupRunLifecycle.Execute(new BackupRunRequest
            {
                Task = new FileBackupTaskState(db, task),
                Source = new FileBackupSource(db, task),
                Delivery = delivery,
                Notification = notification,
                PackageStore = new BackupPackageStore(),
                DoNetwork = task.DoNetwork,
                DoFtp = task.DoFtp,
                DoCloud = task.DoCloud,
                CloudProvider = task.CloudProvider,
                DoDeleteNow = task.DoDeleteNow,
                Mode = "manual"
            });
        }

        private sealed class FakeSource : IBackupSourceAdapter
        {
            public BackupRunStats Stats { get; set; }
            public bool Disposed { get; private set; }

            public BackupSourceInspection Inspect(PendingRecord pending, DateTime utcNow, StringBuilder log)
            {
                return new BackupSourceInspection
                {
                    Decision = new PendingDecision
                    {
                        Reuse = false,
                        Replace = new PendingItem[0],
                        Keep = new PendingItem[0],
                        Reason = "fake"
                    },
                    Stats = Stats,
                    TrackPending = false
                };
            }

            public BackupPackageSet Prepare(PendingRecord pending, PendingDecision decision, DateTime utcNow, StringBuilder log)
            {
                var item = new PendingItem { Key = "fake", ArchiveBytes = 1 };
                return new BackupPackageSet
                {
                    Record = new PendingRecord { Items = new[] { item } },
                    Packages = new[] { item },
                    ArchiveBytes = 1
                };
            }

            public void Dispose()
            {
                Disposed = true;
            }
        }

        private sealed class FakeDelivery : IBackupDelivery
        {
            public bool Succeed { get; set; }
            public int Calls { get; private set; }

            public bool Upload(
                PendingItem item,
                bool doNetwork,
                bool doFtp,
                bool doCloud,
                string provider,
                StringBuilder log,
                Action persist)
            {
                Calls++;
                if (!Succeed)
                {
                    return false;
                }

                if (item.UploadFiles == null)
                {
                    item.UploadFiles = new[] { new PendingUploadFile { LocalPath = item.ZipPath } };
                }

                for (var i = 0; i < item.UploadFiles.Length; i++)
                {
                    item.UploadFiles[i].NetworkOk = true;
                }

                if (persist != null)
                {
                    persist();
                }

                return true;
            }
        }

        private sealed class FakeNotification : IBackupNotification
        {
            public bool Enabled { get; set; }
            public Exception SendError { get; set; }

            public bool ShouldSend(bool dataOk)
            {
                return Enabled;
            }

            public void Send(bool dataOk, BackupRunStats stats, long archiveBytes, bool uploadEnabled, bool uploadOk, int packageCount, string detail)
            {
                if (SendError != null)
                {
                    throw SendError;
                }
            }
        }
    }
}
