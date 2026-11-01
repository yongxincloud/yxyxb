using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using YxyXb.Contracts.Cloud;
using YxyXb.Contracts.Dto;
using YxyXb.Core.Data;
using YxyXb.Core.Store;
using YxyXb.Ctyun;

namespace YxyXb.Core.Tests
{
    public class CtyunStorageTests
    {
        [Test]
        public void Signer_MatchesV4Fixture()
        {
            Assert.That(CtyunSigner.Region("jiangsu-10.zos.ctyun.cn"), Is.EqualTo("jiangsu-10"));
            Assert.That(CtyunSigner.Region("https://guizhou-5.zos.ctyun.cn/path"), Is.EqualTo("guizhou-5"));
            var empty = CtyunSigner.Sha256Hex("");
            Assert.That(empty, Is.EqualTo("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"));
            var authorization = CtyunSigner.Authorization(
                "AKIDtest",
                "testsecret",
                "GET",
                "/backup/a.zip",
                new Dictionary<string, string>(),
                "jiangsu-10.zos.ctyun.cn",
                new DateTime(2022, 12, 28, 9, 56, 32, DateTimeKind.Utc),
                empty);
            Assert.That(authorization, Does.StartWith("AWS4-HMAC-SHA256 Credential=AKIDtest/20221228/jiangsu-10/s3/aws4_request, SignedHeaders=host;x-amz-content-sha256;x-amz-date, Signature="));
            Assert.That(authorization, Does.EndWith("90ddf75aa8c9a9fd400d698cc95b2845b503dbdd3ec15c6da08a752f7dff37ef"));
        }

        [Test]
        public void TestConnection_HeadBucket_Explains403And404()
        {
            var fake = new FakeTransport();
            var storage = Ready(fake);
            fake.Reply = new CtyunReply { Status = 200 };
            storage.TestConnection();
            Assert.That(fake.Last.Method, Is.EqualTo("HEAD"));
            Assert.That(fake.Last.Path, Is.EqualTo("/backup"));
            Assert.That(fake.Last.Url, Is.EqualTo("https://jiangsu-10.zos.ctyun.cn/backup"));
            Assert.That(fake.Last.Authorization, Does.StartWith("AWS4-HMAC-SHA256 Credential=ak/"));

            fake.Reply = new CtyunReply { Status = 403 };
            var denied = Assert.Throws<InvalidOperationException>(delegate { storage.TestConnection(); });
            Assert.That(denied.Message, Does.Contain("密钥"));
            Assert.That(denied.Message, Does.Contain("公网"));

            fake.Reply = new CtyunReply { Status = 404 };
            var missing = Assert.Throws<InvalidOperationException>(delegate { storage.TestConnection(); });
            Assert.That(missing.Message, Does.Contain("桶"));
            Assert.That(missing.Message, Does.Contain("Endpoint"));
        }

