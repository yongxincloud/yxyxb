using System;
using System.IO;
using NUnit.Framework;
using YxyXb.Core.Backup;

namespace YxyXb.Core.Tests
{
    public class PendingBackupTests
    {
        [Test]
        public void DecideFile_SameFingerprint_Reuses()
        {
            var pending = FilePending("local", @"D:\src", "abc", 5, "hash", @"D:\bak\a.zip");
            var now = Snap("local", @"d:\src", "abc", 5, "hash");
            var decision = PendingBackup.DecideFile(pending, now, delegate(string path) { return true; });
            Assert.That(decision.Reuse, Is.True);
            Assert.That(decision.Replace.Length, Is.EqualTo(0));
            Assert.That(decision.Keep[0].ZipPath, Is.EqualTo(@"D:\bak\a.zip"));
        }

        [Test]
        public void DecideFile_SizeChangeOrMissingZipOrSource_Repacks()
        {
            var pending = FilePending("local", @"D:\src", "abc", 5, "hash", @"D:\bak\a.zip");
            var changed = PendingBackup.DecideFile(pending, Snap("local", @"D:\src", "zzz", 5, "hash"), delegate(string p) { return true; });
            Assert.That(changed.Reuse, Is.False);
            Assert.That(changed.Replace.Length, Is.EqualTo(1));

            var level = PendingBackup.DecideFile(pending, Snap("local", @"D:\src", "abc", 9, "hash"), delegate(string p) { return true; });
            Assert.That(level.Reuse, Is.False);

            var password = PendingBackup.DecideFile(pending, Snap("local", @"D:\src", "abc", 5, "other"), delegate(string p) { return true; });
            Assert.That(password.Reuse, Is.False);

            var path = PendingBackup.DecideFile(pending, Snap("local", @"E:\other", "abc", 5, "hash"), delegate(string p) { return true; });
            Assert.That(path.Reuse, Is.False);

            var kind = PendingBackup.DecideFile(pending, Snap("unc", @"D:\src", "abc", 5, "hash"), delegate(string p) { return true; });
            Assert.That(kind.Reuse, Is.False);

            var missing = PendingBackup.DecideFile(pending, Snap("local", @"D:\src", "abc", 5, "hash"), delegate(string p) { return false; });
            Assert.That(missing.Reuse, Is.False);
            Assert.That(missing.Replace[0].ZipPath, Is.EqualTo(@"D:\bak\a.zip"));
        }

        [Test]
        public void DecideFile_BadJson_DoesNotNameAZipToDelete()
        {
            Assert.That(PendingBackup.Parse("{"), Is.Null);
            var decision = PendingBackup.DecideFile(null, Snap("local", @"D:\src", "abc", 5, ""), delegate(string p) { return false; });
            Assert.That(decision.Reuse, Is.False);
            Assert.That(decision.Replace.Length, Is.EqualTo(0));
        }

        [Test]
        public void DecideDatabase_CycleInstanceAndMissingZip()
        {
            var until = new DateTime(2026, 10, 13, 2, 0, 0, DateTimeKind.Utc).ToString("o");
            var pending = DbPending(7, new[] { "A", "B" }, until, 5, "", @"D:\a.zip", @"D:\b.zip");
            var now = DbSnap(7, new[] { "B", "A" }, 5, "");
            var early = new DateTime(2026, 10, 12, 3, 0, 0, DateTimeKind.Utc);
            var reuse = PendingBackup.DecideDatabase(pending, now, early, delegate(string p) { return true; });
            Assert.That(reuse.Reuse, Is.True);

            var due = PendingBackup.DecideDatabase(pending, now, new DateTime(2026, 10, 13, 2, 0, 0, DateTimeKind.Utc), delegate(string p) { return true; });
            Assert.That(due.Reuse, Is.False);
            Assert.That(due.ResetCycle, Is.True);
            Assert.That(due.Replace.Length, Is.EqualTo(2));

            pending.CycleUntilUtc = null;
            var once = PendingBackup.DecideDatabase(pending, now, new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc), delegate(string p) { return true; });
            Assert.That(once.Reuse, Is.True);

