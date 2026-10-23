using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using YxyXb.Core.Service;

namespace YxyXb.Update
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args == null || args.Length < 2)
            {
                return 1;
            }

            var zip = args[0];
            var install = args[1];
            var icon = args.Length > 2 ? args[2] : "";
            if (!File.Exists(zip) || string.IsNullOrEmpty(install))
            {
                if (!string.IsNullOrEmpty(install))
                {
                    Note(install, "更新参数不正确，或找不到已下载的安装包。");
                }

                return 1;
            }

            Log(install, "开始替换程序文件。");

            try
            {
                if (!WaitProcesses(install, "YxyXb.Client"))
                {
                    Note(install, "客户端仍在运行，已停止本次替换。");
                    return 1;
                }

                var current = WindowsServiceControl.Query();
                if (current.Installed && current.Running)
                {
                    var stopped = WindowsServiceControl.Stop();
                    if (stopped.Running)
                    {
                        Note(install, string.IsNullOrEmpty(stopped.LastError) ? "引擎没有停下来。" : stopped.LastError);
                        StartClient(install);
                        return 1;
                    }
                }

                if (!WaitProcesses(install, "YxyXb.Engine"))
                {
                    Note(install, "引擎进程仍在运行，已停止本次替换。");
                    StartClient(install);
                    return 1;
                }

                Extract(zip, install, icon);
                WindowsServiceControl.Start();
                StartClient(install);
                TryDelete(zip);
                Log(install, "替换完成。");
                return 0;
            }
            catch (Exception ex)
            {
                Note(install, ex.Message);
                try
                {
                    WindowsServiceControl.Start();
                }
                catch (Exception)
                {
                }

                StartClient(install);
                return 1;
            }
        }

        private static bool WaitProcesses(string install, string processName)
        {
            var deadline = DateTime.UtcNow.AddSeconds(60);
            while (DateTime.UtcNow < deadline)
            {
                var list = Process.GetProcessesByName(processName);
                var busy = false;
                for (var i = 0; i < list.Length; i++)
                {
                    busy = true;
                    try
                    {
                        list[i].WaitForExit(1000);
                    }
                    catch (Exception)
                    {
                    }
                }

                if (!busy)
                {
                    System.Threading.Thread.Sleep(1000);
                    return true;
                }

                System.Threading.Thread.Sleep(500);
            }

            Log(install, processName + " 退出超时。");
            return false;
        }

        private static void Extract(string zipPath, string install, string icon)
        {
            var root = Path.GetFullPath(install).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            using (var zip = ZipFile.OpenRead(zipPath))
            {
                for (var i = 0; i < zip.Entries.Count; i++)
                {
                    var entry = zip.Entries[i];
                    if (entry == null || string.IsNullOrEmpty(entry.Name))
                    {
                        continue;
                    }

                    var name = (entry.FullName ?? "").Replace('\\', '/');
                    if (Skip(name, icon))
                    {
                        continue;
                    }

                    var dest = Path.GetFullPath(Path.Combine(install, name.Replace('/', Path.DirectorySeparatorChar)));
                    if (!dest.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if ((string.Equals(Path.GetFileName(name), "BaiduApp.json", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(name, "data/site.url", StringComparison.OrdinalIgnoreCase))
                        && File.Exists(dest))
                    {
                        continue;
                    }

                    var dir = Path.GetDirectoryName(dest);
                    if (!string.IsNullOrEmpty(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    WriteEntry(entry, dest);
                }
            }
        }

        private static void WriteEntry(ZipArchiveEntry entry, string dest)
        {
            for (var attempt = 0; attempt < 20; attempt++)
            {
                try
                {
                    entry.ExtractToFile(dest, true);
                    return;
                }
                catch (IOException)
                {
                    System.Threading.Thread.Sleep(500);
                }
                catch (UnauthorizedAccessException)
                {
                    System.Threading.Thread.Sleep(500);
                }
            }

            entry.ExtractToFile(dest, true);
        }

        private static bool Skip(string name, string icon)
        {
            if (string.Equals(name, "oem.json", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (name.StartsWith("data/", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(name, "data/site.url", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var file = Path.GetFileName(name);
            return !string.IsNullOrEmpty(icon)
                && string.Equals(file, icon.Trim(), StringComparison.OrdinalIgnoreCase)
                && name.IndexOf('/') < 0;
        }

        private static void StartClient(string install)
        {
            var exe = Path.Combine(install, "YxyXb.Client.exe");
            if (!File.Exists(exe))
            {
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = exe,
                WorkingDirectory = install,
                UseShellExecute = true
            });
        }

        private static void Log(string install, string message)
        {
            try
            {
                var dir = Path.Combine(install, "data", "updates");
                Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, "update-log.txt"), DateTime.Now.ToString("s") + " " + (message ?? "") + Environment.NewLine);
            }
            catch (Exception)
            {
            }
        }

        private static void Note(string install, string message)
        {
            try
            {
                var dir = Path.Combine(install, "data", "updates");
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "last-error.txt"), DateTime.Now.ToString("s") + " " + (message ?? ""));
            }
            catch (Exception)
            {
            }
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
    }
}
