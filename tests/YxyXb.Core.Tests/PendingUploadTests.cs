using System.Collections.Generic;
using NUnit.Framework;
using YxyXb.Core.Backup;

namespace YxyXb.Core.Tests
{
    public class PendingUploadTests
    {
        [Test]
        public void UploadPackageFiles_RetriesOnlyUnfinishedParts_AndManifestLast()
        {
            var item = new PendingItem
            {
                UploadFiles = new[]
                {
                    new PendingUploadFile { LocalPath = "part01.zip" },
                    new PendingUploadFile { LocalPath = "part02.zip" },
                    new PendingUploadFile { LocalPath = "manifest.json" }
                }
            };
            var calls = new List<string>();
            var failSecond = true;

            var first = PendingFiles.UploadPackageFiles(
                item,
                true,
                false,
                false,
                null,
                delegate(string channel, PendingUploadFile file)
                {
                    calls.Add(file.LocalPath);
                    return !(failSecond && file.LocalPath == "part02.zip");
                });

            Assert.That(first, Is.False);
            Assert.That(calls, Is.EqualTo(new[] { "part01.zip", "part02.zip", "manifest.json" }));

            failSecond = false;
            calls.Clear();
            var second = PendingFiles.UploadPackageFiles(
                item,
                true,
                false,
                false,
                null,
                delegate(string channel, PendingUploadFile file)
                {
                    calls.Add(file.LocalPath);
                    return true;
                });

            Assert.That(second, Is.True);
            Assert.That(calls, Is.EqualTo(new[] { "part02.zip" }));
            Assert.That(item.UploadFiles[2].NetworkOk, Is.True);
        }
    }
}
