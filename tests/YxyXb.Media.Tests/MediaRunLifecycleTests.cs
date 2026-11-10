using System;
using System.Collections.Generic;
using NUnit.Framework;
using YxyXb.Contracts.Dto;
using YxyXb.Media.Sync;

namespace YxyXb.Media.Tests
{
    public class MediaRunLifecycleTests
    {
        [Test]
        public void PackageRun_CopiesRemovesLocalThenWritesManifestAndPurges()
        {
            var runtime = new FakePackageRuntime();
            var result = Run(runtime, false);

            Assert.That(result.Copied, Is.EqualTo(1));
            Assert.That(result.Skipped, Is.Zero);
            Assert.That(result.LocalRemoved, Is.True);
            Assert.That(result.DeletedVersions, Is.EqualTo(2));
            Assert.That(runtime.Calls, Is.EqualTo(new[]
            {
                "tamper",
                "pack",
                "copy",
                "remove",
                "write",
                "purge"
            }));
        }

        [Test]
        public void PackageRun_SkipsMatchingDestinationButStillRemovesLocalCopy()
        {
            var runtime = new FakePackageRuntime { DestinationExists = true };
            var result = Run(runtime, false);

            Assert.That(result.Skipped, Is.EqualTo(1));
            Assert.That(result.Copied, Is.Zero);
            Assert.That(result.LocalRemoved, Is.True);
            Assert.That(runtime.Calls, Does.Not.Contain("copy"));
        }

        [Test]
        public void PackageRun_KeepsLocalZipWhenCopiedSizeDiffers()
        {
            var runtime = new FakePackageRuntime { DestinationSizeAfterCopy = 11 };

            Assert.Throws<InvalidOperationException>(delegate
            {
                Run(runtime, false);
            });

            Assert.That(runtime.Calls, Does.Not.Contain("remove"));
            Assert.That(runtime.Calls, Does.Not.Contain("write"));
            Assert.That(runtime.Calls, Does.Not.Contain("purge"));
        }

        [Test]
        public void PackageRun_TamperStillWritesEvidenceButSkipsPurge()
        {
            var runtime = new FakePackageRuntime { Tampered = true };
            var result = Run(runtime, false);

            Assert.That(result.TamperPending, Is.True);
            Assert.That(runtime.Calls, Contains.Item("write"));
            Assert.That(runtime.Calls, Does.Not.Contain("purge"));
        }

        [Test]
        public void Offline_AlreadyOfflineCompletesWithoutIsolation()
        {
            var runtime = new FakeDiskRuntime { LetterPresent = false };
            var result = new MediaOfflineLifecycle(runtime).Run(Record(), 2, @"C:\bak", false);

            Assert.That(result.Status, Is.EqualTo(MediaOfflineStatus.AlreadyOffline));
            Assert.That(runtime.IsolateCount, Is.Zero);
        }

        [Test]
        public void Offline_IsolationSuccessDoesNotHideAgain()
        {
            var runtime = new FakeDiskRuntime();
            var result = new MediaOfflineLifecycle(runtime).Run(Record(), 3, @"C:\bak", false);

            Assert.That(result.Status, Is.EqualTo(MediaOfflineStatus.FullyOffline));
            Assert.That(runtime.IsolateLevel, Is.EqualTo(3));
            Assert.That(runtime.HideCount, Is.Zero);
        }

        [Test]
        public void Offline_IsolationFailureFallsBackToHiddenLetter()
        {
            var runtime = new FakeDiskRuntime
            {
                IsolationError = new InvalidOperationException("whole disk unsupported")
            };
            var result = new MediaOfflineLifecycle(runtime).Run(Record(), 3, @"C:\bak", false);

            Assert.That(result.Status, Is.EqualTo(MediaOfflineStatus.FallbackHidden));
            Assert.That(runtime.HideCount, Is.EqualTo(1));
        }

