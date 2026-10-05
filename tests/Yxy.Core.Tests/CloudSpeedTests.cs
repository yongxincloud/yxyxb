using System.IO;
using NUnit.Framework;
using Yxy.Core;
using Yxy.Core.Transfer;

namespace Yxy.Core.Tests
{
    [TestFixture]
    public sealed class CloudSpeedTests
    {
        [Test]
        public void UploadUsesMinThenMax()
        {
            Assert.That(CloudSpeed.UploadKbps(100, 300), Is.EqualTo(100));
            Assert.That(CloudSpeed.UploadKbps(0, 300), Is.EqualTo(300));
            Assert.That(CloudSpeed.UploadKbps(0, 0), Is.EqualTo(0));
        }

        [Test]
        public void DownloadUsesMax()
        {
            Assert.That(CloudSpeed.DownloadKbps(100, 300), Is.EqualTo(300));
            Assert.That(CloudSpeed.DownloadKbps(100, 0), Is.EqualTo(0));
        }

        [Test]
        public void StreamPace_UnlimitedCopiesAll()
        {
            var src = new MemoryStream(new byte[] { 1, 2, 3, 4 });
            var dst = new MemoryStream();
            StreamPace.Copy(src, dst, 0);
            Assert.That(dst.ToArray(), Is.EqualTo(new byte[] { 1, 2, 3, 4 }));
        }
    }
}
