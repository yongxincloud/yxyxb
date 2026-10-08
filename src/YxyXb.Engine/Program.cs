using System;
using System.ServiceProcess;

namespace YxyXb.Engine
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            var console = HasFlag(args, "-console") || HasFlag(args, "--console") || Environment.UserInteractive && HasFlag(args, "-d");
            if (console)
            {
                var host = new EngineHost();
                Console.WriteLine("用心云·循备引擎（控制台模式）。按 Enter 停止。");
                host.Start();
                Console.ReadLine();
                host.Stop();
                return 0;
            }

            ServiceBase.Run(new ServiceBase[] { new YxyXbService() });
            return 0;
        }

        private static bool HasFlag(string[] args, string flag)
        {
            if (args == null)
            {
                return false;
            }

            for (var i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
