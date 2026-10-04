using System.Collections.Generic;
using NUnit.Framework;
using Yxy.Contracts.Cloud;
using Yxy.Contracts.Dto;

namespace Yxy.Core.Tests
{
    [TestFixture]
    public sealed class BaiduContractTests
    {
        [Test]
        public void ProviderKey_IsBaidu()
        {
            Assert.That(CloudProviders.Baidu, Is.EqualTo("baidu"));
        }

        [Test]
        public void RemotePath_UsesAppsFolderAndObjectKey()
        {
            var key = CloudObjectKeys.ForZip("用心云备份", "file", "日报", @"D:\a\x.zip");
            var path = CloudObjectKeys.BaiduRemotePath("用心云备份", key);
            Assert.That(path, Is.EqualTo("/apps/用心云备份/" + key));
        }

        [Test]
        public void BaiduReady_NeedsEnabledAndRefreshToken()
        {
            var acc = new CloudAccountDto { Enabled = true };
            Assert.That(acc.IsBaiduReady, Is.False);
            acc.RefreshToken = "rt";
            Assert.That(acc.IsBaiduReady, Is.True);
            Assert.That(acc.IsReady, Is.False);
        }

        [Test]
        public void Choice_PicksSelectedWhenReady()
        {
            var ready = new List<string> { "qiniu", "baidu" };
            Assert.That(CloudChoice.Resolve(ready, true, "baidu"), Is.EqualTo("baidu"));
            Assert.That(CloudChoice.Resolve(ready, false, "baidu"), Is.Null);
            Assert.That(CloudChoice.Resolve(new List<string>(), true, "baidu"), Is.Null);
            Assert.That(CloudChoice.Resolve(ready, true, "tencent"), Is.EqualTo("qiniu"));
            Assert.That(CloudChoice.Resolve(new List<string> { "baidu" }, true, null), Is.EqualTo("baidu"));
        }
    }
}
