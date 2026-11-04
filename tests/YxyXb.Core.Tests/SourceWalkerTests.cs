using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using YxyXb.Core.Backup;

namespace YxyXb.Core.Tests
{
    public class SourceWalkerTests
    {
        [Test]
        public void ScanLocal_ReportsDiscoveredPackedAndSkippedFiles()
        {
            var root = Path.Combine(Path.GetTempPath(), "yxy-scan-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var readable = Path.Combine(root, "readable.txt");
            var locked = Path.Combine(root, "locked.txt");
            File.WriteAllText(readable, "12345");
            File.WriteAllText(locked, "12");

            try
            {
                using (new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    var result = SourceWalker.ScanLocal(root);

                    Assert.That(result.Stats.DiscoveredFiles, Is.EqualTo(2));
                    Assert.That(result.Stats.PackedFiles, Is.EqualTo(1));
                    Assert.That(result.Stats.SkippedFiles, Is.EqualTo(1));
                    Assert.That(result.Stats.SourceBytes, Is.EqualTo(5));
                    Assert.That(result.SkippedPaths, Has.Some.EndsWith("locked.txt"));
                }
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void WalkLocal_KeepsSkippedDiagnosticsForExistingCallers()
        {
            var root = Path.Combine(Path.GetTempPath(), "yxy-scan-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var readable = Path.Combine(root, "readable.txt");
            var locked = Path.Combine(root, "locked.txt");
            File.WriteAllText(readable, "ok");
            File.WriteAllText(locked, "locked");

            try
            {
                using (new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    var skipped = new List<string>();
                    var files = SourceWalker.WalkLocal(root, skipped);

                    Assert.That(files.Count, Is.EqualTo(1));
                    Assert.That(files[0].RelativePath, Is.EqualTo("readable.txt"));
                    Assert.That(skipped, Has.Some.EndsWith("locked.txt"));
                }
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }
    }
}

