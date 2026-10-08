using System;
using System.IO;
using NUnit.Framework;
using YxyXb.Contracts.Cloud;
using YxyXb.Contracts.Dto;
using YxyXb.Core.Backup;
using YxyXb.Core.Data;
using YxyXb.Core.Store;

namespace YxyXb.Core.Tests
{
    [TestFixture]
    public sealed class BaiduStoreTests
    {
        [Test]
        public void Save_RoundTripsBaiduTokenWithoutWipingQiniu()
        {
            var file = Path.Combine(Path.GetTempPath(), "yxy-baidu-" + Guid.NewGuid().ToString("N") + ".db");
            try
            {
                using (var db = new SqliteDb(file))
                {
                    CloudAccountStore.Save(db, new CloudAccountDto
                    {
                        Provider = CloudProviders.Qiniu,
                        Enabled = true,
                        Bucket = "b",
                        AccessKey = "ak",
                        SecretKey = "sk",
                        Zone = "z0"
                    });
                    CloudAccountStore.Save(db, new CloudAccountDto
                    {
                        Provider = CloudProviders.Baidu,
                        Enabled = true,
                        RefreshToken = "rt-1",
                        AccessToken = "at-1",
                        TokenExpiryUtc = new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Utc),
                        DisplayName = "张三"
                    });
                    var qiniu = CloudAccountStore.Get(db, CloudProviders.Qiniu);
                    var baidu = CloudAccountStore.Get(db, CloudProviders.Baidu);
                    Assert.That(qiniu.SecretKey, Is.EqualTo("sk"));
                    Assert.That(baidu.RefreshToken, Is.EqualTo("rt-1"));
                    Assert.That(baidu.DisplayName, Is.EqualTo("张三"));
                    Assert.That(CloudAccountStore.IsBaiduReady(db), Is.True);
                    var ready = CloudAccountStore.EnabledProviders(db);
                    Assert.That(ready, Does.Contain(CloudProviders.Qiniu));
                    Assert.That(ready, Does.Contain(CloudProviders.Baidu));
                    var settings = SettingsStore.Get(db);
                    Assert.That(settings.BaiduRetainDays, Is.EqualTo(7));
                    Assert.That(settings.PurgeBaidu, Is.False);
                    settings.PurgeBaidu = true;
                    settings.BaiduRetainDays = 3;
                    SettingsStore.Save(db, settings);
                    settings = SettingsStore.Get(db);
                    Assert.That(settings.PurgeBaidu, Is.True);
                    Assert.That(settings.BaiduRetainDays, Is.EqualTo(3));
                }
            }
            finally
            {
                try { File.Delete(file); } catch (IOException) { }
            }
        }

        [Test]
        public void HasAnyPurge_TrueWhenOnlyBaidu()
        {
            var s = new AppSettingsDto { PurgeBaidu = true };
            Assert.That(PurgeRunner.HasAnyPurge(s), Is.True);
            Assert.That(PurgeRunner.HasAnyPurge(new AppSettingsDto()), Is.False);
        }
    }
}
