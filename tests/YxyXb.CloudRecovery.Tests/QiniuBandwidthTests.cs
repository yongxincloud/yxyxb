using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using NUnit.Framework;
using YxyXb.Qiniu;

namespace YxyXb.CloudRecovery.Tests
{
    public class QiniuBandwidthTests
    {
        [Test]
        public void Upload_PacesNetworkWrites_WhenLimitConfigured()
        {
            var portProbe = new TcpListener(IPAddress.Loopback, 0);
            portProbe.Start();
            var port = ((IPEndPoint)portProbe.LocalEndpoint).Port;
            portProbe.Stop();

            var prefix = "http://127.0.0.1:" + port + "/";
            var listener = new HttpListener();
            listener.Prefixes.Add(prefix);
            listener.Start();
            long receiveMs = -1;
            Exception serverError = null;
            var server = new Thread(delegate()
            {
                try
                {
                    var block = listener.GetContext();
                    var watch = Stopwatch.StartNew();
                    Drain(block.Request.InputStream);
                    watch.Stop();
                    receiveMs = watch.ElapsedMilliseconds;
                    Reply(block, 200, "{\"ctx\":\"ctx-fixture\"}");

                    var complete = listener.GetContext();
                    Drain(complete.Request.InputStream);
                    Reply(complete, 200, "{\"hash\":\"fixture\"}");
                }
                catch (Exception ex)
                {
                    serverError = ex;
                }
            });
            server.IsBackground = true;
            server.Start();

            var file = Path.Combine(Path.GetTempPath(), "qiniu-bandwidth-" + Guid.NewGuid().ToString("N") + ".zip");
            File.WriteAllBytes(file, new byte[4 * 1024 * 1024]);
            try
            {
                QiniuHttp.UploadFile(prefix, "UpToken test:token", "task/package.zip", file, 2048, null);
            }
            finally
            {
                File.Delete(file);
                listener.Stop();
                server.Join(3000);
            }

            Assert.That(serverError, Is.Null);
            Assert.That(receiveMs, Is.GreaterThanOrEqualTo(1500));
        }

        private static void Drain(Stream stream)
        {
            var buffer = new byte[64 * 1024];
            while (stream.Read(buffer, 0, buffer.Length) > 0)
            {
            }
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
    }
}
