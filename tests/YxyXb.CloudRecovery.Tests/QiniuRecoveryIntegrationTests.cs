using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using NUnit.Framework;
using YxyXb.Contracts.Cloud;
using YxyXb.Core.Cloud;
using YxyXb.Qiniu;

namespace YxyXb.CloudRecovery.Tests
{
    public class QiniuRecoveryIntegrationTests
    {
        [Test]
        public void Upload_QiniuResponseLostAfterMkfile_RecoversByRemoteSize()
        {
            var portProbe = new TcpListener(IPAddress.Loopback, 0);
            portProbe.Start();
            var port = ((IPEndPoint)portProbe.LocalEndpoint).Port;
            portProbe.Stop();

            var prefix = "http://127.0.0.1:" + port + "/";
            var listener = new HttpListener();
            listener.Prefixes.Add(prefix);
            listener.Start();
            Exception serverError = null;
            var committed = false;
            var server = new Thread(delegate()
            {
                try
                {
                    for (var i = 0; i < 4; i++)
                    {
                        var context = listener.GetContext();
                        var path = context.Request.Url.AbsolutePath;
                        if (string.Equals(path, "/stat", StringComparison.Ordinal))
                        {
                            if (committed)
                            {
                                Reply(context, 200, "{\"fsize\":1,\"hash\":\"fixture\"}");
                            }
                            else
                            {
                                Reply(context, 612, "{\"error\":\"no such file\"}");
                            }

                            continue;
                        }

                        if (path.StartsWith("/mkblk/", StringComparison.Ordinal))
                        {
                            Reply(context, 200, "{\"ctx\":\"ctx-fixture\"}");
                            continue;
                        }

                        if (path.StartsWith("/mkfile/", StringComparison.Ordinal))
                        {
                            committed = true;
                            context.Response.Abort();
                            continue;
                        }

                        Reply(context, 404, "{\"error\":\"unexpected path\"}");
                    }
                }
                catch (Exception ex)
                {
                    serverError = ex;
                }
            });
            server.IsBackground = true;
            server.Start();

            var file = Path.Combine(Path.GetTempPath(), "qiniu-recovery-" + Guid.NewGuid().ToString("N") + ".zip");
            File.WriteAllBytes(file, new byte[1]);
            try
            {
                var storage = new QiniuStorageForTest(prefix);
                CloudUploadRecovery.Upload(storage, file, "task/package.zip");
                Assert.That(storage.UploadCalls, Is.EqualTo(1));
            }
            finally
            {
                File.Delete(file);
                listener.Stop();
                server.Join(3000);
            }

            Assert.That(serverError, Is.Null);
        }

        private static void Reply(HttpListenerContext context, int status, string body)
        {
            var bytes = Encoding.UTF8.GetBytes(body);
            context.Response.StatusCode = status;
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = bytes.Length;
            context.Response.OutputStream.Write(bytes, 0, bytes.Length);
            context.Response.OutputStream.Close();
        }

        private sealed class QiniuStorageForTest : ICloudStorage
        {
            private readonly string _prefix;

            public QiniuStorageForTest(string prefix)
            {
                _prefix = prefix;
            }

            public string ProviderKey
            {
                get { return CloudProviders.Qiniu; }
            }

            public bool IsImplemented
            {
                get { return true; }
            }

            public int UploadCalls { get; private set; }

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
                UploadCalls++;
                QiniuHttp.UploadFile(_prefix, "UpToken test:token", remoteKey, localPath, 0, null);
            }

            public CloudObjectInfo Stat(string remoteKey)
            {
                try
                {
                    QiniuHttp.RequestJson("GET", _prefix + "stat", null, null, null);
                }
                catch (InvalidOperationException ex)
                {
                    if (QiniuHttp.IsMissingObjectError(ex.Message))
                    {
                        throw new CloudObjectNotFoundException("missing", ex);
                    }

                    throw;
                }

                return new CloudObjectInfo { Size = 1, Checksum = "fixture", ChecksumKind = "qiniu-etag" };
            }

            public int DeleteExpired(string prefix, DateTime olderThanUtc, Action<string> onDeleted)
            {
                return 0;
            }
        }
    }
}
