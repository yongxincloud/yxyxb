using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using Yxy.Contracts.Dto;

namespace Yxy.Core.Transfer
{
    public static class FtpUtil
    {
        public static void Test(FtpSettingsDto settings)
        {
            var request = Create(settings, settings.RemoteDir, WebRequestMethods.Ftp.ListDirectory);
            using (var response = (FtpWebResponse)request.GetResponse())
            {
                if (response.StatusCode != FtpStatusCode.OpeningData && response.StatusCode != FtpStatusCode.DataAlreadyOpen && response.StatusCode != FtpStatusCode.ClosingData)
                {
                    if ((int)response.StatusCode >= 400)
                    {
                        throw new InvalidOperationException("FTP 测试失败：" + response.StatusDescription);
                    }
                }
            }
        }

        public static void UploadFile(FtpSettingsDto settings, string localFile, string remoteFileName)
        {
            UploadFile(settings, localFile, remoteFileName, 0);
        }

        public static void UploadFile(FtpSettingsDto settings, string localFile, string remoteFileName, int kbps)
        {
            if (settings == null || string.IsNullOrEmpty(settings.Host))
            {
                throw new InvalidOperationException("未配置 FTP。");
            }

            var dir = settings.RemoteDir ?? "";
            var remote = CombineRemote(dir, remoteFileName);
            EnsureDirectory(settings, dir);
            var request = Create(settings, remote, WebRequestMethods.Ftp.UploadFile);
            using (var input = File.OpenRead(localFile))
            using (var output = request.GetRequestStream())
            {
                StreamPace.Copy(input, output, kbps);
            }

            using (var response = (FtpWebResponse)request.GetResponse())
            {
                if ((int)response.StatusCode >= 400)
                {
                    throw new InvalidOperationException("FTP 上传失败：" + response.StatusDescription);
                }
            }
        }

        public static int DeleteOldZips(FtpSettingsDto settings, int days, DateTime utcNow)
        {
            if (settings == null || string.IsNullOrEmpty(settings.Host) || days < 1)
            {
                return 0;
            }

            var cutoff = utcNow.AddDays(-days);
            return DeleteOldZipsUnder(settings, settings.RemoteDir, cutoff, 0);
        }

