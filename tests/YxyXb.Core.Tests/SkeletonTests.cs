using System;
using System.IO;
using NUnit.Framework;
using YxyXb.Contracts.Dto;
using YxyXb.Core;
using YxyXb.Core.Data;
using YxyXb.Core.Device;
using YxyXb.Core.License;
using YxyXb.Core.Store;

namespace YxyXb.Core.Tests
{
    [TestFixture]
    public sealed class SkeletonTests
    {
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "yxy-tests-" + Guid.NewGuid().ToString("N"));
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
        public void EnsureDirectories_CreatesBak()
        {
            AppPaths.EnsureDirectories();
            Assert.That(Directory.Exists(AppPaths.BakDirectory), Is.True);
            Assert.That(AppPaths.BakDirectory, Is.EqualTo(Path.Combine(_root, "bak")));
        }

        [Test]
        public void EnsureDirectories_CreatesBaiduUpload()
        {
            AppPaths.EnsureDirectories();
            Assert.That(Directory.Exists(AppPaths.BaiduUploadDirectory), Is.True);
            Assert.That(AppPaths.BaiduUploadDirectory, Is.EqualTo(Path.Combine(_root, "data", "baidu-upload")));
        }

        [Test]
        public void Sqlite_CreatesSchemaAndKeepsDeviceId()
        {
            AppPaths.EnsureDirectories();
            string first;
            using (var db = SqliteDb.OpenDefault())
            {
                first = DeviceIdStore.GetOrCreate(db).DeviceId;
                Assert.That(first, Is.Not.Null.And.Not.Empty);
                var counts = TaskScheduleStore.GetHomeCounts(db);
                Assert.That(counts.FileTaskCount, Is.EqualTo(0));
            }

            using (var db = SqliteDb.OpenDefault())
            {
                var again = DeviceIdStore.GetOrCreate(db).DeviceId;
                Assert.That(again, Is.EqualTo(first));
            }
        }

        [Test]
        public void GetUpcoming_AllowsUnionOrder()
        {
            using (var db = SqliteDb.OpenDefault())
            {
                MediaTaskStore.Insert(db, new MediaTaskDto
                {
                    Name = "t",
                    MediaId = 1,
                    Enabled = true,
                    WindowStart = "22:00",
                    WindowEnd = "23:00",
                    WaitMinutes = 30,
                    RetainVersions = 7,
                    RetainDays = 7,
                    CycleKind = "day",
                    CycleInterval = 1,
                    NextRunUtc = DateTime.UtcNow.AddMinutes(5)
                });
                var list = TaskScheduleStore.GetUpcoming(db, 20);
                Assert.That(list.Count, Is.EqualTo(1));
                Assert.That(list[0].Kind, Is.EqualTo("media"));
                MediaTaskStore.Delete(db, list[0].Id);
                Assert.That(MediaTaskStore.List(db).Count, Is.EqualTo(0));
            }
        }

        [Test]
        public void MediaUpdate_ClearsWaitAndKeepsEditedWindow()
        {
            using (var db = SqliteDb.OpenDefault())
            {
                var nextLocal = DateTime.Today.AddDays(1).AddHours(22).AddMinutes(10);
                var id = MediaTaskStore.Insert(db, new MediaTaskDto
                {
                    Name = "media",
                    MediaId = 1,
                    Enabled = true,
                    SourcePath = AppPaths.BakDirectory,
                    WindowStart = "22:00",
                    WindowEnd = "23:00",
                    WaitMinutes = 30,
                    RetainVersions = 7,
                    RetainDays = 7,
                    CycleKind = "day",
                    CycleInterval = 1,
                    NextRunUtc = nextLocal.ToUniversalTime()
                });
                Directory.CreateDirectory(AppPaths.BakDirectory);
                MediaTaskStore.SetWaiting(db, id, DateTime.UtcNow);
                var edited = MediaTaskStore.GetById(db, id);
                edited.WindowStart = "21:00";
                edited.WindowEnd = "21:30";
                edited.NextRunUtc = nextLocal.ToUniversalTime();
                MediaTaskStore.Update(db, edited);
                var saved = MediaTaskStore.GetById(db, id);
                Assert.That(saved.WaitingSinceUtc.HasValue, Is.False);
                Assert.That(saved.NextRunUtc.HasValue, Is.True);
                var local = saved.NextRunUtc.Value.ToLocalTime();
                Assert.That(local.Date, Is.EqualTo(nextLocal.Date));
                Assert.That(local.Hour, Is.EqualTo(22));
                Assert.That(local.Minute, Is.EqualTo(10));
            }
        }

        [Test]
        public void TrialClock_ShowsRemainingDays()
        {
            using (var db = SqliteDb.OpenDefault())
            {
                var trial = TrialClock.Get(db);
                Assert.That(trial.RemainingDays, Is.GreaterThan(0).And.LessThanOrEqualTo(TrialClock.TrialDays));
                Assert.That(trial.IsExpired, Is.False);
            }
        }
    }
}
