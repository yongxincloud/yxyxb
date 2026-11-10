using System;
using System.IO;
using NUnit.Framework;
using YxyXb.Contracts.Cloud;
using YxyXb.Core.Cloud;

namespace YxyXb.CloudRecovery.Tests
{
    public class ObjectUploadRetryTests
    {
        [Test]
        public void SmallFile_ResponseLostAfterCommit_DoesNotOverwriteWithinSameCall()
        {
            var path = Path.Combine(Path.GetTempPath(), "yxy-cloud-retry-" + Guid.NewGuid().ToString("N"));
            File.WriteAllBytes(path, new byte[32]);
            var api = new FakeApi();
            try
            {
                Assert.Throws<IOException>(delegate
                {
                    ObjectUpload.Run(path, "task/package.zip", api, null, delegate(int ms) { });
                });
                Assert.That(api.PutWholeCalls, Is.EqualTo(1));
            }
            finally
            {
                File.Delete(path);
            }
        }

        private sealed class FakeApi : IObjectApi
        {
            public int PutWholeCalls { get; private set; }

            public void PutWhole(string key, byte[] body)
            {
                PutWholeCalls++;
                throw new IOException("response lost after commit");
            }

            public string Start(string key)
            {
                throw new NotSupportedException();
            }

            public string PutPart(string key, string uploadId, int number, byte[] body)
            {
                throw new NotSupportedException();
            }

            public void Complete(string key, string uploadId, string[] etags)
            {
                throw new NotSupportedException();
            }

            public void Abort(string key, string uploadId)
            {
            }
        }
    }
}
