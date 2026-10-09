using System;
using System.Collections.Generic;
using System.IO;
using ICSharpCode.SharpZipLib.Zip;
using NUnit.Framework;
using YxyXb.Contracts.Dto;
using YxyXb.Media.Sync;

namespace YxyXb.Media.Tests
{
    [TestFixture]
    public sealed class MediaPackTests
    {
        [Test]
        public void Pack_PutsEveryFileIntoOneZipUnderBak()
        {
            var root = Path.Combine(Path.GetTempPath(), "yxy-pack-" + Guid.NewGuid().ToString("N"));
            var source = Path.Combine(root, "src");
            var bak = Path.Combine(root, "bak");
            Directory.CreateDirectory(Path.Combine(source, "sub"));
            File.WriteAllText(Path.Combine(source, "a.txt"), "hello");
            File.WriteAllText(Path.Combine(source, "sub", "b.txt"), "world");
            try
            {
                var zip = MediaPack.PackToBak(source, bak, "sid", "任务", null, 1, new DateTime(2026, 10, 9, 10, 0, 0, DateTimeKind.Utc), NameParts.Default);
                Assert.That(File.Exists(zip), Is.True);
                Assert.That(Path.GetFullPath(zip).StartsWith(Path.GetFullPath(bak), StringComparison.OrdinalIgnoreCase), Is.True);
                var names = EntryNames(zip);
                Assert.That(names, Does.Contain("a.txt"));
                Assert.That(names, Has.Some.Contains("b.txt"));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        [Test]
        public void RemoveLocal_DeletesZipOnlyAfterItLands()
        {
            var root = Path.Combine(Path.GetTempPath(), "yxy-del-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var zip = Path.Combine(root, "a.zip");
            File.WriteAllText(zip, "zip");
            try
            {
                Assert.That(MediaPack.RemoveLocalAfterCopy(zip, false), Is.False);
                Assert.That(File.Exists(zip), Is.True);
                Assert.That(MediaPack.RemoveLocalAfterCopy(zip, true), Is.True);
                Assert.That(File.Exists(zip), Is.False);
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        private static IList<string> EntryNames(string zipPath)
        {
            var names = new List<string>();
            using (var zip = new ZipFile(zipPath))
            {
                foreach (ZipEntry entry in zip)
                {
                    if (entry.IsFile)
                    {
                        names.Add(entry.Name.Replace('\\', '/'));
                    }
                }
            }

            return names;
        }
    }
}
