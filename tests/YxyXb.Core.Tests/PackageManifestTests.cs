using NUnit.Framework;
using YxyXb.Core.Backup;

namespace YxyXb.Core.Tests
{
    public class PackageManifestTests
    {
        [Test]
        public void Resolve_UsesTaskThenGlobalThenDefault()
        {
            Assert.That(PackageLimits.Resolve(8L << 30, 2L << 30), Is.EqualTo(2L << 30));
            Assert.That(PackageLimits.Resolve(8L << 30, 0), Is.EqualTo(8L << 30));
            Assert.That(PackageLimits.Resolve(0, 0), Is.EqualTo(PackageLimits.DefaultBytes));
        }

        [Test]
        public void NormalizeGlobal_ClampsToSupportedRange()
        {
            Assert.That(PackageLimits.NormalizeGlobal(1), Is.EqualTo(PackageLimits.MinimumBytes));
            Assert.That(PackageLimits.NormalizeGlobal(PackageLimits.MaximumBytes + 1), Is.EqualTo(PackageLimits.MaximumBytes));
        }

        [Test]
        public void Manifest_JsonRoundTripKeepsParts()
        {
            var source = new BackupPackageManifest
            {
                FormatVersion = 1,
                BackupId = "20261104-153000-7f3a2c11",
                PackageName = "a.zip",
                TaskName = "财务",
                CreatedUtc = "2026-11-04T07:30:00.0000000Z",
                PartSizeLimitBytes = PackageLimits.DefaultBytes,
                ArchiveBytes = 9000000,
                ArchiveSha256 = "abc",
                SourceFileCount = 10,
                PackedFileCount = 9,
                SkippedFileCount = 1,
                SourceBytes = 8000,
                Parts = new[]
                {
                    new PackagePartManifest
                    {
                        Index = 1,
                        FileName = "a.part01.zip",
                        PayloadEntryName = "payload/a.part01.bin",
                        PayloadBytes = 9000000,
                        PayloadSha256 = "def",
                        WrappedBytes = 9001000,
                        WrappedSha256 = "ghi"
                    }
                }
            };

            var copy = PackageManifestCodec.Parse(PackageManifestCodec.ToJson(source));

            Assert.That(copy.BackupId, Is.EqualTo(source.BackupId));
            Assert.That(copy.Parts[0].PayloadSha256, Is.EqualTo("def"));
        }
    }
}