        /// <summary>
        /// 递归删除过期 zip（与 DownloadTree 一致）；上传当前虽扁平，子目录内历史文件也能清掉。
        /// </summary>
        private static int DeleteOldZipsUnder(FtpSettingsDto settings, string remoteDir, DateTime cutoffUtc, int depth)
        {
            if (depth > 12)
            {
                return 0;
            }

            var names = ListNames(settings, remoteDir);
            var n = 0;
            for (var i = 0; i < names.Count; i++)
            {
                var name = names[i];
                if (name == "." || name == "..")
                {
                    continue;
                }

                var remote = CombineRemote(remoteDir, name);
                if (IsDirectory(settings, remote))
                {
                    n += DeleteOldZipsUnder(settings, remote, cutoffUtc, depth + 1);
                    continue;
                }

                if (!name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                DateTime stamp;
                try
                {
                    stamp = GetStamp(settings, remote);
                }
                catch (WebException)
                {
                    continue;
                }

                if (stamp.ToUniversalTime() >= cutoffUtc)
                {
                    continue;
                }

                try
                {
                    var request = Create(settings, remote, WebRequestMethods.Ftp.DeleteFile);
                    using (request.GetResponse())
                    {
                    }

                    n++;
                }
                catch (WebException)
                {
                }
            }

            return n;
        }

        private static DateTime GetStamp(FtpSettingsDto settings, string remote)
        {
            var request = Create(settings, remote, WebRequestMethods.Ftp.GetDateTimestamp);
            using (var response = (FtpWebResponse)request.GetResponse())
            {
                return response.LastModified;
            }
        }

        public static string DownloadTree(FtpSettingsDto settings, string remoteDir, string localRoot)
        {
            return DownloadTree(settings, remoteDir, localRoot, 0);
        }

        public static string DownloadTree(FtpSettingsDto settings, string remoteDir, string localRoot, int kbps)
        {
            Directory.CreateDirectory(localRoot);
            DownloadDir(settings, remoteDir, localRoot, kbps);
            return localRoot;
        }

        private static void DownloadDir(FtpSettingsDto settings, string remoteDir, string localDir, int kbps)
        {
            Directory.CreateDirectory(localDir);
            var names = ListNames(settings, remoteDir);
            for (var i = 0; i < names.Count; i++)
            {
                var name = names[i];
                if (name == "." || name == "..")
                {
                    continue;
                }

                var remote = CombineRemote(remoteDir, name);
                if (IsDirectory(settings, remote))
                {
                    DownloadDir(settings, remote, Path.Combine(localDir, name), kbps);
                }
                else
                {
                    var request = Create(settings, remote, WebRequestMethods.Ftp.DownloadFile);
                    using (var response = request.GetResponse())
                    using (var input = response.GetResponseStream())
                    using (var output = File.Create(Path.Combine(localDir, name)))
                    {
                        if (input != null)
                        {
                            StreamPace.Copy(input, output, kbps);
                        }
                    }
                }
            }
        }

        private static IList<string> ListNames(FtpSettingsDto settings, string remoteDir)
        {
            var request = Create(settings, remoteDir, WebRequestMethods.Ftp.ListDirectory);
            var list = new List<string>();
            using (var response = request.GetResponse())
            using (var reader = new StreamReader(response.GetResponseStream()))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    var name = line.Trim();
                    var slash = name.LastIndexOf('/');
                    if (slash >= 0)
                    {
                        name = name.Substring(slash + 1);
                    }

                    if (!string.IsNullOrEmpty(name))
                    {
                        list.Add(name);
                    }
                }
            }

            return list;
        }

        private static bool IsDirectory(FtpSettingsDto settings, string remote)
        {
            try
            {
                var request = Create(settings, remote, WebRequestMethods.Ftp.ListDirectory);
                using (request.GetResponse())
                {
                    return true;
                }
            }
            catch (WebException)
            {
                return false;
            }
        }

        private static void EnsureDirectory(FtpSettingsDto settings, string remoteDir)
        {
            if (string.IsNullOrEmpty(remoteDir) || remoteDir == "/")
            {
                return;
            }

            try
            {
                var request = Create(settings, remoteDir, WebRequestMethods.Ftp.MakeDirectory);
                using (request.GetResponse())
                {
                }
            }
            catch (WebException)
            {
            }
        }

        private static FtpWebRequest Create(FtpSettingsDto settings, string remotePath, string method)
        {
            var uri = BuildUri(settings, remotePath);
            var request = (FtpWebRequest)WebRequest.Create(uri);
            request.Method = method;
            request.UsePassive = settings.Passive;
            request.UseBinary = true;
            request.KeepAlive = false;
            request.Timeout = 30000;
            if (!string.IsNullOrEmpty(settings.User))
            {
                request.Credentials = new NetworkCredential(settings.User, settings.Password ?? "");
            }

            return request;
        }

        private static Uri BuildUri(FtpSettingsDto settings, string remotePath)
        {
            var port = settings.Port <= 0 ? 21 : settings.Port;
            var path = remotePath ?? "";
            if (!path.StartsWith("/", StringComparison.Ordinal))
            {
                path = "/" + path;
            }

            return new Uri("ftp://" + settings.Host + ":" + port + path);
        }

        private static string CombineRemote(string dir, string name)
        {
            var left = dir ?? "";
            if (left.EndsWith("/", StringComparison.Ordinal))
            {
                return left + name;
            }

            if (string.IsNullOrEmpty(left))
            {
                return name;
            }

            return left + "/" + name;
        }
    }
}
