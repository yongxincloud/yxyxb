using System;
using System.IO;
using NUnit.Framework;
using YxyXb.Core.Backup;

namespace YxyXb.Core.Tests
{
    public class PurgeUnitTests
    {
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "yxy-unit-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [Test]
        public void IsExpiredUnit_RequiresEveryFileToBeOld()
        {
            var unit = WriteUnit();
            var cutoff = DateTime.UtcNow.AddDays(-5);
            File.SetLastWriteTimeUtc(Path.Combine(unit, "a.part01.zip"), DateTime.UtcNow.AddDays(-10));
            File.SetLastWriteTimeUtc(Path.Combine(unit, "a.manifest.json"), DateTime.UtcNow);

            Assert.That(BackupUnitPurge.IsExpiredUnit(unit, cutoff), Is.False);

            File.SetLastWriteTimeUtc(Path.Combine(unit, "a.manifest.json"), DateTime.UtcNow.AddDays(-10));
            Assert.That(BackupUnitPurge.IsExpiredUnit(unit, cutoff), Is.True);
        }

        [Test]
        public void DeleteLocalUnit_RemovesPartsThenDirectory()
        {
            var unit = WriteUnit();
            File.SetLastWriteTimeUtc(Path.Combine(unit, "a.part01.zip"), DateTime.UtcNow.AddDays(-10));
            File.SetLastWriteTimeUtc(Path.Combine(unit, "a.manifest.json"), DateTime.UtcNow.AddDays(-10));

            Assert.That(BackupUnitPurge.DeleteLocalUnit(unit), Is.True);
            Assert.That(Directory.Exists(unit), Is.False);
        }

        [Test]
        public void LocalPurge_RemovesExpiredUnitAsWhole()
        {
            var unit = WriteUnit();
            File.SetLastWriteTimeUtc(Path.Combine(unit, "a.part01.zip"), DateTime.UtcNow.AddDays(-10));
            File.SetLastWriteTimeUtc(Path.Combine(unit, "a.manifest.json"), DateTime.UtcNow.AddDays(-10));

            var deleted = LocalPurge.DeleteOldZips(_root, 5, DateTime.UtcNow);

            Assert.That(deleted, Is.EqualTo(1));
            Assert.That(Directory.Exists(unit), Is.False);
        }

        private string WriteUnit()
        {
            var unit = Path.Combine(_root, "a.split-id");
            Directory.CreateDirectory(unit);
            File.WriteAllText(Path.Combine(unit, "a.part01.zip"), "zip");
            var manifest = new BackupPackageManifest
            {
                FormatVersion = 1,
                BackupId = "id",
                PackageName = "a.zip",
                TaskName = "task",
                CreatedUtc = DateTime.UtcNow.ToString("o"),
                PartSizeLimitBytes = PackageLimits.DefaultBytes,
                ArchiveBytes = 3,
                ArchiveSha256 = "archive",
                Parts = new[]
                {
                    new PackagePartManifest
                    {
                        Index = 1,
                        FileName = "a.part01.zip",
                        PayloadEntryName = "payload/a.part01.bin",
                        PayloadBytes = 3,
                        PayloadSha256 = "payload"
                    }
                }
            };
            File.WriteAllText(Path.Combine(unit, "a.manifest.json"), PackageManifestCodec.ToJson(manifest));
            return unit;
        }
    }
}
