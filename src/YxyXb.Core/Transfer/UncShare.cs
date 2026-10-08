using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using YxyXb.Contracts.Dto;

namespace YxyXb.Core.Transfer
{
    public static class UncShare
    {
        private const int ResourceTypeDisk = 1;
        private const int ConnectUpdateProfile = 1;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NetResource
        {
            public int Scope;
            public int Type;
            public int DisplayType;
            public int Usage;
            public string LocalName;
            public string RemoteName;
            public string Comment;
            public string Provider;
        }

        [DllImport("mpr.dll", CharSet = CharSet.Unicode)]
        private static extern int WNetAddConnection2(ref NetResource netResource, string password, string username, int flags);

        [DllImport("mpr.dll", CharSet = CharSet.Unicode)]
        private static extern int WNetCancelConnection2(string name, int flags, bool force);

        public static void Test(UncSettingsDto settings)
        {
            if (settings == null || string.IsNullOrEmpty(settings.Path))
            {
                throw new InvalidOperationException("请填写网络共享路径。");
            }

            Connect(settings);
            if (!Directory.Exists(settings.Path) && !File.Exists(settings.Path))
            {
                throw new DirectoryNotFoundException("共享路径不可读：" + settings.Path);
            }
        }

        public static void CopyFile(UncSettingsDto settings, string localFile, string remoteFileName)
        {
            CopyFile(settings, localFile, remoteFileName, 0);
        }

        public static void CopyFile(UncSettingsDto settings, string localFile, string remoteFileName, int kbps)
        {
            if (settings == null || string.IsNullOrEmpty(settings.Path))
            {
                throw new InvalidOperationException("未配置网络传输路径。");
            }

            Connect(settings);
            Directory.CreateDirectory(settings.Path);
            var dest = Path.Combine(settings.Path, remoteFileName);
            using (var input = File.OpenRead(localFile))
            using (var output = File.Create(dest))
            {
                StreamPace.Copy(input, output, kbps);
            }
        }

        public static IDisposable ConnectScope(UncSettingsDto settings)
        {
            Connect(settings);
            return new CancelScope(RootOf(settings.Path));
        }

        private static void Connect(UncSettingsDto settings)
        {
            var root = RootOf(settings.Path);
            var resource = new NetResource
            {
                Type = ResourceTypeDisk,
                RemoteName = root
            };

            var user = UserName(settings);
            var code = WNetAddConnection2(ref resource, settings.Password, user, ConnectUpdateProfile);
            if (code != 0 && code != 1219)
            {
                throw new Win32Exception(code, "连接共享失败：" + root);
            }
        }

        public static int DeleteOldZips(UncSettingsDto settings, int days, DateTime utcNow)
        {
            if (settings == null || string.IsNullOrEmpty(settings.Path) || days < 1)
            {
                return 0;
            }

            Connect(settings);
            if (!Directory.Exists(settings.Path))
            {
                return 0;
            }

            var cutoff = utcNow.AddDays(-days);
            var n = 0;
            // 与本机一致：递归扫子目录，避免日后改成嵌套路径漏删
            string[] files;
            try
            {
                files = Directory.GetFiles(settings.Path, "*.zip", SearchOption.AllDirectories);
            }
            catch (IOException)
            {
                return 0;
            }

            for (var i = 0; i < files.Length; i++)
            {
                var path = files[i];
                if (path.IndexOf("_mssql_work", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                DateTime mtime;
                try
                {
                    mtime = File.GetLastWriteTimeUtc(path);
                }
                catch (IOException)
                {
                    continue;
                }

                if (mtime >= cutoff)
                {
                    continue;
                }

                try
                {
                    File.Delete(path);
                    n++;
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }

            return n;
        }

        private static string UserName(UncSettingsDto settings)
        {
            if (string.IsNullOrEmpty(settings.User))
            {
                return null;
            }

            if (string.IsNullOrEmpty(settings.Domain))
            {
                return settings.User;
            }

            return settings.Domain + "\\" + settings.User;
        }

        private static string RootOf(string path)
        {
            if (string.IsNullOrEmpty(path) || !path.StartsWith(@"\\", StringComparison.Ordinal))
            {
                return path;
            }

            var trim = path.TrimStart('\\');
            var slash = trim.IndexOf('\\');
            if (slash < 0)
            {
                return @"\\" + trim;
            }

            var rest = trim.Substring(slash + 1);
            var shareEnd = rest.IndexOf('\\');
            var share = shareEnd < 0 ? rest : rest.Substring(0, shareEnd);
            return @"\\" + trim.Substring(0, slash) + "\\" + share;
        }

        private sealed class CancelScope : IDisposable
        {
            private readonly string _root;

            public CancelScope(string root)
            {
                _root = root;
            }

            public void Dispose()
            {
                try
                {
                    WNetCancelConnection2(_root, 0, true);
                }
                catch (Win32Exception)
                {
                }
            }
        }
    }
}