        [Test]
        public void Offline_FailsWhenLetterRemainsAfterFallback()
        {
            var runtime = new FakeDiskRuntime
            {
                IsolationError = new InvalidOperationException("whole disk unsupported"),
                HideKeepsLetter = true
            };
            var result = new MediaOfflineLifecycle(runtime).Run(Record(), 3, @"C:\bak", false);

            Assert.That(result.Status, Is.EqualTo(MediaOfflineStatus.Failed));
            Assert.That(runtime.HideCount, Is.EqualTo(1));
        }

        [Test]
        public void Offline_SafetyRefusalNeverCallsIsolation()
        {
            var runtime = new FakeDiskRuntime { Refuse = "系统盘" };
            var result = new MediaOfflineLifecycle(runtime).Run(Record(), 2, @"C:\bak", false);

            Assert.That(result.Status, Is.EqualTo(MediaOfflineStatus.Refused));
            Assert.That(runtime.IsolateCount, Is.Zero);
            Assert.That(runtime.HideCount, Is.Zero);
        }

        [Test]
        public void Schedule_NextOpenPreservesInsideWindowRunTime()
        {
            var task = new MediaTaskDto
            {
                WindowStart = "09:00",
                WindowEnd = "11:00",
                NextRunUtc = new DateTime(2026, 11, 10, 10, 30, 0, DateTimeKind.Local)
            };

            var next = MediaSchedulePolicy.NextOpenLocal(
                new DateTime(2026, 11, 10, 10, 0, 0, DateTimeKind.Local),
                task);

            Assert.That(next, Is.EqualTo(new DateTime(2026, 11, 10, 10, 30, 0, DateTimeKind.Local)));
        }

        [Test]
        public void Schedule_OutsideWindowTargetsNextStart()
        {
            var task = new MediaTaskDto
            {
                WindowStart = "09:00",
                WindowEnd = "11:00"
            };

            var next = MediaSchedulePolicy.NextOpenLocal(
                new DateTime(2026, 11, 10, 8, 15, 0, DateTimeKind.Local),
                task);

            Assert.That(next, Is.EqualTo(new DateTime(2026, 11, 10, 9, 0, 0, DateTimeKind.Local)));
        }

        [Test]
        public void Schedule_WaitSkipsOnTimeout()
        {
            var now = new DateTime(2026, 11, 10, 10, 35, 0, DateTimeKind.Utc);
            var task = new MediaTaskDto
            {
                WindowStart = "09:00",
                WindowEnd = "11:00",
                WaitMinutes = 5,
                WaitingSinceUtc = now.AddMinutes(-6)
            };

            Assert.That(
                MediaSchedulePolicy.DecideWait(task, now, new DateTime(2026, 11, 10, 10, 35, 0)),
                Is.EqualTo(MediaWaitDecision.SkipTimedOut));
        }

        [Test]
        public void Schedule_WaitSkipsWhenWindowEnds()
        {
            var nowUtc = new DateTime(2026, 11, 10, 3, 30, 0, DateTimeKind.Utc);
            var task = new MediaTaskDto
            {
                WindowStart = "09:00",
                WindowEnd = "11:00",
                WaitMinutes = 30,
                WaitingSinceUtc = nowUtc.AddMinutes(-1)
            };

            Assert.That(
                MediaSchedulePolicy.DecideWait(task, nowUtc, new DateTime(2026, 11, 10, 11, 0, 0)),
                Is.EqualTo(MediaWaitDecision.SkipWindowEnded));
        }

        [Test]
        public void Schedule_WaitStartsThenContinues()
        {
            var nowUtc = DateTime.UtcNow;
            var task = new MediaTaskDto
            {
                WindowStart = "09:00",
                WindowEnd = "11:00",
                WaitMinutes = 5
            };

            Assert.That(
                MediaSchedulePolicy.DecideWait(task, nowUtc, new DateTime(2026, 11, 10, 10, 0, 0)),
                Is.EqualTo(MediaWaitDecision.StartWaiting));

            task.WaitingSinceUtc = nowUtc.AddMinutes(-1);
            Assert.That(
                MediaSchedulePolicy.DecideWait(task, nowUtc, new DateTime(2026, 11, 10, 10, 1, 0)),
                Is.EqualTo(MediaWaitDecision.ContinueWaiting));
        }

