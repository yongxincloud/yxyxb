using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using YxyXb.Baidu;
using YxyXb.Core;

namespace YxyXb.Baidu.Tests
{
    [TestFixture]
    public sealed class BaiduUploadLogTests
    {
        [Test]
        public void Write_CreatesSliceLogFile()
        {
            var root = Path.Combine(Path.GetTempPath(), "yxy-blog-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            AppPaths.SetRootForTests(root);
            try
            {
                AppPaths.EnsureDirectories();
                var local = Path.Combine(root, "a.zip");
                BaiduUploadLog.Write(AppPaths.DataDirectory, local, "分片 1/2 成功");
                var path = BaiduUploadLog.PathFor(AppPaths.DataDirectory, local);
                Assert.That(Path.GetFileName(path), Does.Contain("a.zip"));
                Assert.That(File.Exists(path), Is.True);
                Assert.That(File.ReadAllText(path, Encoding.UTF8), Does.Contain("分片 1/2 成功"));
                Assert.That(BaiduUploadLog.ReadRecent(AppPaths.DataDirectory, 4000), Does.Contain("a.zip"));
            }
            finally
            {
                AppPaths.SetRootForTests(null);
                Directory.Delete(root, true);
            }
        }
    }
}
