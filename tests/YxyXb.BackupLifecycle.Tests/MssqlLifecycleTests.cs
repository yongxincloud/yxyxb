using System;
using System.Text;
using NUnit.Framework;
using YxyXb.Core.Backup;

namespace YxyXb.BackupLifecycle.Tests
{
    [NonParallelizable]
    public class MssqlLifecycleTests
    {
        [Test]
        public void MultiplePackages_SucceedThroughOneLifecycle()
        {
            var task = new FakeTask();
            var source = new TwoPackageSource();
            var delivery = new FakeDelivery { SucceedAt = 99 };

            var result = BackupRunLifecycle.Execute(Request(task, source, delivery));

            Assert.That(result.Ok, Is.True);
            Assert.That(result.DataOk, Is.True);
            Assert.That(delivery.Calls, Is.EqualTo(2));
            Assert.That(task.PendingJson, Is.Null);
            Assert.That(source.Disposed, Is.True);
        }

        [Test]
        public void PartialUploadFailure_KeepsPendingRecordForRetry()
        {
            var task = new FakeTask();
            var source = new TwoPackageSource();
            var delivery = new FakeDelivery { SucceedAt = 1 };

            var result = BackupRunLifecycle.Execute(Request(task, source, delivery));

            Assert.That(result.DataOk, Is.False);
            Assert.That(delivery.Calls, Is.EqualTo(2));
            Assert.That(task.PendingJson, Is.Not.Null.And.Not.Empty);
            Assert.That(task.PendingJson, Does.Contain("db1"));
            Assert.That(task.PendingJson, Does.Contain("db2"));
        }

        private static BackupRunRequest Request(FakeTask task, IBackupSourceAdapter source, FakeDelivery delivery)
        {
            return new BackupRunRequest
            {
                Task = task,
                Source = source,
                Delivery = delivery,
                Notification = new FakeNotification(),
                PackageStore = new BackupPackageStore(),
                DoNetwork = true,
                Mode = "manual"
            };
        }

        private sealed class FakeTask : IBackupRunTask
        {
            public string PendingJson { get; private set; }

            public string LogType
            {
                get { return "mssql"; }
            }

            public string TaskName
            {
                get { return "two-databases"; }
            }

            public string LoadPendingJson()
            {
                return PendingJson;
            }

            public void SavePendingJson(string json)
            {
                PendingJson = json;
            }

            public void MarkResult(bool ok, DateTime utcNow)
            {
            }

            public void WriteLog(string mode, string result, string message)
            {
            }
        }

        private sealed class TwoPackageSource : IBackupSourceAdapter
        {
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
                    Stats = new BackupRunStats { DiscoveredFiles = 2 },
                    TrackPending = true
                };
            }

            public BackupPackageSet Prepare(PendingRecord pending, PendingDecision decision, DateTime utcNow, StringBuilder log)
            {
                var first = new PendingItem { Key = "db1", ArchiveBytes = 10 };
                var second = new PendingItem { Key = "db2", ArchiveBytes = 20 };
                return new BackupPackageSet
                {
                    Record = new PendingRecord { Items = new[] { first, second } },
                    Packages = new[] { first, second },
                    ArchiveBytes = 30
                };
            }

            public void Dispose()
            {
                Disposed = true;
            }
        }

        private sealed class FakeDelivery : IBackupDelivery
        {
            public int SucceedAt { get; set; }
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
                if (Calls > SucceedAt)
                {
                    return false;
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
            public bool ShouldSend(bool dataOk)
            {
                return false;
            }

            public void Send(bool dataOk, BackupRunStats stats, long archiveBytes, bool uploadEnabled, bool uploadOk, int packageCount, string detail)
            {
            }
        }
    }
}
