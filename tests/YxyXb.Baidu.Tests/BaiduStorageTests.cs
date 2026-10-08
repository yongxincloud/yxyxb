using System;
using System.IO;
using NUnit.Framework;
using YxyXb.Contracts.Cloud;
using YxyXb.Contracts.Dto;
using YxyXb.Core;

namespace YxyXb.Baidu.Tests
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
                storage.Upload(file, "用心云循备/file/日报/x.zip");
                Assert.That(Joined(fake), Does.Contain("method=precreate"));
                Assert.That(Joined(fake), Does.Contain("superfile2"));
                Assert.That(Joined(fake), Does.Contain("method=create"));
                Assert.That(Uri.UnescapeDataString(fake.Calls[0]), Does.Contain("/apps/用心云循备/"));
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
                storage.Upload(file, "用心云循备/file/日报/x.zip");
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
                    + "{\"path\":\"/apps/用心云循备/用心云循备/file/old.zip\",\"isdir\":0,\"server_mtime\":" + oldUnix + "},"
                    + "{\"path\":\"/apps/用心云循备/用心云循备/file/new.zip\",\"isdir\":0,\"server_mtime\":" + newUnix + "}"
                    + "]}"
            };
            var storage = Create(fake, now.AddHours(1));
            var deleted = new System.Collections.Generic.List<string>();
            var n = storage.DeleteExpired("用心云循备/", now.AddDays(-1), delegate(string key) { deleted.Add(key); });
            Assert.That(n, Is.EqualTo(1));
            Assert.That(deleted, Is.EqualTo(new[] { "/apps/用心云循备/用心云循备/file/old.zip" }));
            Assert.That(Joined(fake), Does.Contain("filemanager"));
            Assert.That(Joined(fake), Does.Not.Contain("new.zip"));
        }

        [Test]
        public void DeleteExpired_RecursesIntoSubdirs_DoesNotDeleteFolderByMtime()
        {
            var now = DateTime.UtcNow;
            var oldUnix = (long)(now.AddDays(-10) - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
            var newUnix = (long)(now - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
            var root = "/apps/用心云循备/用心云循备";
            var mssql = root + "/mssql";
            var task = mssql + "/服务站用户数据";
            var fake = new FakeBaiduTransport
            {
                ListJsonByDir = new System.Collections.Generic.Dictionary<string, string>
                {
                    {
                        root,
                        "{\"errno\":0,\"list\":[{\"path\":\"" + mssql + "\",\"isdir\":1,\"server_mtime\":" + oldUnix + "}]}"
                    },
                    {
                        mssql,
                        "{\"errno\":0,\"list\":[{\"path\":\"" + task + "\",\"isdir\":1,\"server_mtime\":" + newUnix + "}]}"
                    },
                    {
                        task,
                        "{\"errno\":0,\"list\":["
                            + "{\"path\":\"" + task + "/用心云循备_服务站_20260101-120000.zip\",\"isdir\":0,\"server_mtime\":" + oldUnix + "},"
                            + "{\"path\":\"" + task + "/用心云循备_服务站_20261008-063204.zip\",\"isdir\":0,\"server_mtime\":" + newUnix + "}"
                            + "]}"
                    }
                }
            };
            var storage = Create(fake, now.AddHours(1));
            var deleted = new System.Collections.Generic.List<string>();
            var n = storage.DeleteExpired("用心云循备/", now.AddDays(-2), delegate(string key) { deleted.Add(key); });
            Assert.That(n, Is.EqualTo(1));
            Assert.That(deleted.Count, Is.EqualTo(1));
            Assert.That(deleted[0], Does.Contain("20260101-120000.zip"));
            Assert.That(Joined(fake), Does.Contain("opera=delete"));
            Assert.That(Joined(fake), Does.Not.Contain("20261008-063204.zip"));
            var deleteBodies = string.Join("\n", fake.Calls.FindAll(delegate(string c)
            {
                return c.IndexOf("opera=delete", StringComparison.Ordinal) >= 0;
            }).ToArray());
            Assert.That(deleteBodies, Does.Contain(".zip"));
            Assert.That(deleteBodies, Does.Not.Contain("\"path\":\"" + mssql + "\""));
            Assert.That(deleteBodies, Does.Not.Contain("\"path\":\"" + task + "\""));
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
        public void Upload_WritesNamedSliceLogAndOnLog()
        {
            var root = Path.Combine(Path.GetTempPath(), "yxy-blog2-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            AppPaths.SetRootForTests(root);
            try
            {
                var notes = new System.Collections.Generic.List<string>();
                var fake = new FakeBaiduTransport();
                var storage = Create(fake, DateTime.UtcNow.AddHours(1));
                storage.OnLog = delegate(string line) { notes.Add(line); };
                var file = WriteTemp(new byte[] { 1, 2, 3, 4, 5 });
                storage.Upload(file, "k.zip");
                var recent = BaiduUploadLog.ReadRecent(AppPaths.DataDirectory, 8000);
                Assert.That(notes.Count, Is.GreaterThan(0));
                Assert.That(string.Join("\n", notes.ToArray()), Does.Contain("开始上传"));
                Assert.That(string.Join("\n", notes.ToArray()), Does.Contain("分片"));
                Assert.That(recent, Does.Contain(Path.GetFileName(file)));
                Assert.That(recent, Does.Contain("开始上传"));
                File.Delete(file);
            }
            finally
            {
                AppPaths.SetRootForTests(null);
                Directory.Delete(root, true);
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

        [Test]
        public void Upload_ResumesMissingPartsOnly()
        {
            var root = Path.Combine(Path.GetTempPath(), "yxy-br-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            AppPaths.SetRootForTests(root);
            try
            {
                var fake = new FakeBaiduTransport();
                var storage = Create(fake, DateTime.UtcNow.AddHours(1));
                storage.SetParallel(1);
                var bytes = new byte[BaiduSlices.BlockSize + 5];
                for (var i = 0; i < bytes.Length; i++)
                {
                    bytes[i] = 1;
                }

                var file = WriteTemp(bytes);
                var md5s = BaiduSlices.Md5HexList(file);
                var md5Arr = new string[md5s.Count];
                for (var i = 0; i < md5s.Count; i++)
                {
                    md5Arr[i] = md5s[i];
                }

                var full = Path.GetFullPath(file);
                var remote = CloudObjectKeys.BaiduRemotePath("用心云循备", "k.zip");
                BaiduUploadCheckpoint.Save(AppPaths.DataDirectory, new BaiduUploadCheckpoint
                {
                    LocalPath = full,
                    Size = bytes.Length,
                    BlockSize = BaiduSlices.BlockSize,
                    BlockMd5 = md5Arr,
                    RemotePath = remote,
                    UploadId = "resume-u",
                    DonePartSeq = new[] { 0 }
                });
                storage.Upload(file, "k.zip");
                var joined = Joined(fake);
                Assert.That(joined, Does.Not.Contain("method=precreate"));
                Assert.That(joined, Does.Contain("partseq=1"));
                Assert.That(joined, Does.Not.Contain("partseq=0"));
                Assert.That(joined, Does.Contain("method=create"));
                Assert.That(BaiduUploadCheckpoint.TryLoad(AppPaths.DataDirectory, full, bytes.Length, md5s), Is.Null);
                File.Delete(file);
            }
            finally
            {
                AppPaths.SetRootForTests(null);
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void Upload_BlocksCreateWhenPartMissing()
        {
            var root = Path.Combine(Path.GetTempPath(), "yxy-bg-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            AppPaths.SetRootForTests(root);
            try
            {
                var fake = new FakeBaiduTransport { FailPartSeq = 1 };
                var storage = Create(fake, DateTime.UtcNow.AddHours(1));
                storage.SetParallel(1);
                var bytes = new byte[BaiduSlices.BlockSize + 5];
                var file = WriteTemp(bytes);
                var full = Path.GetFullPath(file);
                var md5s = BaiduSlices.Md5HexList(file);
                Assert.Throws<InvalidOperationException>(() => storage.Upload(file, "k.zip"));
                var joined = Joined(fake);
                Assert.That(joined, Does.Not.Contain("method=create"));
                Assert.That(BaiduUploadCheckpoint.TryLoad(AppPaths.DataDirectory, full, bytes.Length, md5s), Is.Not.Null);
                File.Delete(file);
            }
            finally
            {
                AppPaths.SetRootForTests(null);
                Directory.Delete(root, true);
            }
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
            return new BaiduAppConfig { AppKey = "k", AppSecret = "s", AppFolderName = "用心云循备" };
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
