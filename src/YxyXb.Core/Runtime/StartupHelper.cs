using System;
using Microsoft.Win32;

namespace YxyXb.Core.Runtime
{
    public static class StartupHelper
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "用心云·循备";

        public static void Apply(bool enabled, string exePath)
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RunKey, true) ?? Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (key == null)
                {
                    return;
                }

                if (enabled)
                {
                    key.SetValue(ValueName, "\"" + exePath + "\"");
                }
                else if (key.GetValue(ValueName) != null)
                {
                    key.DeleteValue(ValueName, false);
                }
            }
        }
    }
}
