using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using YxyXb.Aliyun;
using YxyXb.Contracts.Cloud;
using YxyXb.Contracts.Dto;
using YxyXb.Core.Cloud;
using YxyXb.Core.Data;
using YxyXb.Core.Store;

namespace YxyXb.Core.Tests
{
    public class AliyunStorageTests
    {
        [Test]
        public void Signer_MatchesV1Fixture()
        {
            var headers = new Dictionary<string, string>
            {
                { "X-OSS-Meta-Magic", " abracadabra " },
                { "x-oss-meta-author", "alice" }
            };
            var canonical = AliyunSigner.Canonical(
                "PUT",
                "",
                "",
                "Wed, 28 Dec 2022 09:56:32 GMT",
                headers,
                "/examplebucket/nelson");
            Assert.That(canonical, Is.EqualTo(
                "PUT\n\n\nWed, 28 Dec 2022 09:56:32 GMT\nx-oss-meta-author:alice\nx-oss-meta-magic:abracadabra\n/examplebucket/nelson"));
            Assert.That(
                AliyunSigner.Authorization("LTAITest", "yourAccessKeySecret", canonical),
                Is.EqualTo("OSS LTAITest:iYvN442Hou61qRkCt2Psz09s0f4="));
            Assert.That(
                AliyunSigner.Resource("b", "k", new Dictionary<string, string> { { "uploadId", "u" }, { "partNumber", "1" } }),
                Is.EqualTo("/b/k?partNumber=1&uploadId=u"));
        }

        [Test]
        public void TestConnection_HeadBucket_Explains403And404()
        {
            var fake = new FakeTransport();
            var storage = Ready(fake);
            fake.Reply = new AliyunReply { Status = 200 };
            storage.TestConnection();
            Assert.That(fake.Last.Method, Is.EqualTo("HEAD"));
            Assert.That(fake.Last.Resource, Is.EqualTo("/backup/"));
            Assert.That(fake.Last.Url, Is.EqualTo("https://backup.oss-cn-hangzhou.aliyuncs.com/"));
            Assert.That(fake.Last.Authorization, Does.StartWith("OSS ak:"));

            fake.Reply = new AliyunReply { Status = 403 };
            var denied = Assert.Throws<InvalidOperationException>(delegate { storage.TestConnection(); });
            Assert.That(denied.Message, Does.Contain("密钥"));
            Assert.That(denied.Message, Does.Contain("公网"));

            fake.Reply = new AliyunReply { Status = 404 };
            var missing = Assert.Throws<InvalidOperationException>(delegate { storage.TestConnection(); });
            Assert.That(missing.Message, Does.Contain("桶"));
            Assert.That(missing.Message, Does.Contain("公网"));
        }

        [Test]
        public void Stat_HeadObject_ReturnsLengthAndEtag()
        {
            var fake = new FakeTransport
            {
                Reply = new AliyunReply { Status = 200, ContentLength = 32, ETag = "\"etag\"" }
            };
            var storage = Ready(fake);

            var info = storage.Stat("dir/a.zip");

            Assert.That(fake.Last.Method, Is.EqualTo("HEAD"));
            Assert.That(fake.Last.Resource, Is.EqualTo("/backup/dir/a.zip"));
            Assert.That(info.Size, Is.EqualTo(32));
            Assert.That(info.Checksum, Is.EqualTo("\"etag\""));
        }

