using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using YxyXb.Contracts.Cloud;
using YxyXb.Contracts.Dto;
using YxyXb.Core.Data;
using YxyXb.Core.Store;
using YxyXb.TencentCloud;

namespace YxyXb.Core.Tests
{
    public class TencentStorageTests
    {
        [Test]
        public void Signer_MatchesSha1Fixture()
        {
            var headers = new Dictionary<string, string>
            {
                { "Host", "examplebucket-1250000000.cos.ap-guangzhou.myqcloud.com" }
            };
            var http = TencentSigner.HttpString("PUT", "/exampleobject", null, headers);
            Assert.That(http, Is.EqualTo("put\n/exampleobject\n\nhost=examplebucket-1250000000.cos.ap-guangzhou.myqcloud.com\n"));
            Assert.That(
                TencentSigner.Authorization("AKIDtest", "testsecret", "PUT", "/exampleobject", null, headers, "1557989151;1557996351"),
                Is.EqualTo("q-sign-algorithm=sha1&q-ak=AKIDtest&q-sign-time=1557989151;1557996351&q-key-time=1557989151;1557996351&q-header-list=host&q-url-param-list=&q-signature=6ce9a7d28ef72c64ca250fb0b3e549db53978ee6"));
            var query = new Dictionary<string, string> { { "uploadId", "u" }, { "partNumber", "1" } };
            Assert.That(TencentSigner.HttpString("PUT", "/k", query, headers), Does.Contain("\npartnumber=1&uploadid=u\n"));
        }

        [Test]
        public void TestConnection_HeadBucket_Explains403And404()
        {
            var fake = new FakeTransport();
            var storage = Ready(fake);
            fake.Reply = new TencentReply { Status = 200 };
            storage.TestConnection();
            Assert.That(fake.Last.Method, Is.EqualTo("HEAD"));
            Assert.That(fake.Last.Path, Is.EqualTo("/"));
            Assert.That(fake.Last.Url, Is.EqualTo("https://backup-1250000000.cos.ap-guangzhou.myqcloud.com/"));
            Assert.That(fake.Last.Authorization, Does.StartWith("q-sign-algorithm=sha1&q-ak=ak&"));

            fake.Reply = new TencentReply { Status = 403 };
            var denied = Assert.Throws<InvalidOperationException>(delegate { storage.TestConnection(); });
            Assert.That(denied.Message, Does.Contain("密钥"));
            Assert.That(denied.Message, Does.Contain("公网"));

            fake.Reply = new TencentReply { Status = 404 };
            var missing = Assert.Throws<InvalidOperationException>(delegate { storage.TestConnection(); });
            Assert.That(missing.Message, Does.Contain("桶"));
            Assert.That(missing.Message, Does.Contain("公网"));
        }

        [Test]
        public void Stat_HeadObject_ReturnsLengthAndEtag()
        {
            var fake = new FakeTransport
            {
                Reply = new TencentReply { Status = 200, ContentLength = 32, ETag = "\"etag\"" }
            };
            var storage = Ready(fake);

            var info = storage.Stat("dir/a.zip");

            Assert.That(fake.Last.Method, Is.EqualTo("HEAD"));
            Assert.That(fake.Last.Path, Is.EqualTo("/dir/a.zip"));
            Assert.That(info.Size, Is.EqualTo(32));
            Assert.That(info.Checksum, Is.EqualTo("\"etag\""));
        }

