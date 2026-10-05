using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using Yxy.Baidu;

namespace Yxy.Baidu.Tests
{
    [TestFixture]
    public sealed class BaiduUnitTests
    {
        [Test]
        public void Slices_OneMbBoundaries()
        {
            var path = Path.Combine(Path.GetTempPath(), "yxy-slice-" + Guid.NewGuid().ToString("N") + ".bin");
            File.WriteAllBytes(path, new byte[BaiduSlices.BlockSize + 1]);
            try
            {
                var list = BaiduSlices.Md5HexList(path);
                Assert.That(BaiduSlices.BlockSize, Is.EqualTo(1024 * 1024));
                Assert.That(list.Count, Is.EqualTo(2));
                Assert.That(list[0].Length, Is.EqualTo(32));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public void Checkpoint_MatchesAndGates()
        {
            Assert.That(BaiduSlices.BlockSize, Is.EqualTo(1024 * 1024));
            Assert.That(BaiduUploadTimeout.Seconds(0, BaiduSlices.BlockSize), Is.EqualTo(600));
            Assert.That(BaiduUploadTimeout.Seconds(64, BaiduSlices.BlockSize), Is.GreaterThanOrEqualTo(120));

            var dir = Path.Combine(Path.GetTempPath(), "yxy-cp-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var md5 = new[] { "a", "b", "c" };
                var cp = new BaiduUploadCheckpoint
                {
                    LocalPath = @"D:\a.zip",
                    Size = 3L * BaiduSlices.BlockSize,
                    BlockSize = BaiduSlices.BlockSize,
                    BlockMd5 = md5,
                    RemotePath = "/apps/用心云备份/x.zip",
                    UploadId = "u1",
                    DonePartSeq = new[] { 0, 1 }
                };
                BaiduUploadCheckpoint.Save(dir, cp);
                var loaded = BaiduUploadCheckpoint.TryLoad(dir, @"D:\a.zip", cp.Size, md5);
                Assert.That(loaded, Is.Not.Null);
                Assert.That(loaded.UploadId, Is.EqualTo("u1"));
                Assert.That(loaded.IsComplete(3), Is.False);
                loaded.MarkDone(2);
                BaiduUploadCheckpoint.Save(dir, loaded);
                Assert.That(BaiduUploadCheckpoint.TryLoad(dir, @"D:\a.zip", cp.Size, md5).IsComplete(3), Is.True);
                Assert.That(BaiduUploadCheckpoint.TryLoad(dir, @"D:\a.zip", cp.Size + 1, md5), Is.Null);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Test]
        public void Config_MissingFile_Throws()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => BaiduAppConfig.Load(@"C:\no-such-baidu-app.json"));
            Assert.That(ex.Message, Is.EqualTo("未配置百度应用"));
        }

        [Test]
        public void Config_LoadUtf8FolderName()
        {
            var path = Path.Combine(Path.GetTempPath(), "yxy-baidu-app-" + Guid.NewGuid().ToString("N") + ".json");
            var json = "{\"AppKey\":\"k\",\"AppSecret\":\"s\",\"RedirectUri\":\"http://127.0.0.1:17890/baidu/\",\"AppFolderName\":\"用心云备份\"}";
            File.WriteAllText(path, json, new UTF8Encoding(false));
            try
            {
                var cfg = BaiduAppConfig.Load(path);
                Assert.That(cfg.AppFolderName, Is.EqualTo("用心云备份"));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public void OAuth_BuildsAuthorizeUrlAndParsesCallback()
        {
            var cfg = new BaiduAppConfig
            {
                AppKey = "ak",
                AppSecret = "sk",
                RedirectUri = "http://127.0.0.1:17890/baidu/",
                AppFolderName = "用心云备份"
            };
            var url = BaiduOAuth.AuthorizeUrl(cfg, "st-1");
            Assert.That(url, Does.Contain("client_id=ak"));
            Assert.That(url, Does.Contain("response_type=code"));
            Assert.That(url, Does.Contain("scope=basic,netdisk"));
            Assert.That(url, Does.Contain("qrcode=1"));
            string code;
            string error;
            Assert.That(BaiduOAuth.TryParseCallback("http://127.0.0.1:17890/baidu/?code=abc&state=st-1", "st-1", out code, out error), Is.True);
            Assert.That(code, Is.EqualTo("abc"));
            Assert.That(BaiduOAuth.TryParseCallback("http://127.0.0.1:17890/baidu/?code=abc&state=nope", "st-1", out code, out error), Is.False);
        }

        [Test]
        public void Factory_CreatesImplementedBaidu()
        {
            var storage = Yxy.Cloud.CloudStorageFactory.Create(Yxy.Contracts.Cloud.CloudProviders.Baidu);
            Assert.That(storage.IsImplemented, Is.True);
            Assert.That(storage.ProviderKey, Is.EqualTo("baidu"));
            var ex = Assert.Throws<InvalidOperationException>(() => storage.TestConnection());
            Assert.That(ex.Message, Does.Contain("授权"));
        }
    }
}