        [Test]
        public void Upload_SmallFileSetsPrivateAclAndNoStorageClass()
        {
            var path = TempFile(32);
            var fake = new FakeTransport { Reply = new AliyunReply { Status = 200 } };
            var storage = Ready(fake);
            try
            {
                storage.Upload(path, "dir/a.zip");
                Assert.That(fake.Last.Method, Is.EqualTo("PUT"));
                Assert.That(fake.Last.Resource, Is.EqualTo("/backup/dir/a.zip"));
                Assert.That(fake.Last.Url, Is.EqualTo("https://backup.oss-cn-hangzhou.aliyuncs.com/dir/a.zip"));
                Assert.That(fake.Last.OssHeaders["x-oss-object-acl"], Is.EqualTo("private"));
                Assert.That(fake.Last.OssHeaders.ContainsKey("x-oss-storage-class"), Is.False);
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
                fake.Handler = delegate(AliyunExchange call)
                {
                    if (call.Url.IndexOf("uploads", StringComparison.Ordinal) >= 0)
                    {
                        return new AliyunReply { Status = 200, Body = "<InitiateMultipartUploadResult><UploadId>UID</UploadId></InitiateMultipartUploadResult>" };
                    }

                    if (call.Method == "PUT")
                    {
                        return new AliyunReply { Status = 200, ETag = "\"p\"" };
                    }

                    return new AliyunReply { Status = 200 };
                };
                storage.Upload(path, "a.zip");
                Assert.That(fake.Methods, Does.Contain("POST"));
                Assert.That(Joined(fake), Does.Contain("uploads"));
                Assert.That(Joined(fake), Does.Contain("partNumber=1"));
                Assert.That(Joined(fake), Does.Contain("partNumber=2"));
                Assert.That(Joined(fake), Does.Contain("uploadId=UID"));
                Assert.That(fake.Methods, Does.Not.Contain("DELETE"));

                fake.Calls.Clear();
                fake.Methods.Clear();
                fake.Handler = delegate(AliyunExchange call)
                {
                    if (call.Url.IndexOf("uploads", StringComparison.Ordinal) >= 0)
                    {
                        return new AliyunReply { Status = 200, Body = "<UploadId>UID</UploadId>" };
                    }

                    if (call.Method == "PUT")
                    {
                        return new AliyunReply { Status = 500 };
                    }

                    return new AliyunReply { Status = 204 };
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
            fake.Handler = delegate(AliyunExchange call)
            {
                if (call.Method == "GET")
                {
                    page++;
                    if (page == 1)
                    {
                        return new AliyunReply
                        {
                            Status = 200,
                            Body = "<ListBucketResult><IsTruncated>true</IsTruncated><NextMarker>m2</NextMarker>"
                                + "<Contents><Key>pre/old.zip</Key><LastModified>2020-01-01T00:00:00.000Z</LastModified></Contents>"
                                + "<Contents><Key>pre/new.zip</Key><LastModified>2030-01-01T00:00:00.000Z</LastModified></Contents>"
                                + "<Contents><Key>pre/note.txt</Key><LastModified>2020-01-01T00:00:00.000Z</LastModified></Contents>"
                                + "</ListBucketResult>"
                        };
                    }

                    return new AliyunReply
                    {
                        Status = 200,
                        Body = "<ListBucketResult><IsTruncated>false</IsTruncated>"
                            + "<Contents><Key>pre/db_20200102-030405.zip</Key></Contents>"
                            + "</ListBucketResult>"
                    };
                }

                if (call.Resource.IndexOf("stamp", StringComparison.Ordinal) >= 0 || call.Resource.EndsWith("db_20200102-030405.zip", StringComparison.Ordinal))
                {
                    return new AliyunReply { Status = 500 };
                }

                return new AliyunReply { Status = 204 };
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
        public void EnabledProviders_IncludesReadyAliyun()
        {
            var file = Path.Combine(Path.GetTempPath(), "yxy-ali-" + Guid.NewGuid().ToString("N") + ".db");
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
                        Zone = "https://oss-cn-hangzhou.aliyuncs.com/x"
                    });
                    var ready = CloudAccountStore.EnabledProviders(db);
                    Assert.That(ready, Does.Contain(CloudProviders.Aliyun));
                    Assert.That(ready, Does.Not.Contain(CloudProviders.Tencent));
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

        private static AliyunStorage Ready(FakeTransport fake)
        {
            var storage = new AliyunStorage(Account(), fake);
            storage.UtcNow = delegate { return new DateTime(2022, 12, 28, 9, 56, 32, DateTimeKind.Utc); };
            return storage;
        }

        private static CloudAccountDto Account()
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

        private static string TempFile(long length)
        {
            var path = Path.Combine(Path.GetTempPath(), "yxy-ali-" + Guid.NewGuid().ToString("N") + ".bin");
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
                text += fake.Calls[i].Url + "\n" + fake.Calls[i].Resource + "\n";
            }

            return text;
        }

        private sealed class FakeTransport : IAliyunTransport
        {
            public AliyunReply Reply = new AliyunReply { Status = 200 };
            public Func<AliyunExchange, AliyunReply> Handler;
            public AliyunExchange Last;
            public List<AliyunExchange> Calls = new List<AliyunExchange>();
            public List<string> Methods = new List<string>();

            public AliyunReply Send(AliyunExchange call)
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
