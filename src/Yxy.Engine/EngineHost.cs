using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Yxy.Contracts.Dto;
using Yxy.Core;
using Yxy.Core.Backup;
using Yxy.Core.Runtime;
using Yxy.Cloud;
using Yxy.Core.Store;
using Yxy.Media;
using Yxy.Mssql;
using Yxy.MySql;

namespace Yxy.Engine
{
    public sealed class EngineHost
    {
        private readonly ManualResetEvent _stop = new ManualResetEvent(false);
        private Thread _thread;
        private HostBootstrap _boot;

        public void Start()
        {
            CloudModule.Register();
            _boot = HostBootstrap.Start();
            _thread = new Thread(Loop);
            _thread.IsBackground = true;
            _thread.Name = "YxyEngineLoop";
            _thread.Start();
        }

        public void Stop()
        {
            _stop.Set();
            if (_thread != null && !_thread.Join(8000))
            {
                try
                {
                    _thread.Abort();
                }
                catch (ThreadStateException)
                {
                }
            }

            if (_boot != null)
            {
                _boot.Db.Dispose();
                _boot = null;
            }
        }

        public void RunUntilStopped()
        {
            Start();
            _stop.WaitOne();
            Stop();
        }

        private void Loop()
        {
            while (!_stop.WaitOne(5000))
            {
                try
                {
                    Tick();
                }
                catch (Exception ex)
                {
                    WriteStatus("error " + ex.Message);
                }
            }
        }

        private void Tick()
        {
            var licensed = SiteGate.AllowsChannels();
            var ran = 0;
            if (licensed)
            {
                ran += FileBackupRunner.ExecuteDue(_boot.Db);
                ran += MssqlRunner.ExecuteDue(_boot.Db);
                ran += MysqlRunner.ExecuteDue(_boot.Db);
            }
            ran += MediaRunner.ExecuteDue(_boot.Db);
            if (PurgeRunner.ExecuteDue(_boot.Db))
            {
                ran++;
            }
            IList<ScheduledItemDto> upcoming;
            try
            {
                upcoming = TaskScheduleStore.GetUpcoming(_boot.Db, 20);
            }
            catch (Exception)
            {
                upcoming = new List<ScheduledItemDto>();
            }
            var next = "none";
            if (upcoming.Count > 0 && upcoming[0].NextRunUtc.HasValue)
            {
                next = upcoming[0].Kind + ":" + upcoming[0].Name + " @ " + upcoming[0].NextRunUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            }

            WriteStatus("ok ran=" + ran + " licensed=" + (licensed ? "1" : "0") + " queued=" + upcoming.Count + " next=" + next);
        }

        private static void WriteStatus(string line)
        {
            try
            {
                AppPaths.EnsureDirectories();
                File.WriteAllText(AppPaths.EngineStatusFile, line, Encoding.UTF8);
            }
            catch (IOException)
            {
            }
        }
    }
}
