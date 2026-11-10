using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using YxyXb.Core.Backup;

namespace YxyXb.BackupLifecycle.Tests
{
    [NonParallelizable]
    public class LegacyPendingTests
    {
        [Test]
        public void LegacySingleZip_IsReusedWithoutRepacking()
        {
            var task = new FakeTask(
                "{\"SourceKind\":\"local\",\"SourcePath\":\"D:\\\\source\",\"Items\":["
                + "{\"Key\":\"file\",\"ZipPath\":\"old.zip\",\"ArchiveBytes\":10,"
                + "\"UploadFiles\":[{\"LocalPath\":\"old.zip\",\"NetworkOk\":true}]}]}");
            var delivery = new SuccessfulDelivery();

            var result = BackupRunLifecycle.Execute(Request(task, new LegacyReuseSource(), delivery));

            Assert.That(result.Ok, Is.True);
            Assert.That(delivery.Calls, Is.EqualTo(1));
            Assert.That(task.PendingJson, Is.Null);
        }

        [Test]
        public void HistoricalVolumePendingRecord_IsReusedWithoutRepacking()
        {
            var task = new FakeTask(
                "{\"SourceKind\":\"local\",\"SourcePath\":\"D:\\\\source\",\"Items\":["
                + "{\"Key\":\"file\",\"PackageDirectory\":\"old.split-id\",\"ArchiveBytes\":30,"
                + "\"UploadFiles\":["
                + "{\"LocalPath\":\"old.split-id\\\\a.part01.zip\"},"
                + "{\"LocalPath\":\"old.split-id\\\\a.part02.zip\"},"
                + "{\"LocalPath\":\"old.split-id\\\\a.manifest.json\"}]}]}");
            var delivery = new SuccessfulDelivery();

            var result = BackupRunLifecycle.Execute(Request(task, new LegacyReuseSource(), delivery));

            Assert.That(result.Ok, Is.True);
            Assert.That(delivery.Calls, Is.EqualTo(1));
            Assert.That(task.PendingJson, Is.Null);
        }

        [Test]
        public void DeleteFailure_DoesNotReportSuccessAndKeepsPendingEvidence()
        {
            var task = new FakeTask();
            var source = new OnePackageSource();
            var request = Request(task, source, new SuccessfulDelivery());
            request.DoDeleteNow = true;
            request.PackageStore = new FailingPackageStore();

            var result = BackupRunLifecycle.Execute(request);

            Assert.That(result.DataOk, Is.False);
            Assert.That(task.PendingJson, Is.Not.Null.And.Not.Empty);
        }

        private static BackupRunRequest Request(FakeTask task, IBackupSourceAdapter source, SuccessfulDelivery delivery)
        {
            return new BackupRunRequest
            {
                Task = task,
                Source = source,
                Delivery = delivery,
                Notification = new NoNotification(),
                PackageStore = new BackupPackageStore(),
                DoNetwork = true,
                Mode = "manual"
            };
        }

        private sealed class FakeTask : IBackupRunTask
        {
            public FakeTask(string pendingJson = null)
            {
                PendingJson = pendingJson;
            }

            public string PendingJson { get; private set; }

            public string LogType
            {
                get { return "file"; }
            }

            public string TaskName
            {
                get { return "legacy"; }
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

        private sealed class LegacyReuseSource : IBackupSourceAdapter
        {
            public BackupSourceInspection Inspect(PendingRecord pending, DateTime utcNow, StringBuilder log)
            {
                if (pending == null || pending.Items == null || pending.Items.Length == 0)
                {
                    throw new AssertionException("legacy pending record was not parsed");
                }

                return new BackupSourceInspection
                {
                    Decision = new PendingDecision
                    {
                        Reuse = true,
                        Replace = new PendingItem[0],
                        Keep = pending.Items,
                        Reason = "legacy"
                    },
                    Stats = new BackupRunStats { DiscoveredFiles = 1 },
                    TrackPending = true
                };
            }

            public BackupPackageSet Prepare(PendingRecord pending, PendingDecision decision, DateTime utcNow, StringBuilder log)
            {
                throw new AssertionException("legacy pending package was repacked");
            }

            public void Dispose()
            {
            }
        }

        private sealed class OnePackageSource : IBackupSourceAdapter
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
                        Reason = "new"
                    },
                    Stats = new BackupRunStats { DiscoveredFiles = 1 },
                    TrackPending = true
                };
            }

            public BackupPackageSet Prepare(PendingRecord pending, PendingDecision decision, DateTime utcNow, StringBuilder log)
            {
                var item = new PendingItem { Key = "file", ArchiveBytes = 10 };
                return new BackupPackageSet
                {
                    Record = new PendingRecord { Items = new[] { item } },
                    Packages = new[] { item },
                    ArchiveBytes = 10
                };
            }

            public void Dispose()
            {
            }
        }

        private sealed class SuccessfulDelivery : IBackupDelivery
        {
            public int Calls { get; private set; }

            public bool Upload(PendingItem item, bool doNetwork, bool doFtp, bool doCloud, string provider, StringBuilder log, Action persist)
            {
                Calls++;
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

        private sealed class FailingPackageStore : IBackupPackageStore
        {
            public bool DeleteReplacements(PendingItem[] items, StringBuilder log)
            {
                return true;
            }

            public bool DeletePackage(PendingItem item)
            {
                return false;
            }
        }
    }
}
