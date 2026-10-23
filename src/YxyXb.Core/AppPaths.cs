using System;
using System.IO;

namespace YxyXb.Core
{
    public static class AppPaths
    {
        public const string ProductDisplayName = "用心云·循备";

        public const string ProductPathName = "用心云循备";

        public const string ProductShortName = "循备";

        public const string ServiceName = "YxyXbService";

        public const string ServiceDisplayName = "用心云·循备 Service";

        public const string DatabaseFileName = "yxyxb.db";

        public const string ReleaseVersion = "1.0.0";

        public const string UpdateManifestUrl = "http://download.yongxinyun.com/yxyxb/version.json";

        private static string _rootOverride;

        public static string InstallDirectory
        {
            get
            {
                if (!string.IsNullOrEmpty(_rootOverride))
                {
                    return _rootOverride;
                }

                var baseDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (File.Exists(Path.Combine(baseDir, "YxyXb.Engine.exe")))
                {
                    return baseDir;
                }

                var sibling = Path.GetFullPath(Path.Combine(baseDir, "..", "dist"));
                if (File.Exists(Path.Combine(sibling, "YxyXb.Engine.exe")))
                {
                    return sibling;
                }

                return baseDir;
            }
        }

        public static void SetRootForTests(string path)
        {
            _rootOverride = path;
        }

        public static string DataDirectory
        {
            get { return Path.Combine(InstallDirectory, "data"); }
        }

        public static string BaiduUploadDirectory
        {
            get { return Path.Combine(DataDirectory, "baidu-upload"); }
        }

        public static string QiniuUploadDirectory
        {
            get { return Path.Combine(DataDirectory, "qiniu-upload"); }
        }

        public const string DefaultBakDirectory = @"D:\yxyxb\bak";

        public static string BakDirectory
        {
            get
            {
                if (!string.IsNullOrEmpty(_rootOverride))
                {
                    return Path.Combine(_rootOverride, "bak");
                }

                return DefaultBakDirectory;
            }
        }

        public static bool DirectoryExists(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            try
            {
                return Directory.Exists(path);
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        public static bool IsDefaultBak(string path)
        {
            var left = (path ?? "").Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var right = DefaultBakDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        public static string DisplayBakPath(string configured)
        {
            var path = (configured ?? "").Trim();
            if (path.Length == 0)
            {
                return DirectoryExists(DefaultBakDirectory) ? DefaultBakDirectory : "";
            }

            if (IsDefaultBak(path) && !DirectoryExists(path))
            {
                return "";
            }

            return path;
        }

        public static bool HasValidBakPath(string configured)
        {
            return DirectoryExists(DisplayBakPath(configured));
        }

        public static string BackupDirectoryOrThrow(string configured)
        {
            var path = DisplayBakPath(configured);
            if (!DirectoryExists(path))
            {
                throw new InvalidOperationException("未设置备份目录。请先在「设置 → 备份存储」选择一个已有磁盘上的文件夹。");
            }

            return path;
        }

        public static string DatabaseFile
        {
            get { return Path.Combine(DataDirectory, DatabaseFileName); }
        }

        public static string DeviceIdFile
        {
            get { return Path.Combine(DataDirectory, "device.id"); }
        }

        public static string EngineStatusFile
        {
            get { return Path.Combine(DataDirectory, "engine.status"); }
        }

        public static void EnsureDirectories()
        {
            Directory.CreateDirectory(DataDirectory);
            Directory.CreateDirectory(BaiduUploadDirectory);
            Directory.CreateDirectory(QiniuUploadDirectory);
        }
    }
}
