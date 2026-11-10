using System;
using System.IO;
using NUnit.Framework;
using YxyXb.Contracts.Cloud;
using YxyXb.Core.Cloud;

namespace YxyXb.CloudRecovery.Tests
{
    public class CloudUploadRecoveryTests
    {
        [Test]
        public void Upload_ExistingRemoteSameSize_SkipsUpload()
        {
            WithFile(32, delegate(string path)
            {
                var storage = new FakeStorage { RemoteSize = 32 };

                CloudUploadRecovery.Upload(storage, path, "task/package.zip");

                Assert.That(storage.UploadCalls, Is.EqualTo(0));
                Assert.That(storage.StatCalls, Is.EqualTo(1));
            });
        }

        [Test]
        public void Upload_ResponseLostAfterCommit_AcceptsMatchingRemote()
        {
            WithFile(32, delegate(string path)
            {
                var storage = new FakeStorage
                {
                    CommitBeforeUploadError = true,
                    UploadError = new IOException("response lost")
                };

                CloudUploadRecovery.Upload(storage, path, "task/package.zip");

                Assert.That(storage.UploadCalls, Is.EqualTo(1));
                Assert.That(storage.StatCalls, Is.EqualTo(2));
            });
        }

        [Test]
        public void Upload_ExistingRemoteDifferentSize_DoesNotOverwrite()
        {
            WithFile(32, delegate(string path)
            {
                var storage = new FakeStorage { RemoteSize = 31 };

                var error = Assert.Throws<InvalidOperationException>(delegate
                {
                    CloudUploadRecovery.Upload(storage, path, "task/package.zip");
                });

                Assert.That(error.Message, Does.Contain("大小不一致"));
                Assert.That(error.Message, Does.Contain("WORM"));
                Assert.That(storage.UploadCalls, Is.EqualTo(0));
            });
        }

        [Test]
        public void Upload_RemoteLookupDenied_DoesNotUpload()
        {
            WithFile(32, delegate(string path)
            {
                var storage = new FakeStorage
                {
                    StatError = new InvalidOperationException("access denied")
                };

                var error = Assert.Throws<InvalidOperationException>(delegate
                {
                    CloudUploadRecovery.Upload(storage, path, "task/package.zip");
                });

                Assert.That(error.Message, Does.Contain("access denied"));
                Assert.That(storage.UploadCalls, Is.EqualTo(0));
            });
        }

        private static void WithFile(int length, Action<string> test)
        {
            var path = Path.Combine(Path.GetTempPath(), "yxy-cloud-recovery-" + Guid.NewGuid().ToString("N"));
            File.WriteAllBytes(path, new byte[length]);
            try
            {
                test(path);
            }
            finally
            {
                File.Delete(path);
            }
        }

        private sealed class FakeStorage : ICloudStorage
        {
            public string ProviderKey
            {
                get { return "fake"; }
            }

            public bool IsImplemented
            {
                get { return true; }
            }

            public long? RemoteSize { get; set; }

            public Exception StatError { get; set; }

            public Exception UploadError { get; set; }

            public bool CommitBeforeUploadError { get; set; }

            public int StatCalls { get; private set; }

            public int UploadCalls { get; private set; }

            public void TestConnection()
            {
            }

            public void SetSpeed(int minKbps, int maxKbps)
            {
            }

            public void SetParallel(int parallel)
            {
            }

            public void Upload(string localPath, string remoteKey)
            {
                UploadCalls++;
                if (CommitBeforeUploadError)
                {
                    RemoteSize = new FileInfo(localPath).Length;
                }

                if (UploadError != null)
                {
                    throw UploadError;
                }
            }

            public CloudObjectInfo Stat(string remoteKey)
            {
                StatCalls++;
                if (StatError != null)
                {
                    throw StatError;
                }

                if (!RemoteSize.HasValue)
                {
                    throw new CloudObjectNotFoundException("missing");
                }

                return new CloudObjectInfo { Size = RemoteSize.Value };
            }

            public int DeleteExpired(string prefix, DateTime olderThanUtc, Action<string> onDeleted)
            {
                return 0;
            }
        }
    }
}
