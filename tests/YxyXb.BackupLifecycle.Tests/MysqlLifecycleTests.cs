using System;
using System.Text;
using NUnit.Framework;
using YxyXb.Core.Backup;

namespace YxyXb.BackupLifecycle.Tests
{
    public class MysqlLifecycleTests
    {
        [Test]
        public void MysqlTask_UsesLifecycleAndClearsPendingAfterSuccess()
        {
            var task = new FakeTask();
            var result = BackupRunLifecycle.Execute(new BackupRunRequest
            {
                Task = task,
                Source = new TwoPackageSource(),
                Delivery = new SuccessfulDelivery(),
                Notification = new NoNotification(),
                PackageStore = new BackupPackageStore(),
                DoNetwork = true,
                Mode = "manual"
            });

            Assert.That(result.Ok, Is.True);
            Assert.That(task.PendingJson, Is.Null);
            Assert.That(task.ResultCode, Is.EqualTo("ok"));
        }

        private sealed class FakeTask : IBackupRunTask
        {
            public string PendingJson { get; private set; }
            public string ResultCode { get; private set; }

            public string LogType
            {
                get { return "mysql"; }
            }

            public string TaskName
            {
                get { return "mysql-lifecycle"; }
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
                ResultCode = result;
            }
        }

        private sealed class TwoPackageSource : IBackupSourceAdapter
        {
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
            }
        }

        private sealed class SuccessfulDelivery : IBackupDelivery
        {
            public bool Upload(PendingItem item, bool doNetwork, bool doFtp, bool doCloud, string provider, StringBuilder log, Action persist)
            {
                if (persist != null)
                {
                    persist();
                }

                return true;
            }
        }

        private sealed class NoNotification : IBackupNotification
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
