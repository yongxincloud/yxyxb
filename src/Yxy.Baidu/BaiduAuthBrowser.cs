using System;
using System.Diagnostics;
using System.IO;

namespace Yxy.Baidu
{
    public sealed class BaiduAuthSession
    {
        public Process Process { get; set; }
        public string ProfileDir { get; set; }
    }

    public static class BaiduAuthBrowser
    {
        public static string AppModeArguments(string url)
        {
            return AppModeArguments(url, null);
        }

        public static string AppModeArguments(string url, string userDataDir)
        {
            var args = "--new-window --no-first-run --no-default-browser-check";
            if (!string.IsNullOrEmpty(userDataDir))
            {
                args += " --user-data-dir=\"" + userDataDir + "\"";
            }

            return args + " --app=\"" + (url ?? "") + "\"";
        }

        public static BaiduAuthSession Open(string url)
        {
            var exe = Find();
            var profile = Path.Combine(
                Path.GetTempPath(),
                "yxy-baidu-auth-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(profile);
            var session = new BaiduAuthSession { ProfileDir = profile };
            if (string.IsNullOrEmpty(exe))
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
                return session;
            }

            session.Process = Process.Start(new ProcessStartInfo
            {
                FileName = exe,
                Arguments = AppModeArguments(url, profile),
                UseShellExecute = false
            });
            return session;
        }

        public static void Close(BaiduAuthSession session)
        {
            if (session == null)
            {
                return;
            }

            CloseProcess(session.Process);
            TryDeleteProfile(session.ProfileDir);
        }

        /// <summary>兼容旧调用：仅关进程，不清理配置目录。</summary>
        public static void Close(Process process)
        {
            CloseProcess(process);
        }

        private static void CloseProcess(Process process)
        {
            if (process == null)
            {
                return;
            }

            try
            {
                if (process.HasExited)
                {
                    return;
                }

                process.CloseMainWindow();
                process.WaitForExit(2000);
                if (!process.HasExited)
                {
                    process.Kill();
                }
            }
            catch (InvalidOperationException)
            {
            }
            catch (System.ComponentModel.Win32Exception)
            {
            }
        }

        private static void TryDeleteProfile(string dir)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            {
                return;
            }

            try
            {
                Directory.Delete(dir, true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        internal static string Find()
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            var candidates = new[]
            {
                Path.Combine(pf86, @"Microsoft\Edge\Application\msedge.exe"),
                Path.Combine(pf, @"Microsoft\Edge\Application\msedge.exe"),
                Path.Combine(local, @"Microsoft\Edge\Application\msedge.exe"),
                Path.Combine(pf, @"Google\Chrome\Application\chrome.exe"),
                Path.Combine(pf86, @"Google\Chrome\Application\chrome.exe"),
                Path.Combine(local, @"Google\Chrome\Application\chrome.exe")
            };
            for (var i = 0; i < candidates.Length; i++)
            {
                if (File.Exists(candidates[i]))
                {
                    return candidates[i];
                }
            }

            return null;
        }
    }
}
