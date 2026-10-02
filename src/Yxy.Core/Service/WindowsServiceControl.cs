using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Threading;
using Yxy.Contracts.Dto;

namespace Yxy.Core.Service
{
    public static class WindowsServiceControl
    {
        public static EngineStatusDto Query()
        {
            try
            {
                using (var controller = new ServiceController(AppPaths.ServiceName))
                {
                    controller.Refresh();
                    var running = controller.Status == ServiceControllerStatus.Running;
                    return new EngineStatusDto
                    {
                        Installed = true,
                        Running = running,
                        DisplayText = running ? "运行中" : StatusText(controller.Status)
                    };
                }
            }
            catch (InvalidOperationException)
            {
                return NotInstalled();
            }
            catch (Win32Exception)
            {
                return NotInstalled();
            }
        }

        public static EngineStatusDto Install(string engineExePath)
        {
            if (string.IsNullOrEmpty(engineExePath) || !File.Exists(engineExePath))
            {
                return Fail("找不到引擎程序：" + engineExePath);
            }

            var existing = Query();
            if (existing.Installed)
            {
                return existing;
            }

            var binPath = "\"" + engineExePath + "\"";
            var args = "create " + AppPaths.ServiceName
                + " binPath= " + binPath
                + " start= auto DisplayName= \"" + AppPaths.ServiceDisplayName + "\"";
            var result = RunSc(args, true);
            if (!result.Success)
            {
                return Fail(result.Output);
            }

            RunSc("description " + AppPaths.ServiceName + " \"用心云备份本机任务引擎\"", true);
            return WaitUntil(delegate(EngineStatusDto s) { return s.Installed; }, 8);
        }

        public static EngineStatusDto Start()
        {
            var viaApi = TryChange(delegate(ServiceController c)
            {
                if (c.Status != ServiceControllerStatus.Running)
                {
                    c.Start();
                    c.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(20));
                }
            });
            if (viaApi != null && viaApi.Running)
            {
                return viaApi;
            }

            var result = RunSc("start " + AppPaths.ServiceName, true);
            if (!result.Success)
            {
                return Fail(result.Output);
            }

            return WaitUntil(delegate(EngineStatusDto s) { return s.Running; }, 15);
        }

        public static EngineStatusDto Stop()
        {
            var viaApi = TryChange(delegate(ServiceController c)
            {
                if (c.Status == ServiceControllerStatus.Stopped)
                {
                    return;
                }

                try
                {
                    c.ExecuteCommand(128);
                }
                catch (InvalidOperationException)
                {
                }
                catch (Win32Exception)
                {
                }

                c.Refresh();
                if (c.Status != ServiceControllerStatus.Stopped)
                {
                    c.Stop();
                    c.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(20));
                }
            });
            if (viaApi != null && viaApi.Installed && !viaApi.Running)
            {
                return viaApi;
            }

            var result = RunSc("stop " + AppPaths.ServiceName, true);
            if (!result.Success)
            {
                return Fail(result.Output);
            }

            return WaitUntil(delegate(EngineStatusDto s) { return s.Installed && !s.Running; }, 15);
        }

        public static EngineStatusDto Uninstall()
        {
            var current = Query();
            if (!current.Installed)
            {
                return current;
            }

            if (current.Running)
            {
                var stopped = Stop();
                if (stopped.Running)
                {
                    return Fail(string.IsNullOrEmpty(stopped.LastError) ? "卸载前停止服务失败。" : stopped.LastError);
                }
            }

            var result = RunSc("delete " + AppPaths.ServiceName, true);
            if (!result.Success)
            {
                return Fail(result.Output);
            }

            return WaitUntil(delegate(EngineStatusDto s) { return !s.Installed; }, 8);
        }

        private static EngineStatusDto TryChange(Action<ServiceController> action)
        {
            try
            {
                using (var controller = new ServiceController(AppPaths.ServiceName))
                {
                    controller.Refresh();
                    action(controller);
                    return Query();
                }
            }
            catch (InvalidOperationException)
            {
                return null;
            }
            catch (Win32Exception)
            {
                return null;
            }
            catch (System.ServiceProcess.TimeoutException)
            {
                return null;
            }
        }

        private static EngineStatusDto WaitUntil(Func<EngineStatusDto, bool> ok, int seconds)
        {
            EngineStatusDto last = Query();
            for (var i = 0; i < seconds * 2; i++)
            {
                last = Query();
                if (ok(last))
                {
                    return last;
                }

                System.Threading.Thread.Sleep(500);
            }

            return last;
        }

        private static EngineStatusDto NotInstalled()
        {
            return new EngineStatusDto
            {
                Installed = false,
                Running = false,
                DisplayText = "未安装"
            };
        }

        private static EngineStatusDto Fail(string error)
        {
            var status = Query();
            status.LastError = error;
            if (string.IsNullOrEmpty(status.DisplayText))
            {
                status.DisplayText = "失败";
            }

            return status;
        }

        private static string StatusText(ServiceControllerStatus status)
        {
            switch (status)
            {
                case ServiceControllerStatus.Stopped:
                    return "已停止";
                case ServiceControllerStatus.StartPending:
                    return "正在启动";
                case ServiceControllerStatus.StopPending:
                    return "正在停止";
                case ServiceControllerStatus.Running:
                    return "运行中";
                default:
                    return status.ToString();
            }
        }

        private sealed class ScResult
        {
            public bool Success;
            public string Output;
        }

        private static ScResult RunSc(string arguments, bool elevate)
        {
            var start = new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = arguments,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            if (elevate)
            {
                start.UseShellExecute = true;
                start.Verb = "runas";
            }
            else
            {
                start.UseShellExecute = false;
                start.RedirectStandardOutput = true;
                start.RedirectStandardError = true;
                start.CreateNoWindow = true;
            }

            try
            {
                using (var process = Process.Start(start))
                {
                    if (process == null)
                    {
                        return new ScResult { Success = false, Output = "无法启动 sc.exe" };
                    }

                    var output = string.Empty;
                    if (!elevate)
                    {
                        output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
                    }

                    process.WaitForExit(20000);
                    if (process.ExitCode == 0)
                    {
                        return new ScResult { Success = true, Output = output };
                    }

                    return new ScResult
                    {
                        Success = false,
                        Output = string.IsNullOrWhiteSpace(output)
                            ? "操作失败，请在 UAC 提示时选择是。"
                            : output.Trim()
                    };
                }
            }
            catch (Win32Exception ex)
            {
                var canceled = ex.NativeErrorCode == 1223;
                return new ScResult
                {
                    Success = false,
                    Output = canceled ? "已取消管理员授权。" : ex.Message
                };
            }
        }
    }
}
