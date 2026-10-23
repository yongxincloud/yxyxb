using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using YxyXb.Core;
using YxyXb.Core.Runtime;
using YxyXb.Core.Security;
using YxyXb.Core.Store;

namespace YxyXb.Client
{
    internal static class UpdateCheck
    {
        private static int _gate;

        public static void Start(HostBootstrap boot)
        {
            if (boot == null)
            {
                return;
            }

            ThreadPool.QueueUserWorkItem(delegate
            {
                Thread.Sleep(120000);
                while (true)
                {
                    Try(boot);
                    Thread.Sleep(TimeSpan.FromHours(24));
                }
            });
        }

        private static void Try(HostBootstrap boot)
        {
            if (!System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
            {
                return;
            }

            if (Interlocked.Exchange(ref _gate, 1) == 1)
            {
                return;
            }

            try
            {
                if (TaskRunLock.AnyActive(boot.Db, DateTime.UtcNow))
                {
                    return;
                }

                var manifest = ReadManifest();
                if (manifest == null || !IsNewer(manifest.version, AppPaths.ReleaseVersion))
                {
                    return;
                }

                var hash = (manifest.sha256 ?? "").Trim();
                var url = (manifest.url ?? "").Trim();
                if (hash.Length != 64 || url.Length == 0)
                {
                    return;
                }

                var dir = Path.Combine(AppPaths.DataDirectory, "updates");
                Directory.CreateDirectory(dir);
                var zip = Path.Combine(dir, "pending.zip");
                Download(url, zip, 300000);
                if (!string.Equals(Sha256(zip), hash, StringComparison.OrdinalIgnoreCase))
                {
                    TryDelete(zip);
                    return;
                }

                var tempExe = StageUpdater(AppPaths.InstallDirectory);
                if (string.IsNullOrEmpty(tempExe))
                {
                    return;
                }

                var icon = OemSkin.Load().Icon ?? "";
                var start = new ProcessStartInfo
                {
                    FileName = tempExe,
                    WorkingDirectory = Path.GetDirectoryName(tempExe),
                    Arguments = Quote(zip) + " " + Quote(AppPaths.InstallDirectory) + " " + Quote(icon),
                    UseShellExecute = false
                };
                if (Process.Start(start) == null)
                {
                    return;
                }

                var app = System.Windows.Application.Current;
                if (app != null)
                {
                    app.Dispatcher.BeginInvoke(new Action(delegate { app.Shutdown(); }));
                }
            }
            catch (Exception)
            {
            }
            finally
            {
                Interlocked.Exchange(ref _gate, 0);
            }
        }

        private static string StageUpdater(string install)
        {
            var updater = Path.Combine(install, "YxyXb.Update.exe");
            if (!File.Exists(updater))
            {
                return null;
            }

            var tempDir = Path.Combine(Path.GetTempPath(), "yxyxb-update");
            Directory.CreateDirectory(tempDir);
            foreach (var file in Directory.GetFiles(install))
            {
                var ext = Path.GetExtension(file);
                if (!ext.Equals(".dll", StringComparison.OrdinalIgnoreCase)
                    && !ext.Equals(".exe", StringComparison.OrdinalIgnoreCase)
                    && !ext.Equals(".config", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                File.Copy(file, Path.Combine(tempDir, Path.GetFileName(file)), true);
            }

            CopyNative(install, tempDir, "x64");
            CopyNative(install, tempDir, "x86");
            var staged = Path.Combine(tempDir, "YxyXb.Update.exe");
            return File.Exists(staged) ? staged : null;
        }

        private static void CopyNative(string install, string tempDir, string folder)
        {
            var from = Path.Combine(install, folder);
            if (!Directory.Exists(from))
            {
                return;
            }

            var to = Path.Combine(tempDir, folder);
            Directory.CreateDirectory(to);
            foreach (var file in Directory.GetFiles(from))
            {
                File.Copy(file, Path.Combine(to, Path.GetFileName(file)), true);
            }
        }

        private static UpdateManifest ReadManifest()
        {
            TlsPolicy.Apply();
            var request = (HttpWebRequest)WebRequest.Create(AppPaths.UpdateManifestUrl);
            request.Method = "GET";
            request.Timeout = 20000;
            request.ReadWriteTimeout = 20000;
            request.KeepAlive = false;
            using (var response = (HttpWebResponse)request.GetResponse())
            using (var reader = new StreamReader(response.GetResponseStream() ?? Stream.Null, Encoding.UTF8))
            {
                return JsonUtil.FromJson<UpdateManifest>(reader.ReadToEnd());
            }
        }

        private static void Download(string url, string dest, int timeoutMs)
        {
            TlsPolicy.Apply();
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.Timeout = timeoutMs;
            request.ReadWriteTimeout = timeoutMs;
            request.KeepAlive = false;
            using (var response = (HttpWebResponse)request.GetResponse())
            using (var input = response.GetResponseStream() ?? Stream.Null)
            using (var output = File.Create(dest))
            {
                input.CopyTo(output);
            }
        }

        private static string Sha256(string path)
        {
            using (var sha = SHA256.Create())
            using (var input = File.OpenRead(path))
            {
                var hash = sha.ComputeHash(input);
                var sb = new StringBuilder(hash.Length * 2);
                for (var i = 0; i < hash.Length; i++)
                {
                    sb.Append(hash[i].ToString("x2"));
                }

                return sb.ToString();
            }
        }

        private static bool IsNewer(string remote, string local)
        {
            var left = Parts(remote);
            var right = Parts(local);
            if (left == null || right == null)
            {
                return false;
            }

            var count = left.Length > right.Length ? left.Length : right.Length;
            for (var i = 0; i < count; i++)
            {
                var a = i < left.Length ? left[i] : 0;
                var b = i < right.Length ? right[i] : 0;
                if (a > b)
                {
                    return true;
                }

                if (a < b)
                {
                    return false;
                }
            }

            return false;
        }

        private static int[] Parts(string version)
        {
            var text = (version ?? "").Trim();
            if (text.StartsWith("Ver ", StringComparison.OrdinalIgnoreCase))
            {
                text = text.Substring(4).Trim();
            }

            if (text.Length == 0)
            {
                return null;
            }

            var bits = text.Split('.');
            var numbers = new int[bits.Length];
            for (var i = 0; i < bits.Length; i++)
            {
                int value;
                if (!int.TryParse(bits[i], out value) || value < 0)
                {
                    return null;
                }

                numbers[i] = value;
            }

            return numbers;
        }

        private static string Quote(string text)
        {
            return "\"" + (text ?? "").Replace("\"", "") + "\"";
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (IOException)
            {
            }
        }

        private sealed class UpdateManifest
        {
            public string version { get; set; }
            public string url { get; set; }
            public string sha256 { get; set; }
        }
    }
}
