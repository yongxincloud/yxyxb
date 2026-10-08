using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using YxyXb.Core;

namespace YxyXb.Engine
{
    public sealed class YxyXbService : ServiceBase
    {
        private const int ServiceRunning = 4;
        private const int AcceptStop = 0x1;
        private const int AcceptShutdown = 0x4;
        private const int ArmCommand = 128;

        private readonly object _gate = new object();
        private EngineHost _host;
        private System.Timers.Timer _timer;
        private DateTime _armUntilUtc = DateTime.MinValue;

        public YxyXbService()
        {
            ServiceName = AppPaths.ServiceName;
            CanStop = true;
            CanShutdown = true;
            AutoLog = true;
        }

        protected override void OnStart(string[] args)
        {
            _host = new EngineHost();
            _host.Start();
            TryArmRestart();
            _timer = new System.Timers.Timer(400);
            _timer.AutoReset = true;
            _timer.Elapsed += delegate
            {
                if (_timer != null && _timer.Interval < 1000)
                {
                    _timer.Interval = 10000;
                }

                Publish();
            };
            _timer.Start();
        }

        protected override void OnCustomCommand(int command)
        {
            if (command != ArmCommand)
            {
                return;
            }

            lock (_gate)
            {
                _armUntilUtc = DateTime.UtcNow.AddSeconds(60);
            }

            Publish();
        }

        protected override void OnStop()
        {
            StopHost();
        }

        protected override void OnShutdown()
        {
            StopHost();
        }

        private void StopHost()
        {
            if (_timer != null)
            {
                _timer.Stop();
                _timer.Dispose();
                _timer = null;
            }

            if (_host != null)
            {
                _host.Stop();
                _host = null;
            }
        }

        private bool Armed()
        {
            lock (_gate)
            {
                return DateTime.UtcNow < _armUntilUtc;
            }
        }

        private void Publish()
        {
            var handle = StatusHandle();
            if (handle == IntPtr.Zero)
            {
                return;
            }

            var acceptStop = Armed();
            var status = new ServiceStatus
            {
                serviceType = 0x10,
                currentState = ServiceRunning,
                controlsAccepted = AcceptShutdown | (acceptStop ? AcceptStop : 0),
                win32ExitCode = 0,
                serviceSpecificExitCode = 0,
                checkPoint = 0,
                waitHint = 0
            };
            Native.SetServiceStatus(handle, ref status);
        }

        private IntPtr StatusHandle()
        {
            var field = typeof(ServiceBase).GetField("statusHandle", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                return IntPtr.Zero;
            }

            var value = field.GetValue(this);
            return value is IntPtr ? (IntPtr)value : IntPtr.Zero;
        }

        private static void TryArmRestart()
        {
            RunSc("failure " + AppPaths.ServiceName + " reset= 86400 actions= restart/1000/restart/1000/restart/1000");
            RunSc("failureflag " + AppPaths.ServiceName + " 1");
        }

        private static void RunSc(string arguments)
        {
            try
            {
                var start = new ProcessStartInfo
                {
                    FileName = "sc.exe",
                    Arguments = arguments,
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                using (var process = Process.Start(start))
                {
                    if (process != null)
                    {
                        process.WaitForExit(5000);
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ServiceStatus
        {
            public int serviceType;
            public int currentState;
            public int controlsAccepted;
            public int win32ExitCode;
            public int serviceSpecificExitCode;
            public int checkPoint;
            public int waitHint;
        }

        private static class Native
        {
            [DllImport("advapi32.dll", SetLastError = true)]
            public static extern bool SetServiceStatus(IntPtr handle, ref ServiceStatus status);
        }
    }
}
