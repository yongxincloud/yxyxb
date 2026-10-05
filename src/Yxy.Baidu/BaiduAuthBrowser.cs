using System;
using System.Diagnostics;
using System.IO;

namespace Yxy.Baidu
{
    public static class BaiduAuthBrowser
    {
        public static string AppModeArguments(string url)
        {
            return "--new-window --app=\"" + (url ?? "") + "\"";
        }

        public static Process Open(string url)
        {
            var exe = Find();
            if (string.IsNullOrEmpty(exe))
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
                return null;
            }

            return Process.Start(new ProcessStartInfo
            {
                FileName = exe,
                Arguments = AppModeArguments(url),
                UseShellExecute = false
            });
        }

        public static void Close(Process process)
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
            }
            catch (InvalidOperationException)
            {
            }
            catch (System.ComponentModel.Win32Exception)
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
