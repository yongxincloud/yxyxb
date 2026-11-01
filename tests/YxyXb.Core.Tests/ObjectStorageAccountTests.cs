using System;
using System.IO;
using NUnit.Framework;
using YxyXb.Contracts.Cloud;
using YxyXb.Contracts.Dto;
using YxyXb.Core.Data;
using YxyXb.Core.Store;

namespace YxyXb.Core.Tests
{
    public class ObjectStorageAccountTests
    {
        [Test]
        public void Host_KeepsOnlyHostname()
        {
            Assert.That(CloudEndpoints.Host("https://oss-cn-hangzhou.aliyuncs.com/bucket/key"), Is.EqualTo("oss-cn-hangzhou.aliyuncs.com"));
            Assert.That(CloudEndpoints.Host("  HTTPS://jiangsu-10.zos.ctyun.cn/path?x=1 "), Is.EqualTo("jiangsu-10.zos.ctyun.cn"));
            Assert.That(CloudEndpoints.Host("cos.ap-guangzhou.myqcloud.com"), Is.EqualTo("cos.ap-guangzhou.myqcloud.com"));
            Assert.That(CloudEndpoints.Host("  "), Is.EqualTo(""));
            Assert.That(CloudEndpoints.Host(null), Is.EqualTo(""));
        }

        [Test]
        public void IsObjectReady_RequiresBucketKeysAndHost()
        {
            var account = new CloudAccountDto
            {
                Provider = CloudProviders.Aliyun,
                Enabled = true,
                Bucket = "backup",
                AccessKey = "ak",
                SecretKey = "sk",
                Zone = "https://oss-cn-hangzhou.aliyuncs.com/extra"
            };
            Assert.That(account.IsObjectReady, Is.True);
            account.Zone = "";
            Assert.That(account.IsObjectReady, Is.False);
            account.Zone = "oss-cn-hangzhou.aliyuncs.com";
            account.Enabled = false;
            Assert.That(account.IsObjectReady, Is.False);
        }

        [Test]
        public void Label_UsesProductNames()
        {
            Assert.That(CloudChoice.Label(CloudProviders.Aliyun), Is.EqualTo("阿里云"));
            Assert.That(CloudChoice.Label(CloudProviders.Tencent), Is.EqualTo("腾讯云"));
            Assert.That(CloudChoice.Label(CloudProviders.Ctyun), Is.EqualTo("天翼云"));
            Assert.That(CloudChoice.Label(CloudProviders.Qiniu), Is.EqualTo("七牛云"));
        }

        [Test]
        public void Save_DoesNotTurnEmptyObjectZoneIntoQiniuDefault()
        {
            var file = Path.Combine(Path.GetTempPath(), "yxy-cloud-" + Guid.NewGuid().ToString("N") + ".db");
            try
            {
                using (var db = new SqliteDb(file))
                {
                    CloudAccountStore.Save(db, new CloudAccountDto
                    {
                        Provider = CloudProviders.Aliyun,
                        Enabled = true,
                        Bucket = "backup",
                        AccessKey = "ak",
                        SecretKey = "sk",
                        Zone = ""
                    });
                    var aliyun = CloudAccountStore.Get(db, CloudProviders.Aliyun);
                    Assert.That(aliyun.Zone, Is.EqualTo(""));
                    Assert.That(aliyun.IsObjectReady, Is.False);

                    CloudAccountStore.Save(db, new CloudAccountDto
                    {
                        Provider = CloudProviders.Qiniu,
                        Enabled = true,
                        Bucket = "space",
                        AccessKey = "qak",
                        SecretKey = "qsk",
                        Zone = ""
                    });
                    Assert.That(CloudAccountStore.Get(db, CloudProviders.Qiniu).Zone, Is.EqualTo("z0"));
                }
            }
            finally
            {
                TryDelete(file);
            }
        }

        [Test]
        public void Settings_RoundTripObjectPurgeIndependently()
        {
            var file = Path.Combine(Path.GetTempPath(), "yxy-purge-" + Guid.NewGuid().ToString("N") + ".db");
            try
            {
                using (var db = new SqliteDb(file))
                {
                    var settings = SettingsStore.Get(db);
                    settings.PurgeAliyun = true;
                    settings.PurgeTencent = false;
                    settings.PurgeCtyun = true;
                    settings.AliyunRetainDays = 0;
                    settings.TencentRetainDays = 3;
                    settings.CtyunRetainDays = 15;
                    settings.PurgeQiniu = false;
                    SettingsStore.Save(db, settings);
                    var again = SettingsStore.Get(db);
                    Assert.That(again.PurgeAliyun, Is.True);
                    Assert.That(again.PurgeTencent, Is.False);
                    Assert.That(again.PurgeCtyun, Is.True);
                    Assert.That(again.AliyunRetainDays, Is.EqualTo(10));
                    Assert.That(again.TencentRetainDays, Is.EqualTo(3));
                    Assert.That(again.CtyunRetainDays, Is.EqualTo(15));
                    Assert.That(again.PurgeQiniu, Is.False);
                }
            }
            finally
            {
                TryDelete(file);
            }
        }

        private static void TryDelete(string file)
        {
            try
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
            catch (IOException)
            {
            }
        }
    }
}