        private static MediaPackageRunResult Run(FakePackageRuntime runtime, bool alreadyTampered)
        {
            return new MediaPackageLifecycle(runtime).Run(
                Task(),
                @"C:\source",
                @"C:\bak",
                @"D:\files",
                @"D:\versions",
                new DateTime(2026, 11, 10, 2, 0, 0, DateTimeKind.Utc),
                alreadyTampered);
        }

        private static MediaTaskDto Task()
        {
            return new MediaTaskDto
            {
                Name = "介质任务",
                SourcePath = @"C:\source",
                ConflictMode = "overwrite",
                RetainVersions = 7,
                RetainDays = 30
            };
        }

        private static MediaRecordDto Record()
        {
            return new MediaRecordDto
            {
                Id = 1,
                SerialNumber = "SERIAL",
                DiskSignature = "SIG",
                CapacityGb = 64
            };
        }

        private sealed class FakePackageRuntime : IMediaPackageRuntime
        {
            public readonly List<string> Calls = new List<string>();
            public bool DestinationExists;
            public long DestinationSizeAfterCopy = 12;
            public bool Tampered;

            public string Pack(MediaTaskDto task, string source, string bakDir, DateTime utc)
            {
                Calls.Add("pack");
                return @"C:\bak\task\package.zip";
            }

            public bool FileExists(string path)
            {
                return DestinationExists;
            }

            public void CreateDirectory(string path)
            {
            }

            public FileStamp Stamp(string path, string relativePath, bool hash)
            {
                var isDestination = relativePath == @"task\package.zip" && path.StartsWith(@"D:\files", StringComparison.OrdinalIgnoreCase);
                return new FileStamp
                {
                    Rel = relativePath,
                    Size = isDestination && Calls.Contains("copy") ? DestinationSizeAfterCopy : 12,
                    Mtime = "same"
                };
            }

            public void CopyPart(string source, string destination, string conflictMode)
            {
                Calls.Add("copy");
            }

            public bool RemoveLocalAfterCopy(string zipPath, bool landed)
            {
                Calls.Add("remove");
                return landed;
            }

            public void WriteAllText(string path, string text)
            {
                Calls.Add("write");
            }

            public bool LooksTampered(string filesRoot, string versionsRoot, bool useHash)
            {
                Calls.Add("tamper");
                return Tampered;
            }

            public int PurgeVersions(string filesRoot, string versionsRoot, int keepVersions, int keepDays)
            {
                Calls.Add("purge");
                return 2;
            }
        }

        private sealed class FakeDiskRuntime : IMediaDiskRuntime
        {
            public bool LetterPresent = true;
            public bool HideKeepsLetter;
            public string Refuse;
            public Exception IsolationError;
            public int IsolateCount;
            public int IsolateLevel;
            public int HideCount;

            public LiveDiskDto Find(MediaRecordDto record)
            {
                return new LiveDiskDto { DriveLetter = LetterPresent ? "E:" : null };
            }

            public bool HasLetter(LiveDiskDto disk)
            {
                return LetterPresent && disk != null && !string.IsNullOrEmpty(disk.DriveLetter);
            }

            public void FlushLockDismount(string letter)
            {
            }

            public string RefuseIsolate(LiveDiskDto disk, string bakPath, bool registered)
            {
                return Refuse;
            }

            public void Isolate(LiveDiskDto disk, int level)
            {
                IsolateCount++;
                IsolateLevel = level;
                if (IsolationError != null) throw IsolationError;
                LetterPresent = false;
            }

            public string HideLetter(MediaRecordDto record)
            {
                HideCount++;
                if (!HideKeepsLetter) LetterPresent = false;
                return "E:";
            }
        }
    }
}
