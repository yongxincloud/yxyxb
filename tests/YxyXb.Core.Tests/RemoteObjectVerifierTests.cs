using System;
using System.IO;
using NUnit.Framework;
using YxyXb.Contracts.Cloud;
using YxyXb.Core.Cloud;

namespace YxyXb.Core.Tests
{
    public class RemoteObjectVerifierTests
    {
        [Test]
        public void EnsureSameSize_ThrowsWhenRemoteLengthDiffers()
        {
            var path = Path.Combine(Path.GetTempPath(), "yxy-remote-" + Guid.NewGuid().ToString("N"));
            File.WriteAllBytes(path, new byte[12]);
            try
            {
                Assert.Throws<InvalidOperationException>(delegate
                {
                    RemoteObjectVerifier.EnsureSameSize(path, new CloudObjectInfo { Size = 11 });
                });
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public void EnsureSameSize_AllowsMatchingLength()
        {
            var path = Path.Combine(Path.GetTempPath(), "yxy-remote-" + Guid.NewGuid().ToString("N"));
            File.WriteAllBytes(path, new byte[12]);
            try
            {
                RemoteObjectVerifier.EnsureSameSize(path, new CloudObjectInfo { Size = 12, Checksum = "etag" });
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}

