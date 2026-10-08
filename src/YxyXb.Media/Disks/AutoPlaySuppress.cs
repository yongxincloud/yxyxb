using System;
using System.Collections.Generic;
using Microsoft.Win32;

namespace YxyXb.Media.Disks
{
    internal sealed class AutoPlaySuppress : IDisposable
    {
        private const string PolicyKey = @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer";
        private const string HandlerKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\AutoplayHandlers";
        private const string NoRunName = "NoDriveTypeAutoRun";
        private const string DisableName = "DisableAutoplay";

        private readonly List<SavedValue> _saved = new List<SavedValue>();
        private bool _disposed;

        public static AutoPlaySuppress Hold()
        {
            var guard = new AutoPlaySuppress();
            guard.Apply();
            return guard;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            for (var i = 0; i < _saved.Count; i++)
            {
                Restore(_saved[i]);
            }
        }

        private void Apply()
        {
            Touch(RegistryHive.LocalMachine, null);
            try
            {
                using (var users = RegistryKey.OpenBaseKey(RegistryHive.Users, RegistryView.Registry64))
                {
                    var names = users.GetSubKeyNames();
                    for (var i = 0; i < names.Length; i++)
                    {
                        var name = names[i];
                        if (name.StartsWith("S-1-5-21-", StringComparison.Ordinal)
                            && name.IndexOf("_Classes", StringComparison.Ordinal) < 0)
                        {
                            Touch(RegistryHive.Users, name);
                        }
                    }
                }
            }
            catch (System.Security.SecurityException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private void Touch(RegistryHive hive, string sid)
        {
            Remember(hive, sid, PolicyKey, NoRunName, 255);
            Remember(hive, sid, HandlerKey, DisableName, 1);
        }

        private void Remember(RegistryHive hive, string sid, string keyPath, string valueName, int forced)
        {
            try
            {
                var full = string.IsNullOrEmpty(sid) ? keyPath : sid + "\\" + keyPath;
                using (var root = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64))
                using (var key = root.CreateSubKey(full))
                {
                    if (key == null)
                    {
                        return;
                    }

                    var current = key.GetValue(valueName);
                    var saved = new SavedValue
                    {
                        Hive = hive,
                        KeyPath = full,
                        Name = valueName,
                        Existed = current != null,
                        OldValue = current == null ? 0 : Convert.ToInt32(current)
                    };
                    var next = valueName == NoRunName ? (saved.OldValue | forced) : forced;
                    key.SetValue(valueName, next, RegistryValueKind.DWord);
                    _saved.Add(saved);
                }
            }
            catch (System.Security.SecurityException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (System.IO.IOException)
            {
            }
        }

        private static void Restore(SavedValue saved)
        {
            try
            {
                using (var root = RegistryKey.OpenBaseKey(saved.Hive, RegistryView.Registry64))
                using (var key = root.OpenSubKey(saved.KeyPath, true))
                {
                    if (key == null)
                    {
                        return;
                    }

                    if (!saved.Existed)
                    {
                        key.DeleteValue(saved.Name, false);
                        return;
                    }

                    key.SetValue(saved.Name, saved.OldValue, RegistryValueKind.DWord);
                }
            }
            catch (System.Security.SecurityException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (System.IO.IOException)
            {
            }
        }

        private sealed class SavedValue
        {
            public RegistryHive Hive;
            public string KeyPath;
            public string Name;
            public bool Existed;
            public int OldValue;
        }
    }
}