            pending.CycleUntilUtc = until;
            var instance = PendingBackup.DecideDatabase(pending, DbSnap(8, new[] { "A", "B" }, 5, ""), early, delegate(string p) { return true; });
            Assert.That(instance.ResetCycle, Is.True);
            Assert.That(instance.Replace.Length, Is.EqualTo(2));

            var names = PendingBackup.DecideDatabase(pending, DbSnap(7, new[] { "A" }, 5, ""), early, delegate(string p) { return true; });
            Assert.That(names.ResetCycle, Is.True);

            var one = PendingBackup.DecideDatabase(pending, now, early, delegate(string p) { return !p.EndsWith("b.zip"); });
            Assert.That(one.Reuse, Is.False);
            Assert.That(one.ResetCycle, Is.False);
            Assert.That(one.Replace.Length, Is.EqualTo(1));
            Assert.That(one.Replace[0].Key, Is.EqualTo("B"));
        }

        [Test]
        public void ChannelsToSend_SkipsChannelAlreadyOk()
        {
            var item = new PendingItem { NetworkOk = true, FtpOk = false, CloudOk = false };
            var channels = PendingBackup.ChannelsToSend(item, true, false, true);
            Assert.That(channels, Is.EqualTo(new[] { "cloud" }));
            Assert.That(PendingBackup.NeedsUpload(item, true, false, true), Is.True);
            item.CloudOk = true;
            Assert.That(PendingBackup.NeedsUpload(item, true, false, true), Is.False);
            Assert.That(PendingBackup.NeedsUpload(item, false, false, false), Is.False);
        }

        [Test]
        public void AllowPack_FollowsDeleteResult()
        {
            Assert.That(PendingBackup.AllowPack(false), Is.False);
            Assert.That(PendingBackup.AllowPack(true), Is.True);
        }

        [Test]
        public void TryDeleteZip_RemovesZipAndPart()
        {
            var dir = Path.Combine(Path.GetTempPath(), "yxy-zip-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var zip = Path.Combine(dir, "a.zip");
            File.WriteAllText(zip, "zip");
            File.WriteAllText(zip + ".yxypart", "part");
            try
            {
                Assert.That(PendingFiles.TryDeleteZip(zip), Is.True);
                Assert.That(File.Exists(zip), Is.False);
                Assert.That(File.Exists(zip + ".yxypart"), Is.False);
                Assert.That(PendingFiles.TryDeleteZip(zip), Is.True);
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }

        private static FileSnapshot Snap(string kind, string path, string fingerprint, int level, string passwordHash)
        {
            return new FileSnapshot
            {
                SourceKind = kind,
                SourcePath = path,
                Fingerprint = fingerprint,
                ZipLevel = level,
                PasswordSha256 = passwordHash
            };
        }

        private static PendingRecord FilePending(string kind, string path, string fingerprint, int level, string passwordHash, string zip)
        {
            return new PendingRecord
            {
                SourceKind = kind,
                SourcePath = path,
                Items = new[]
                {
                    new PendingItem
                    {
                        Key = "file",
                        ZipPath = zip,
                        Fingerprint = fingerprint,
                        ZipLevel = level,
                        PasswordSha256 = passwordHash
                    }
                }
            };
        }

        private static DatabaseSnapshot DbSnap(long instanceId, string[] names, int level, string passwordHash)
        {
            return new DatabaseSnapshot
            {
                InstanceId = instanceId,
                DbNames = names,
                ZipLevel = level,
                PasswordSha256 = passwordHash
            };
        }

        private static PendingRecord DbPending(long instanceId, string[] names, string until, int level, string passwordHash, string zipA, string zipB)
        {
            return new PendingRecord
            {
                InstanceId = instanceId,
                DbNames = names,
                CycleUntilUtc = until,
                Items = new[]
                {
                    new PendingItem { Key = "A", ZipPath = zipA, ZipLevel = level, PasswordSha256 = passwordHash },
                    new PendingItem { Key = "B", ZipPath = zipB, ZipLevel = level, PasswordSha256 = passwordHash }
                }
            };
        }
    }
}
