using System;
using System.IO;
using NUnit.Framework;
using YxyXb.Contracts.Dto;
using YxyXb.Contracts.Time;
using YxyXb.Core.Data;
using YxyXb.Core.Schedule;
using YxyXb.Core.Store;

namespace YxyXb.BackupLifecycle.Tests
{
    [NonParallelizable]
    public class ScheduleAnchorTests
    {
        private string _databasePath;

        [SetUp]
        public void SetUp()
        {
            _databasePath = Path.Combine(Path.GetTempPath(), "yxy-schedule-" + Guid.NewGuid().ToString("N"), "test.db");
        }

        [TearDown]
        public void TearDown()
        {
            var directory = Path.GetDirectoryName(_databasePath);
            if (directory != null && Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }

        [Test]
        public void NextPlannedAfter_CatchUp_ReturnsNextWallClockOccurrence()
        {
            var anchor = new DateTime(2026, 11, 9, 9, 0, 0, DateTimeKind.Utc);
            var catchUpAt = new DateTime(2026, 11, 10, 1, 2, 0, DateTimeKind.Utc);

            var next = ScheduleCalculator.NextPlannedAfter("day", 1, anchor, catchUpAt);

            Assert.That(next, Is.EqualTo(new DateTime(2026, 11, 10, 9, 0, 0, DateTimeKind.Utc)));
        }

        [Test]
        public void NextPlannedAfter_LongRun_SkipsCrossedOccurrences()
        {
            var anchor = new DateTime(2026, 11, 9, 9, 0, 0, DateTimeKind.Utc);
            var finishedAt = new DateTime(2026, 11, 11, 10, 0, 0, DateTimeKind.Utc);

            var next = ScheduleCalculator.NextPlannedAfter("day", 1, anchor, finishedAt);

            Assert.That(next, Is.EqualTo(new DateTime(2026, 11, 12, 9, 0, 0, DateTimeKind.Utc)));
        }

        [Test]
        public void NextPlannedAfter_ManualRunBeforePlan_PreservesPlan()
        {
            var anchor = new DateTime(2026, 11, 10, 9, 0, 0, DateTimeKind.Utc);
            var manualRunAt = new DateTime(2026, 11, 10, 1, 0, 0, DateTimeKind.Utc);

            var next = ScheduleCalculator.NextPlannedAfter("day", 1, anchor, manualRunAt);

            Assert.That(next, Is.EqualTo(anchor));
        }

        [Test]
        public void NextPlannedAfter_OnceBeforePlan_PreservesFuturePlan()
        {
            var anchor = new DateTime(2026, 11, 10, 9, 0, 0, DateTimeKind.Utc);
            var manualRunAt = new DateTime(2026, 11, 10, 1, 0, 0, DateTimeKind.Utc);

            var next = ScheduleCalculator.NextPlannedAfter("once", 1, anchor, manualRunAt);

            Assert.That(next, Is.EqualTo(anchor));
        }

        [Test]
        public void NextPlannedAfter_OnceAfterPlan_ConsumesPlan()
        {
            var anchor = new DateTime(2026, 11, 10, 9, 0, 0, DateTimeKind.Utc);

            var next = ScheduleCalculator.NextPlannedAfter(
                "once",
                1,
                anchor,
                new DateTime(2026, 11, 10, 9, 5, 0, DateTimeKind.Utc));

            Assert.That(next, Is.Null);
        }

        [Test]
        public void NextPlannedAfter_Monthly_UsesOriginalDayWithoutDriftingOnShortMonths()
        {
            var anchor = new DateTime(2026, 1, 31, 9, 0, 0, DateTimeKind.Utc);

            var next = ScheduleCalculator.NextPlannedAfter(
                "month",
                1,
                anchor,
                new DateTime(2026, 2, 28, 9, 0, 0, DateTimeKind.Utc));

            Assert.That(next, Is.EqualTo(new DateTime(2026, 3, 31, 9, 0, 0, DateTimeKind.Utc)));
        }

        [Test]
        public void NextPlannedAfter_MonthlyNearMidnight_UsesShanghaiCalendarDate()
        {
            var anchor = ChinaTime.ToUtc(new DateTime(2026, 1, 31, 0, 30, 0));
            var after = ChinaTime.ToUtc(new DateTime(2026, 2, 1, 0, 0, 0));
            var expected = ChinaTime.ToUtc(new DateTime(2026, 2, 28, 0, 30, 0));

            var next = ScheduleCalculator.NextPlannedAfter("month", 1, anchor, after);

            Assert.That(next, Is.EqualTo(expected));
        }

        [Test]
        public void ChinaTime_UsesShanghaiWallClock()
        {
            var utc = new DateTime(2026, 11, 10, 9, 0, 0, DateTimeKind.Utc);

            Assert.That(ChinaTime.ToChina(utc), Is.EqualTo(new DateTime(2026, 11, 10, 17, 0, 0)));
            Assert.That(
                ChinaTime.ToUtc(new DateTime(2026, 11, 10, 17, 0, 0)),
                Is.EqualTo(utc));
        }

        [Test]
        public void FileTask_CatchUp_PreservesAnchorAndRestoresPlannedTime()
        {
            using (var db = new SqliteDb(_databasePath))
            {
                var anchor = new DateTime(2026, 11, 9, 9, 0, 0, DateTimeKind.Utc);
                var task = FileTaskStore.GetById(db, InsertFileTask(db, anchor));

                FileTaskStore.MarkResult(db, task, true, new DateTime(2026, 11, 10, 1, 2, 0, DateTimeKind.Utc));

                var saved = FileTaskStore.GetById(db, task.Id);
                Assert.That(saved.ScheduleAnchorUtc, Is.EqualTo(anchor));
                Assert.That(saved.NextRunUtc, Is.EqualTo(new DateTime(2026, 11, 10, 9, 0, 0, DateTimeKind.Utc)));
            }
        }

        [Test]
        public void FileTask_FailedRetry_PreservesAnchor()
        {
            using (var db = new SqliteDb(_databasePath))
            {
                var anchor = new DateTime(2026, 11, 10, 9, 0, 0, DateTimeKind.Utc);
                var failedAt = new DateTime(2026, 11, 10, 9, 5, 0, DateTimeKind.Utc);
                var task = FileTaskStore.GetById(db, InsertFileTask(db, anchor));

                FileTaskStore.MarkResult(db, task, false, failedAt);

                var saved = FileTaskStore.GetById(db, task.Id);
                Assert.That(saved.ScheduleAnchorUtc, Is.EqualTo(anchor));
                Assert.That(saved.NextRunUtc, Is.EqualTo(failedAt.AddMinutes(5)));
            }
        }

        [Test]
        public void TaskStores_KeepScheduleAnchorForEveryBackupKind()
        {
            using (var db = new SqliteDb(_databasePath))
            {
                var anchor = new DateTime(2026, 11, 10, 9, 0, 0, DateTimeKind.Utc);
                var file = FileTaskStore.GetById(db, InsertFileTask(db, anchor));
                var mssql = MssqlTaskStore.GetById(db, MssqlTaskStore.Insert(db, new MssqlTaskDto
                {
                    Name = "MSSQL",
                    InstanceId = 1,
                    Enabled = true,
                    DbScope = "user",
                    RetryMinutes = 5,
                    CycleKind = "day",
                    CycleInterval = 1,
                    ScheduleAnchorUtc = anchor,
                    NextRunUtc = anchor
                }));
                var mysql = MysqlTaskStore.GetById(db, MysqlTaskStore.Insert(db, new MysqlTaskDto
                {
                    Name = "MySQL",
                    InstanceId = 1,
                    Enabled = true,
                    DbScope = "user",
                    RetryMinutes = 5,
                    CycleKind = "day",
                    CycleInterval = 1,
                    ScheduleAnchorUtc = anchor,
                    NextRunUtc = anchor
                }));
                var media = MediaTaskStore.GetById(db, MediaTaskStore.Insert(db, new MediaTaskDto
                {
                    Name = "Media",
                    MediaId = 1,
                    Enabled = true,
                    SourcePath = Path.GetDirectoryName(_databasePath),
                    TargetFolder = "test",
                    WindowStart = "22:00",
                    WindowEnd = "23:00",
                    WaitMinutes = 30,
                    RetainVersions = 7,
                    RetainDays = 7,
                    ConflictMode = "overwrite",
                    IsolateLevel = 2,
                    CycleKind = "day",
                    CycleInterval = 1,
                    ScheduleAnchorUtc = anchor,
                    NextRunUtc = anchor
                }));

                Assert.That(file.ScheduleAnchorUtc, Is.EqualTo(anchor));
                Assert.That(mssql.ScheduleAnchorUtc, Is.EqualTo(anchor));
                Assert.That(mysql.ScheduleAnchorUtc, Is.EqualTo(anchor));
                Assert.That(media.ScheduleAnchorUtc, Is.EqualTo(anchor));
            }
        }

        [Test]
        public void TaskStores_RestorePlannedTimeAfterCatchUpForEveryBackupKind()
        {
            using (var db = new SqliteDb(_databasePath))
            {
                var anchor = new DateTime(2026, 11, 9, 9, 0, 0, DateTimeKind.Utc);
                var catchUpAt = new DateTime(2026, 11, 10, 1, 2, 0, DateTimeKind.Utc);
                var expected = new DateTime(2026, 11, 10, 9, 0, 0, DateTimeKind.Utc);

                var file = FileTaskStore.GetById(db, InsertFileTask(db, anchor));
                var mssql = MssqlTaskStore.GetById(db, MssqlTaskStore.Insert(db, NewMssql(anchor)));
                var mysql = MysqlTaskStore.GetById(db, MysqlTaskStore.Insert(db, NewMysql(anchor)));
                var media = MediaTaskStore.GetById(db, MediaTaskStore.Insert(db, NewMedia(anchor, Path.GetDirectoryName(_databasePath))));

                FileTaskStore.MarkResult(db, file, true, catchUpAt);
                MssqlTaskStore.MarkResult(db, mssql, true, catchUpAt);
                MysqlTaskStore.MarkResult(db, mysql, true, catchUpAt);
                MediaTaskStore.MarkResult(db, media, true, catchUpAt);

                Assert.That(FileTaskStore.GetById(db, file.Id).NextRunUtc, Is.EqualTo(expected));
                Assert.That(MssqlTaskStore.GetById(db, mssql.Id).NextRunUtc, Is.EqualTo(expected));
                Assert.That(MysqlTaskStore.GetById(db, mysql.Id).NextRunUtc, Is.EqualTo(expected));
                Assert.That(MediaTaskStore.GetById(db, media.Id).NextRunUtc, Is.EqualTo(expected));
            }
        }

        [Test]
        public void DatabaseOpening_BackfillsMissingScheduleAnchorFromLegacyNextRun()
        {
            var anchor = new DateTime(2026, 11, 10, 9, 0, 0, DateTimeKind.Utc);
            long taskId;
            using (var db = new SqliteDb(_databasePath))
            {
                taskId = InsertFileTask(db, anchor);
                db.Execute("UPDATE file_tasks SET schedule_anchor_utc=NULL WHERE id=@id",
                    new System.Data.SQLite.SQLiteParameter("@id", taskId));
            }

            using (var db = new SqliteDb(_databasePath))
            {
                Assert.That(FileTaskStore.GetById(db, taskId).ScheduleAnchorUtc, Is.EqualTo(anchor));
            }
        }

        private static long InsertFileTask(SqliteDb db, DateTime anchor)
        {
            return FileTaskStore.Insert(db, new FileTaskDto
            {
                Name = "File",
                Enabled = true,
                SourceKind = "local",
                SourcePath = Path.GetTempPath(),
                RetryMinutes = 5,
                CycleKind = "day",
                CycleInterval = 1,
                ScheduleAnchorUtc = anchor,
                NextRunUtc = anchor
            });
        }

        private static MssqlTaskDto NewMssql(DateTime anchor)
        {
            return new MssqlTaskDto
            {
                Name = "MSSQL",
                InstanceId = 1,
                Enabled = true,
                DbScope = "user",
                RetryMinutes = 5,
                CycleKind = "day",
                CycleInterval = 1,
                ScheduleAnchorUtc = anchor,
                NextRunUtc = anchor
            };
        }

        private static MysqlTaskDto NewMysql(DateTime anchor)
        {
            return new MysqlTaskDto
            {
                Name = "MySQL",
                InstanceId = 1,
                Enabled = true,
                DbScope = "user",
                RetryMinutes = 5,
                CycleKind = "day",
                CycleInterval = 1,
                ScheduleAnchorUtc = anchor,
                NextRunUtc = anchor
            };
        }

        private static MediaTaskDto NewMedia(DateTime anchor, string sourcePath)
        {
            return new MediaTaskDto
            {
                Name = "Media",
                MediaId = 1,
                Enabled = true,
                SourcePath = sourcePath,
                TargetFolder = "test",
                WindowStart = "22:00",
                WindowEnd = "23:00",
                WaitMinutes = 30,
                RetainVersions = 7,
                RetainDays = 7,
                ConflictMode = "overwrite",
                IsolateLevel = 2,
                CycleKind = "day",
                CycleInterval = 1,
                ScheduleAnchorUtc = anchor,
                NextRunUtc = anchor
            };
        }
    }
}