        [Test]
        public void Upload_SmallFileSetsPrivateAclAndNoStorageClass()
        {
            var path = TempFile(32);
            var fake = new FakeTransport { Reply = new TencentReply { Status = 200 } };
            var storage = Ready(fake);
            try
            {
                storage.Upload(path, "dir/a.zip");
                Assert.That(fake.Last.Method, Is.EqualTo("PUT"));
                Assert.That(fake.Last.Path, Is.EqualTo("/dir/a.zip"));
                Assert.That(fake.Last.Url, Is.EqualTo("https://backup-1250000000.cos.ap-guangzhou.myqcloud.com/dir/a.zip"));
                Assert.That(fake.Last.Headers["x-cos-acl"], Is.EqualTo("private"));
                Assert.That(fake.Last.Headers.ContainsKey("x-cos-storage-class"), Is.False);
                Assert.That(string.IsNullOrEmpty(fake.Last.ContentMd5), Is.False);
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
                fake.Handler = delegate(TencentExchange call)
                {
                    if (call.Url.IndexOf("uploads", StringComparison.Ordinal) >= 0)
                    {
                        return new TencentReply { Status = 200, Body = "<InitiateMultipartUploadResult><UploadId>UID</UploadId></InitiateMultipartUploadResult>" };
                    }

                    if (call.Method == "PUT")
                    {
                        return new TencentReply { Status = 200, ETag = "\"p\"" };
                    }

                    return new TencentReply { Status = 200 };
                };
                storage.Upload(path, "a.zip");
                Assert.That(Joined(fake), Does.Contain("uploads"));
                Assert.That(Joined(fake), Does.Contain("partnumber=1"));
                Assert.That(Joined(fake), Does.Contain("partnumber=2"));
                Assert.That(Joined(fake), Does.Contain("uploadid=UID"));
                Assert.That(fake.Methods, Does.Not.Contain("DELETE"));

                fake.Calls.Clear();
                fake.Methods.Clear();
                fake.Handler = delegate(TencentExchange call)
                {
                    if (call.Url.IndexOf("uploads", StringComparison.Ordinal) >= 0)
                    {
                        return new TencentReply { Status = 200, Body = "<UploadId>UID</UploadId>" };
                    }

                    if (call.Method == "PUT")
                    {
                        return new TencentReply { Status = 500 };
                    }

                    return new TencentReply { Status = 204 };
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
            fake.Handler = delegate(TencentExchange call)
            {
                if (call.Method == "GET")
                {
                    page++;
                    if (page == 1)
                    {
                        return new TencentReply
                        {
                            Status = 200,
                            Body = "<ListBucketResult><IsTruncated>true</IsTruncated><NextMarker>m2</NextMarker>"
                                + "<Contents><Key>pre/old.zip</Key><LastModified>2020-01-01T00:00:00.000Z</LastModified></Contents>"
                                + "<Contents><Key>pre/new.zip</Key><LastModified>2030-01-01T00:00:00.000Z</LastModified></Contents>"
                                + "<Contents><Key>pre/note.txt</Key><LastModified>2020-01-01T00:00:00.000Z</LastModified></Contents>"
                                + "</ListBucketResult>"
                        };
                    }

                    return new TencentReply
                    {
                        Status = 200,
                        Body = "<ListBucketResult><IsTruncated>false</IsTruncated>"
                            + "<Contents><Key>pre/db_20200102-030405.zip</Key></Contents>"
                            + "</ListBucketResult>"
                    };
                }

                if (call.Path.EndsWith("db_20200102-030405.zip", StringComparison.Ordinal))
                {
                    return new TencentReply { Status = 500 };
                }

                return new TencentReply { Status = 204 };
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
        public void EnabledProviders_IncludesReadyTencent()
        {
            var file = Path.Combine(Path.GetTempPath(), "yxy-cos-" + Guid.NewGuid().ToString("N") + ".db");
            try
            {
                using (var db = new SqliteDb(file))
                {
                    CloudAccountStore.Save(db, new CloudAccountDto
                    {
                        Provider = CloudProviders.Tencent,
                        Enabled = true,
                        Bucket = "backup-1250000000",
                        AccessKey = "ak",
                        SecretKey = "sk",
                        Zone = "ap-guangzhou"
                    });
                    var ready = CloudAccountStore.EnabledProviders(db);
                    Assert.That(ready, Does.Contain(CloudProviders.Tencent));
                    Assert.That(ready, Does.Not.Contain(CloudProviders.Ctyun));
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

        private static TencentCloudStorage Ready(FakeTransport fake)
        {
            var storage = new TencentCloudStorage(Account(), fake);
            storage.UtcNow = delegate { return new DateTime(2022, 12, 28, 9, 56, 32, DateTimeKind.Utc); };
            return storage;
        }

        private static CloudAccountDto Account()
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

        private static string TempFile(long length)
        {
            var path = Path.Combine(Path.GetTempPath(), "yxy-cos-" + Guid.NewGuid().ToString("N") + ".bin");
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

        private sealed class FakeTransport : ITencentTransport
        {
            public TencentReply Reply = new TencentReply { Status = 200 };
            public Func<TencentExchange, TencentReply> Handler;
            public TencentExchange Last;
            public List<TencentExchange> Calls = new List<TencentExchange>();
            public List<string> Methods = new List<string>();

            public TencentReply Send(TencentExchange call)
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
