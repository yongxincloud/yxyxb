using System;
using System.IO;

namespace Yxy.Core
{
    public static class AppPaths
    {
        public const string ProductDisplayName = "用心云·循备";

        public const string ProductPathName = "用心云循备";

        public const string ProductShortName = "循备";

        public const string ServiceName = "YxyBackupService";

        public const string ServiceDisplayName = "用心云·循备 Service";

        public const string DatabaseFileNameLegacy = "yxy.db";

        public const string DatabaseFileNameNew = "yxyxb.db";

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
                if (File.Exists(Path.Combine(baseDir, "Yxy.Engine.exe")))
                {
                    return baseDir;
                }

                var sibling = Path.GetFullPath(Path.Combine(baseDir, "..", "dist"));
                if (File.Exists(Path.Combine(sibling, "Yxy.Engine.exe")))
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

        public static string DatabaseFile
        {
            get
            {
                var legacy = Path.Combine(DataDirectory, DatabaseFileNameLegacy);
                if (File.Exists(legacy))
                {
                    return legacy;
                }

                return Path.Combine(DataDirectory, DatabaseFileNameNew);
            }
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
            Directory.CreateDirectory(BakDirectory);
            Directory.CreateDirectory(BaiduUploadDirectory);
        }
    }
}
