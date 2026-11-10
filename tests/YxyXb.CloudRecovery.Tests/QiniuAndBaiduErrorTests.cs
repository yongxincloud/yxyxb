using System;
using NUnit.Framework;
using YxyXb.Baidu;
using YxyXb.Contracts.Cloud;
using YxyXb.Contracts.Dto;
using YxyXb.Qiniu;

namespace YxyXb.CloudRecovery.Tests
{
    public class QiniuAndBaiduErrorTests
    {
        [Test]
        public void QiniuMissingObjectCode_IsRecognized()
        {
            Assert.That(QiniuHttp.IsMissingObjectError("七牛 HTTP 612：{\"error\":\"no such file\"}"), Is.True);
            Assert.That(QiniuHttp.IsMissingObjectError("七牛 HTTP 401：bad token"), Is.False);
        }

        [Test]
        public void QiniuUrlSafeBase64_KeepsPaddingForManagementPaths()
        {
            Assert.That(QiniuHttp.UrlSafeBase64("bucket:key"), Is.EqualTo("YnVja2V0OmtleQ=="));
        }

        [Test]
        public void QiniuStatJson_ExposesRemoteSize()
        {
            var stat = QiniuHttp.FromJson<QiniuStatResult>("{\"fsize\":4096,\"hash\":\"fixture\"}");

            Assert.That(stat.fsize, Is.EqualTo(4096));
            Assert.That(stat.hash, Is.EqualTo("fixture"));
        }

        [Test]
        public void BaiduEmptyDirListing_IsObjectMissing()
        {
            var account = new CloudAccountDto
            {
                Provider = CloudProviders.Baidu,
                Enabled = true,
                RefreshToken = "refresh",
                AccessToken = "access",
                TokenExpiryUtc = DateTime.UtcNow.AddHours(1)
            };
            var storage = new BaiduStorage(
                account,
                new BaiduAppConfig { AppFolderName = "backup" },
                new EmptyListTransport());

            Assert.Throws<CloudObjectNotFoundException>(delegate
            {
                storage.Stat("task/package.zip");
            });
        }

        [Test]
        public void BaiduMissingRemoteDirectory_IsObjectMissing()
        {
            var account = new CloudAccountDto
            {
                Provider = CloudProviders.Baidu,
                Enabled = true,
                RefreshToken = "refresh",
                AccessToken = "access",
                TokenExpiryUtc = DateTime.UtcNow.AddHours(1)
            };
            var storage = new BaiduStorage(
                account,
                new BaiduAppConfig { AppFolderName = "backup" },
                new ErrnoTransport(-9));

            Assert.Throws<CloudObjectNotFoundException>(delegate
            {
                storage.Stat("new-task/package.zip");
            });
        }

        private sealed class EmptyListTransport : IBaiduTransport
        {
            public string Call(string method, string url, string accessToken, byte[] body, string contentType)
            {
                return "{\"errno\":0,\"list\":[]}";
            }
        }

        private sealed class ErrnoTransport : IBaiduTransport
        {
            private readonly int _errno;

            public ErrnoTransport(int errno)
            {
                _errno = errno;
            }

            public string Call(string method, string url, string accessToken, byte[] body, string contentType)
            {
                return "{\"errno\":" + _errno + "}";
            }
        }
    }
}
