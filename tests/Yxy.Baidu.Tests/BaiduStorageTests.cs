using System;
using System.IO;
using NUnit.Framework;
using Yxy.Contracts.Cloud;
using Yxy.Contracts.Dto;

namespace Yxy.Baidu.Tests
{
    [TestFixture]
    public sealed class BaiduStorageTests
    {
        [Test]
        public void Upload_SendsPrecreatePartsAndCreate()
        {
            var fake = new FakeBaiduTransport();
            var storage = Create(fake, DateTime.UtcNow.AddHours(1));
            var file = WriteTemp(new byte[] { 1, 2, 3, 4, 5 });
            try
            {
                storage.Upload(file, "用心云备份/file/日报/x.zip");
                Assert.That(Joined(fake), Does.Contain("method=precreate"));
                Assert.That(Joined(fake), Does.Contain("superfile2"));
                Assert.That(Joined(fake), Does.Contain("method=create"));
                Assert.That(Uri.UnescapeDataString(fake.Calls[0]), Does.Contain("/apps/用心云备份/"));
            }
            finally
            {
                File.Delete(file);
            }
        }

        [Test]
        public void Upload_RapidSkipParts()
        {
            var fake = new FakeBaiduTransport { RapidReturnType = 2 };
            var storage = Create(fake, DateTime.UtcNow.AddHours(1));
            var file = WriteTemp(new byte[] { 1, 2, 3, 4, 5 });
            try
            {
                storage.Upload(file, "用心云备份/file/日报/x.zip");
                Assert.That(Joined(fake), Does.Contain("method=precreate"));
                Assert.That(Joined(fake), Does.Not.Contain("superfile2"));
            }
            finally
            {
                File.Delete(file);
            }
        }

        [Test]
        public void DeleteExpired_OnlyOldUnderPrefix()
        {
            var now = DateTime.UtcNow;
            var oldUnix = (long)(now.AddDays(-10) - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
            var newUnix = (long)(now - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
            var fake = new FakeBaiduTransport
            {
                ListJson = "{\"errno\":0,\"list\":["
                    + "{\"path\":\"/apps/用心云备份/用心云备份/file/old.zip\",\"server_mtime\":" + oldUnix + "},"
                    + "{\"path\":\"/apps/用心云备份/用心云备份/file/new.zip\",\"server_mtime\":" + newUnix + "}"
                    + "]}"
            };
            var storage = Create(fake, now.AddHours(1));
            var deleted = new System.Collections.Generic.List<string>();
            var n = storage.DeleteExpired("用心云备份/", now.AddDays(-1), delegate(string key) { deleted.Add(key); });
            Assert.That(n, Is.EqualTo(1));
            Assert.That(deleted, Is.EqualTo(new[] { "/apps/用心云备份/用心云备份/file/old.zip" }));
            Assert.That(Joined(fake), Does.Contain("filemanager"));
            Assert.That(Joined(fake), Does.Not.Contain("new.zip"));
        }

        [Test]
        public void Upload_RefreshesExpiredToken()
        {
            CloudAccountDto saved = null;
            var fake = new FakeBaiduTransport();
            var acc = ReadyAccount(DateTime.UtcNow.AddMinutes(-1));
            var storage = new BaiduStorage(acc, Cfg(), fake);
            storage.PersistAccount = delegate(CloudAccountDto u) { saved = u; };
            var file = WriteTemp(new byte[] { 1 });
            try
            {
                storage.Upload(file, "k.zip");
                Assert.That(Joined(fake), Does.Contain("grant_type=refresh_token"));
                Assert.That(saved, Is.Not.Null);
                Assert.That(saved.AccessToken, Is.EqualTo("at-new"));
            }
            finally
            {
                File.Delete(file);
            }
        }

        [Test]
        public void Upload_WithoutRefresh_Throws()
        {
            var storage = new BaiduStorage(new CloudAccountDto { Enabled = true }, Cfg(), new FakeBaiduTransport());
            var ex = Assert.Throws<InvalidOperationException>(() => storage.Upload("x", "k"));
            Assert.That(ex.Message, Does.Contain("授权"));
        }

        [Test]
        public void Storage_SetParallel_Clamps()
        {
            var s = new BaiduStorage(ReadyAccount(DateTime.UtcNow.AddHours(1)), Cfg(), new FakeBaiduTransport());
            s.SetParallel(0);
            s.SetParallel(9);
        }

        private static BaiduStorage Create(FakeBaiduTransport fake, DateTime expiry)
        {
            return new BaiduStorage(ReadyAccount(expiry), Cfg(), fake);
        }

        private static CloudAccountDto ReadyAccount(DateTime expiry)
        {
            return new CloudAccountDto
            {
                Provider = CloudProviders.Baidu,
                Enabled = true,
                RefreshToken = "rt",
                AccessToken = "at",
                TokenExpiryUtc = expiry
            };
        }

        private static BaiduAppConfig Cfg()
        {
            return new BaiduAppConfig { AppKey = "k", AppSecret = "s", AppFolderName = "用心云备份" };
        }

        private static string WriteTemp(byte[] bytes)
        {
            var path = Path.Combine(Path.GetTempPath(), "yxy-up-" + Guid.NewGuid().ToString("N") + ".bin");
            File.WriteAllBytes(path, bytes);
            return path;
        }

        private static string Joined(FakeBaiduTransport fake)
        {
            return string.Join("\n", fake.Calls.ToArray());
        }
    }
}
