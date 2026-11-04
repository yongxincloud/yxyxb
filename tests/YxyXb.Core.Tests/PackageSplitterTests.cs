using System.IO;
using NUnit.Framework;
using YxyXb.Core.Backup;

namespace YxyXb.Core.Tests
{
    public class PackageSplitterTests
    {
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "yxy-split-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [Test]
        public void SplitAndAssemble_RoundTripsArchiveBytes()
        {
            var full = Path.Combine(_root, "a.zip");
            var bytes = new byte[3 * 1024 * 1024];
            new System.Random(7).NextBytes(bytes);
            File.WriteAllBytes(full, bytes);

            var result = PackageSplitter.Split(
                full,
                "任务",
                2L * 1024 * 1024,
                new BackupRunStats { DiscoveredFiles = 1, PackedFiles = 1 },
                delegate { return "b1"; });

            Assert.That(result, Is.Not.Null);
            Assert.That(result.PartPaths.Length, Is.GreaterThanOrEqualTo(3));
            Assert.That(File.Exists(result.ManifestPath), Is.True);

            var work = Path.Combine(_root, "work");
            Directory.CreateDirectory(work);
            var assembled = PackageSplitter.Assemble(result.PackageDirectory, work);
            Assert.That(File.ReadAllBytes(assembled), Is.EqualTo(bytes));
        }

        [Test]
        public void Assemble_MissingPart_Fails()
        {
            var full = Path.Combine(_root, "a.zip");
            File.WriteAllBytes(full, new byte[3 * 1024 * 1024]);
            var result = PackageSplitter.Split(
                full,
                "任务",
                2L * 1024 * 1024,
                new BackupRunStats(),
                delegate { return "b2"; });

            File.Delete(result.PartPaths[1]);

            var error = Assert.Throws<System.InvalidOperationException>(
                delegate { PackageSplitter.Assemble(result.PackageDirectory, _root); });
            Assert.That(error.Message, Does.Contain("缺少分卷"));
        }

        [Test]
        public void Split_DoesNotCreatePartsWhenArchiveIsUnderLimit()
        {
            var full = Path.Combine(_root, "small.zip");
            File.WriteAllBytes(full, new byte[1024]);

            var result = PackageSplitter.Split(
                full,
                "任务",
                2048,
                new BackupRunStats(),
                delegate { return "b3"; });

            Assert.That(result, Is.Null);
            Assert.That(File.Exists(full), Is.True);
        }
    }
}