        [Test]
        public void Upload_SmallFileUsesPathStyleAndPrivateAcl()
        {
            var path = TempFile(32);
            var fake = new FakeTransport { Reply = new CtyunReply { Status = 200 } };
            var storage = Ready(fake);
            try
            {
                storage.Upload(path, "dir/a.zip");
                Assert.That(fake.Last.Method, Is.EqualTo("PUT"));
                Assert.That(fake.Last.Path, Is.EqualTo("/backup/dir/a.zip"));
                Assert.That(fake.Last.Url, Is.EqualTo("https://jiangsu-10.zos.ctyun.cn/backup/dir/a.zip"));
                Assert.That(fake.Last.Headers["x-amz-acl"], Is.EqualTo("private"));
                Assert.That(fake.Last.Headers.ContainsKey("x-amz-storage-class"), Is.False);
                Assert.That(string.IsNullOrEmpty(fake.Last.Headers["x-amz-content-sha256"]), Is.False);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public void Upload_MultipartCompletesAndAbortOnFailure()
        {
            var path = TempFile(ObjectSlices.MinPartBytes + 1);
            var fake = new FakeTransport();
            var storage = Ready(fake);
            try
            {
                fake.Handler = delegate(CtyunExchange call)
                {
                    if (call.Url.IndexOf("uploads", StringComparison.Ordinal) >= 0)
                    {
                        return new CtyunReply { Status = 200, Body = "<InitiateMultipartUploadResult><UploadId>UID</UploadId></InitiateMultipartUploadResult>" };
                    }

                    if (call.Method == "PUT")
                    {
                        return new CtyunReply { Status = 200, ETag = "\"p\"" };
                    }

                    return new CtyunReply { Status = 200 };
                };
                storage.Upload(path, "a.zip");
                Assert.That(Joined(fake), Does.Contain("uploads"));
                Assert.That(Joined(fake), Does.Contain("partNumber=1"));
                Assert.That(Joined(fake), Does.Contain("partNumber=2"));
                Assert.That(Joined(fake), Does.Contain("uploadId=UID"));
                Assert.That(fake.Methods, Does.Not.Contain("DELETE"));

                fake.Calls.Clear();
                fake.Methods.Clear();
                fake.Handler = delegate(CtyunExchange call)
                {
                    if (call.Url.IndexOf("uploads", StringComparison.Ordinal) >= 0)
                    {
                        return new CtyunReply { Status = 200, Body = "<UploadId>UID</UploadId>" };
                    }

                    if (call.Method == "PUT")
                    {
                        return new CtyunReply { Status = 500 };
                    }

                    return new CtyunReply { Status = 204 };
                };
                Assert.Throws<InvalidOperationException>(delegate { storage.Upload(path, "a.zip"); });
                Assert.That(fake.Methods, Does.Contain("DELETE"));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public void DeleteExpired_RemovesOldZipsAndStopsOnError()
        {
            var fake = new FakeTransport();
            var storage = Ready(fake);
            var page = 0;
            fake.Handler = delegate(CtyunExchange call)
            {
                if (call.Method == "GET")
                {
                    page++;
                    if (page == 1)
                    {
                        return new CtyunReply
                        {
                            Status = 200,
                            Body = "<ListBucketResult><IsTruncated>true</IsTruncated><NextMarker>m2</NextMarker>"
                                + "<Contents><Key>pre/old.zip</Key><LastModified>2020-01-01T00:00:00.000Z</LastModified></Contents>"
                                + "<Contents><Key>pre/new.zip</Key><LastModified>2030-01-01T00:00:00.000Z</LastModified></Contents>"
                                + "<Contents><Key>pre/note.txt</Key><LastModified>2020-01-01T00:00:00.000Z</LastModified></Contents>"
                                + "</ListBucketResult>"
                        };
                    }

                    return new CtyunReply
                    {
                        Status = 200,
                        Body = "<ListBucketResult><IsTruncated>false</IsTruncated>"
                            + "<Contents><Key>pre/db_20200102-030405.zip</Key></Contents>"
                            + "</ListBucketResult>"
                    };
                }

                if (call.Path.EndsWith("db_20200102-030405.zip", StringComparison.Ordinal))
                {
                    return new CtyunReply { Status = 500 };
                }

                return new CtyunReply { Status = 204 };
            };

            var deleted = new List<string>();
            Assert.Throws<InvalidOperationException>(delegate
            {
                storage.DeleteExpired("pre/", new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), deleted.Add);
            });
            Assert.That(deleted, Is.EqualTo(new[] { "pre/old.zip" }));
            Assert.That(page, Is.EqualTo(2));
        }

        [Test]
        public void EnabledProviders_IncludesReadyCtyun()
        {
            var file = Path.Combine(Path.GetTempPath(), "yxy-zos-" + Guid.NewGuid().ToString("N") + ".db");
            try
            {
                using (var db = new SqliteDb(file))
                {
                    CloudAccountStore.Save(db, new CloudAccountDto
                    {
                        Provider = CloudProviders.Ctyun,
                        Enabled = true,
                        Bucket = "backup",
                        AccessKey = "ak",
                        SecretKey = "sk",
                        Zone = "jiangsu-10.zos.ctyun.cn"
                    });
                    var ready = CloudAccountStore.EnabledProviders(db);
                    Assert.That(ready, Does.Contain(CloudProviders.Ctyun));
                }
            }
            finally
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
        }

        private static CtyunStorage Ready(FakeTransport fake)
        {
            var storage = new CtyunStorage(Account(), fake);
            storage.UtcNow = delegate { return new DateTime(2022, 12, 28, 9, 56, 32, DateTimeKind.Utc); };
            return storage;
        }

        private static CloudAccountDto Account()
        {
            return new CloudAccountDto
            {
                Provider = CloudProviders.Ctyun,
                Enabled = true,
                Bucket = "backup",
                AccessKey = "ak",
                SecretKey = "sk",
                Zone = "jiangsu-10.zos.ctyun.cn"
            };
        }

        private static string TempFile(long length)
        {
            var path = Path.Combine(Path.GetTempPath(), "yxy-zos-" + Guid.NewGuid().ToString("N") + ".bin");
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                stream.SetLength(length);
            }

            return path;
        }

        private static string Joined(FakeTransport fake)
        {
            var text = "";
            for (var i = 0; i < fake.Calls.Count; i++)
            {
                text += fake.Calls[i].Url + "\n" + fake.Calls[i].Path + "\n";
            }

            return text;
        }

        private sealed class FakeTransport : ICtyunTransport
        {
            public CtyunReply Reply = new CtyunReply { Status = 200 };
            public Func<CtyunExchange, CtyunReply> Handler;
            public CtyunExchange Last;
            public List<CtyunExchange> Calls = new List<CtyunExchange>();
            public List<string> Methods = new List<string>();

            public CtyunReply Send(CtyunExchange call)
            {
                Last = call;
                Calls.Add(call);
                Methods.Add(call.Method);
                if (Handler != null)
                {
                    return Handler(call);
                }

                return Reply;
            }
        }
    }
}
