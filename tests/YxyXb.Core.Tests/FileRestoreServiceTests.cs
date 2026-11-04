using System.IO;
using NUnit.Framework;
using YxyXb.Core.Backup;

namespace YxyXb.Core.Tests
{
    public class FileRestoreServiceTests
    {
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "yxy-restore-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [Test]
        public void Restore_CreatesTimestampDirectory_AndExtractsFiles()
        {
            var source = Path.Combine(_root, "source");
            Directory.CreateDirectory(source);
            var input = Path.Combine(source, "a.txt");
            var payload = new byte[2 * 1024 * 1024];
            new System.Random(9).NextBytes(payload);
            File.WriteAllBytes(input, payload);
            var zip = Path.Combine(_root, "a.zip");
            ZipPacker.Pack(
                new[] { new SourceFile { FullPath = input, RelativePath = "folder/a.txt" } },
                zip,
                "",
                0);

            var downloads = Path.Combine(_root, "downloads");
            Directory.CreateDirectory(downloads);
            var split = PackageSplitter.Split(
                zip,
                "任务",
                2L * 1024 * 1024,
                new BackupRunStats { DiscoveredFiles = 1, PackedFiles = 1 },
                delegate { return "restore1"; });
            File.Delete(zip);

            var outputParent = Path.Combine(_root, "output");
            Directory.CreateDirectory(outputParent);
            var result = FileRestoreService.Restore(split.PackageDirectory, outputParent, "");

            Assert.That(Directory.Exists(result.OutputDirectory), Is.True);
            Assert.That(Path.GetFileName(result.OutputDirectory), Does.StartWith("restore-"));
            Assert.That(File.ReadAllBytes(Path.Combine(result.OutputDirectory, "folder", "a.txt")),
                Is.EqualTo(payload));
            Assert.That(result.FileCount, Is.EqualTo(1));
        }

        [Test]
        public void ExtractAllSafe_RejectsPathTraversal()
        {
            var malicious = Path.Combine(_root, "evil.zip");
            using (var output = File.Create(malicious))
            using (var zip = new ICSharpCode.SharpZipLib.Zip.ZipOutputStream(output))
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes("bad");
                zip.PutNextEntry(new ICSharpCode.SharpZipLib.Zip.ZipEntry("../outside.txt") { Size = bytes.Length });
                zip.Write(bytes, 0, bytes.Length);
                zip.CloseEntry();
                zip.Finish();
            }

            var target = Path.Combine(_root, "safe");
            Directory.CreateDirectory(target);
            var error = Assert.Throws<System.InvalidOperationException>(
                delegate { ZipPacker.ExtractAllSafe(malicious, "", target); });

            Assert.That(error.Message, Does.Contain("路径越界"));
            Assert.That(File.Exists(Path.Combine(_root, "outside.txt")), Is.False);
        }

        [Test]
        public void CreateUniqueTimestampDirectory_AvoidsExistingDirectory()
        {
            var first = FileRestoreService.CreateUniqueTimestampDirectory(_root);
            var second = FileRestoreService.CreateUniqueTimestampDirectory(_root);

            Assert.That(second, Is.Not.EqualTo(first));
            Assert.That(Directory.Exists(second), Is.True);
        }
    }
}
