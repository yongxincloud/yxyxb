using System;
using System.IO;

namespace Yxy.Core
{
    public static class AppPaths
    {
        public const string ProductName = "用心云备份";

        public const string ServiceName = "YxyBackupService";

        public const string ServiceDisplayName = "用心云备份 Service";

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

        public static string BakDirectory
        {
            get { return Path.Combine(InstallDirectory, "bak"); }
        }

        public static string DatabaseFile
        {
            get { return Path.Combine(DataDirectory, "yxy.db"); }
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
        }
    }
}
