using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using NUnit.Framework;
using YxyXb.Contracts.Cloud;
using YxyXb.Core.Cloud;

namespace YxyXb.Core.Tests
{
    public class ObjectUploadTests
    {
        [Test]
        public void SmallFile_PutsOnce()
        {
            var path = TempFile(100);
            var api = new FakeApi();
            var sleeps = new List<int>();
            try
            {
                ObjectUpload.Run(path, "a.zip", api, null, sleeps.Add);
                Assert.That(api.PutWholeCalls, Is.EqualTo(1));
                Assert.That(api.StartCalls, Is.EqualTo(0));
                Assert.That(sleeps, Is.Empty);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public void SmallFile_RetriesThenSucceeds()
        {
            var path = TempFile(100);
            var api = new FakeApi { FailPutWhole = 2 };
            var sleeps = new List<int>();
            try
            {
                ObjectUpload.Run(path, "a.zip", api, null, sleeps.Add);
                Assert.That(api.PutWholeCalls, Is.EqualTo(3));
                Assert.That(sleeps, Is.EqualTo(new[] { 400, 800 }));
                Assert.That(api.AbortCalls, Is.EqualTo(0));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public void SmallFile_StopsAfterThreeFailures()
        {
            var path = TempFile(100);
            var api = new FakeApi { FailPutWhole = 3 };
            var sleeps = new List<int>();
            try
            {
                var ex = Assert.Throws<InvalidOperationException>(delegate
                {
                    ObjectUpload.Run(path, "a.zip", api, null, sleeps.Add);
                });
                Assert.That(ex.Message, Is.EqualTo("整包失败"));
                Assert.That(api.PutWholeCalls, Is.EqualTo(3));
                Assert.That(sleeps, Is.EqualTo(new[] { 400, 800, 1200 }));
                Assert.That(api.AbortCalls, Is.EqualTo(0));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public void Multipart_CompletesPartsInOrder()
        {
            var path = TempFile(ObjectSlices.MinPartBytes + 1);
            var api = new FakeApi();
            try
            {
                ObjectUpload.Run(path, "dir/a.zip", api, null, delegate(int ms) { });
                Assert.That(api.StartCalls, Is.EqualTo(1));
                Assert.That(api.PartNumbers, Is.EquivalentTo(new[] { 1, 2 }));
                Assert.That(api.CompletedEtags, Is.EqualTo(new[] { "e1", "e2" }));
                Assert.That(api.AbortCalls, Is.EqualTo(0));
                Assert.That(api.PutWholeCalls, Is.EqualTo(0));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public void Multipart_AbortsWhenAPartFails()
        {
            var path = TempFile(ObjectSlices.MinPartBytes + 1);
            var api = new FakeApi { FailPartNumber = 1, FailPartTimes = 9 };
            var lines = new List<string>();
            try
            {
                Assert.Throws<InvalidOperationException>(delegate
                {
                    ObjectUpload.Run(path, "a.zip", api, lines.Add, delegate(int ms) { });
                });
                Assert.That(api.AbortCalls, Is.EqualTo(1));
                Assert.That(api.CompleteCalls, Is.EqualTo(0));
                Assert.That(string.Join("\n", lines.ToArray()), Does.Contain("取消未完成分片"));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public void Multipart_KeepsTwoPartsInFlight()
        {
            var path = TempFile(ObjectSlices.MinPartBytes * 3 + 1);
            var api = new FakeApi { PartDelayMs = 40 };
            try
            {
                ObjectUpload.Run(path, "a.zip", api, null, delegate(int ms) { });
                Assert.That(api.PartNumbers.Count, Is.EqualTo(4));
                Assert.That(api.MaxInFlight, Is.EqualTo(2));
                Assert.That(api.AbortCalls, Is.EqualTo(0));
            }
            finally
            {
                File.Delete(path);
            }
        }

        private static string TempFile(long length)
        {
            var path = Path.Combine(Path.GetTempPath(), "yxy-up-" + Guid.NewGuid().ToString("N") + ".bin");
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                stream.SetLength(length);
            }

            return path;
        }

        private sealed class FakeApi : IObjectApi
        {
            public int FailPutWhole;
            public int FailPartNumber;
            public int FailPartTimes;
            public int PartDelayMs;
            public int PutWholeCalls;
            public int StartCalls;
            public int CompleteCalls;
            public int AbortCalls;
            public int MaxInFlight;
            public List<int> PartNumbers = new List<int>();
            public string[] CompletedEtags;
            private int _inFlight;
            private int _partFails;
            private readonly object _gate = new object();

            public void PutWhole(string key, byte[] body)
            {
                lock (_gate)
                {
                    PutWholeCalls++;
                    if (PutWholeCalls <= FailPutWhole)
                    {
                        throw new InvalidOperationException("整包失败");
                    }
                }
            }

            public string Start(string key)
            {
                lock (_gate)
                {
                    StartCalls++;
                }

                return "up-1";
            }

            public string PutPart(string key, string uploadId, int number, byte[] body)
            {
                var entered = false;
                lock (_gate)
                {
                    _inFlight++;
                    if (_inFlight > MaxInFlight)
                    {
                        MaxInFlight = _inFlight;
                    }

                    entered = true;
                    if (number == FailPartNumber && _partFails < FailPartTimes)
                    {
                        _partFails++;
                        _inFlight--;
                        entered = false;
                        throw new InvalidOperationException("分片失败");
                    }
                }

                try
                {
                    if (PartDelayMs > 0)
                    {
                        Thread.Sleep(PartDelayMs);
                    }

                    lock (_gate)
                    {
                        PartNumbers.Add(number);
                    }

                    return "e" + number;
                }
                finally
                {
                    if (entered)
                    {
                        lock (_gate)
                        {
                            _inFlight--;
                        }
                    }
                }
            }

            public void Complete(string key, string uploadId, string[] etags)
            {
                lock (_gate)
                {
                    CompleteCalls++;
                    CompletedEtags = etags;
                }
            }

            public void Abort(string key, string uploadId)
            {
                lock (_gate)
                {
                    AbortCalls++;
                }
            }
        }
    }
}
