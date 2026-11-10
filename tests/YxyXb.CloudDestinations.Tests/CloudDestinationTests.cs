using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using YxyXb.Contracts.Cloud;
using YxyXb.Core.Cloud;

namespace YxyXb.CloudDestinations.Tests
{
    public class CloudDestinationTests
    {
        private string _path;

        [SetUp]
        public void SetUp()
        {
            _path = Path.Combine(Path.GetTempPath(), "yxy-cloud-" + Guid.NewGuid().ToString("N"));
            File.WriteAllBytes(_path, new byte[12]);
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_path)) File.Delete(_path);
        }

        [Test]
        public void Upload_ReusesExistingMatchingRemoteObject()
        {
            var storage = new FakeStorage(delegate (int call)
            {
                return new CloudObjectInfo { Size = 12 };
            });

            CloudUploadRecovery.Upload(storage, _path, "software/task/package.zip");

            Assert.That(storage.UploadCount, Is.Zero);
            Assert.That(storage.StatCount, Is.EqualTo(1));
        }

        [Test]
        public void Upload_RecoversWhenUploadThrowsButRemoteObjectIsComplete()
        {
            var storage = new FakeStorage(delegate (int call)
            {
                if (call == 1) throw new CloudObjectNotFoundException("missing");
                return new CloudObjectInfo { Size = 12 };
            });
            storage.OnUpload = delegate
            {
                throw new InvalidOperationException("connection reset");
            };

            CloudUploadRecovery.Upload(storage, _path, "software/task/package.zip");

            Assert.That(storage.UploadCount, Is.EqualTo(1));
            Assert.That(storage.StatCount, Is.EqualTo(2));
        }

        [Test]
        public void Upload_VerifiesSuccessfulUploadAndRejectsDifferentSize()
        {
            var storage = new FakeStorage(delegate (int call)
            {
                if (call == 1) throw new CloudObjectNotFoundException("missing");
                return new CloudObjectInfo { Size = 11 };
            });

            var error = Assert.Throws<InvalidOperationException>(delegate
            {
                CloudUploadRecovery.Upload(storage, _path, "software/task/package.zip");
            });

            Assert.That(error.Message, Does.Contain("远端对象大小不一致"));
            Assert.That(storage.UploadCount, Is.EqualTo(1));
            Assert.That(storage.StatCount, Is.EqualTo(2));
        }

        [Test]
        public void Retention_DeletesOnlyItsPartsBeforeManifest_AndKeepsOtherUnitsSeparate()
        {
            var old = DateTime.UtcNow.AddDays(-10);
            var catalog = new FakeCatalog
            {
                Entries =
                {
                    Entry("software/task/a.part01.zip", old),
                    Entry("software/task/a.part02.zip", old),
                    Entry("software/task/a.manifest.json", old),
                    Entry("software/task/b.part01.zip", old),
                    Entry("software/task/b.manifest.json", old)
                }
            };
            var batches = new List<string[]>();

            var result = CloudRetentionRunner.DeleteExpired(
                catalog,
                "software/",
                DateTime.UtcNow.AddDays(-5),
                delegate (IList<string> batch)
                {
                    batches.Add(new List<string>(batch).ToArray());
                },
                null);

            Assert.That(result.DeletedUnitCount, Is.EqualTo(2));
            Assert.That(result.DeletedCount, Is.EqualTo(5));
            Assert.That(batches[0], Is.EqualTo(new[]
            {
                "software/task/a.part01.zip",
                "software/task/a.part02.zip"
            }));
            Assert.That(batches[1], Is.EqualTo(new[] { "software/task/a.manifest.json" }));
            Assert.That(batches[2], Is.EqualTo(new[] { "software/task/b.part01.zip" }));
            Assert.That(batches[3], Is.EqualTo(new[] { "software/task/b.manifest.json" }));
        }

        [Test]
        public void Retention_KeepsWholeUnitWhenAnyPartIsRecent()
        {
            var old = DateTime.UtcNow.AddDays(-10);
            var catalog = new FakeCatalog
            {
                Entries =
                {
                    Entry("software/task/a.part01.zip", old),
                    Entry("software/task/a.part02.zip", DateTime.UtcNow),
                    Entry("software/task/a.manifest.json", old)
                }
            };
            var batches = new List<string[]>();

            var result = CloudRetentionRunner.DeleteExpired(
                catalog,
                "software/",
                DateTime.UtcNow.AddDays(-5),
                delegate (IList<string> batch)
                {
                    batches.Add(new List<string>(batch).ToArray());
                },
                null);

            Assert.That(result.DeletedCount, Is.Zero);
            Assert.That(batches, Is.Empty);
        }

        [Test]
        public void Retention_StopsBeforeManifestWhenPartDeleteFails()
        {
            var old = DateTime.UtcNow.AddDays(-10);
            var catalog = new FakeCatalog
            {
                Entries =
                {
                    Entry("software/task/a.part01.zip", old),
                    Entry("software/task/a.manifest.json", old)
                }
            };
            var batches = new List<string[]>();

            Assert.Throws<IOException>(delegate
            {
                CloudRetentionRunner.DeleteExpired(
                    catalog,
                    "software/",
                    DateTime.UtcNow.AddDays(-5),
                    delegate (IList<string> batch)
                    {
                        batches.Add(new List<string>(batch).ToArray());
                        throw new IOException("denied");
                    },
                    null);
            });

            Assert.That(batches.Count, Is.EqualTo(1));
            Assert.That(batches[0], Is.EqualTo(new[] { "software/task/a.part01.zip" }));
        }

        [Test]
        public void Retention_DeletesStandaloneZipButKeepsOrphanPart_AndNeverLeavesProductPrefix()
        {
            var old = DateTime.UtcNow.AddDays(-10);
            var catalog = new FakeCatalog
            {
                Entries =
                {
                    Entry("software/task/old.zip", old),
                    Entry("software/task/orphan.part01.zip", old),
                    Entry("another-product/secret.zip", old)
                }
            };
            var deleted = new List<string>();

            var result = CloudRetentionRunner.DeleteExpired(
                catalog,
                "software/",
                DateTime.UtcNow.AddDays(-5),
                delegate (IList<string> batch)
                {
                    deleted.AddRange(batch);
                },
                null);

            Assert.That(result.DeletedCount, Is.EqualTo(1));
            Assert.That(deleted, Is.EqualTo(new[] { "software/task/old.zip" }));
        }

        [Test]
        public void Retention_KeepsUnitWhenPartNumbersAreIncomplete()
        {
            var old = DateTime.UtcNow.AddDays(-10);
            var catalog = new FakeCatalog
            {
                Entries =
                {
                    Entry("software/task/a.part01.zip", old),
                    Entry("software/task/a.part03.zip", old),
                    Entry("software/task/a.manifest.json", old)
                }
            };
            var batches = new List<string[]>();

            var result = CloudRetentionRunner.DeleteExpired(
                catalog,
                "software/",
                DateTime.UtcNow.AddDays(-5),
                delegate (IList<string> batch)
                {
                    batches.Add(new List<string>(batch).ToArray());
                },
                null);

            Assert.That(result.DeletedCount, Is.Zero);
            Assert.That(batches, Is.Empty);
        }

        [Test]
        public void Retention_RejectsEmptyProductPrefix()
        {
            var catalog = new FakeCatalog();
            Assert.Throws<InvalidOperationException>(delegate
            {
                CloudRetentionRunner.DeleteExpired(
                    catalog,
                    "",
                    DateTime.UtcNow,
                    delegate (IList<string> batch) { },
                    null);
            });
            Assert.That(catalog.ListCount, Is.Zero);
        }

        private static CloudObjectEntry Entry(string key, DateTime modifiedUtc)
        {
            return new CloudObjectEntry { Key = key, ModifiedUtc = modifiedUtc };
        }

        private sealed class FakeCatalog : ICloudObjectCatalog
        {
            public List<CloudObjectEntry> Entries = new List<CloudObjectEntry>();
            public int ListCount;

            public IList<CloudObjectEntry> List(string prefix)
            {
                ListCount++;
                return Entries;
            }
        }

        private sealed class FakeStorage : ICloudStorage
        {
            private readonly Func<int, CloudObjectInfo> _stat;
            public Action OnUpload;

            public FakeStorage(Func<int, CloudObjectInfo> stat)
            {
                _stat = stat;
            }

            public string ProviderKey => CloudProviders.Qiniu;
            public bool IsImplemented => true;
            public Action<string> OnLog { get; set; }
            public int UploadCount { get; private set; }
            public int StatCount { get; private set; }

            public void TestConnection()
            {
            }

            public void SetSpeed(int minKbps, int maxKbps)
            {
            }

            public void SetParallel(int parallel)
            {
            }

            public void Upload(string localPath, string remoteKey)
            {
                UploadCount++;
                if (OnUpload != null) OnUpload();
            }

            public CloudObjectInfo Stat(string remoteKey)
            {
                StatCount++;
                return _stat(StatCount);
            }

            public int DeleteExpired(string prefix, DateTime olderThanUtc, Action<string> onDeleted)
            {
                return 0;
            }
        }
    }
}
