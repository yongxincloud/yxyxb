using NUnit.Framework;
using YxyXb.Aliyun;
using YxyXb.Contracts.Cloud;
using YxyXb.Contracts.Dto;
using YxyXb.Ctyun;
using YxyXb.TencentCloud;

namespace YxyXb.CloudRecovery.Tests
{
    public class CloudProviderErrorTests
    {
        [Test]
        public void AliyunStat404_IsObjectMissing()
        {
            var storage = new AliyunStorage(AliyunAccount(), new AliyunTransport
            {
                Reply = new AliyunReply { Status = 404 }
            });

            Assert.Throws<CloudObjectNotFoundException>(delegate { storage.Stat("task/package.zip"); });
        }

        [Test]
        public void TencentStat404_IsObjectMissing()
        {
            var storage = new TencentCloudStorage(TencentAccount(), new TencentTransport
            {
                Reply = new TencentReply { Status = 404 }
            });

            Assert.Throws<CloudObjectNotFoundException>(delegate { storage.Stat("task/package.zip"); });
        }

        [Test]
        public void CtyunStat404_IsObjectMissing()
        {
            var storage = new CtyunStorage(CtyunAccount(), new CtyunTransport
            {
                Reply = new CtyunReply { Status = 404 }
            });

            Assert.Throws<CloudObjectNotFoundException>(delegate { storage.Stat("task/package.zip"); });
        }

        [Test]
        public void ObjectStorage403_ExplainsLockAsWellAsCredentials()
        {
            var aliyun = new AliyunStorage(AliyunAccount(), new AliyunTransport
            {
                Reply = new AliyunReply { Status = 403 }
            });
            var tencent = new TencentCloudStorage(TencentAccount(), new TencentTransport
            {
                Reply = new TencentReply { Status = 403 }
            });
            var ctyun = new CtyunStorage(CtyunAccount(), new CtyunTransport
            {
                Reply = new CtyunReply { Status = 403 }
            });

            AssertLockMessage(aliyun);
            AssertLockMessage(tencent);
            AssertLockMessage(ctyun);
        }

        private static void AssertLockMessage(ICloudStorage storage)
        {
            var error = Assert.Throws<System.InvalidOperationException>(storage.TestConnection);
            Assert.That(error.Message, Does.Contain("密钥"));
            Assert.That(error.Message, Does.Contain("对象锁定"));
        }

        private static CloudAccountDto AliyunAccount()
        {
            return new CloudAccountDto
            {
                Provider = CloudProviders.Aliyun,
                Enabled = true,
                Bucket = "backup",
                AccessKey = "ak",
                SecretKey = "sk",
                Zone = "oss-cn-hangzhou"
            };
        }

        private static CloudAccountDto TencentAccount()
        {
            return new CloudAccountDto
            {
                Provider = CloudProviders.Tencent,
                Enabled = true,
                Bucket = "backup-1250000000",
                AccessKey = "ak",
                SecretKey = "sk",
                Zone = "ap-guangzhou"
            };
        }

        private static CloudAccountDto CtyunAccount()
        {
            return new CloudAccountDto
            {
                Provider = CloudProviders.Ctyun,
                Enabled = true,
                Bucket = "backup",
                AccessKey = "ak",
                SecretKey = "sk",
                Zone = "ctyun"
            };
        }

        private sealed class AliyunTransport : IAliyunTransport
        {
            public AliyunReply Reply { get; set; }

            public AliyunReply Send(AliyunExchange call)
            {
                return Reply;
            }
        }

        private sealed class TencentTransport : ITencentTransport
        {
            public TencentReply Reply { get; set; }

            public TencentReply Send(TencentExchange call)
            {
                return Reply;
            }
        }

        private sealed class CtyunTransport : ICtyunTransport
        {
            public CtyunReply Reply { get; set; }

            public CtyunReply Send(CtyunExchange call)
            {
                return Reply;
            }
        }
    }
}
