using System;
using System.Globalization;
using System.IO;

namespace YxyXb.Core
{
    public static class SiteGate
    {
        public const int GraceDays = 7;

        public static bool AllowsChannels()
        {
            return AllowsChannels(DateTime.UtcNow);
        }

        public static bool AllowsChannels(DateTime utcNow)
        {
            var email = "";
            var valid = false;
            var status = "";
            DateTime? end = null;
            try
            {
                var session = Path.Combine(AppPaths.DataDirectory, "site.session");
                if (File.Exists(session))
                {
                    var lines = File.ReadAllLines(session);
                    foreach (var line in lines)
                    {
                        var index = line.IndexOf('=');
                        if (index <= 0)
                        {
                            continue;
                        }

                        var key = line.Substring(0, index);
                        var value = line.Substring(index + 1);
                        if (key == "email") email = value;
                        else if (key == "valid") valid = value == "true";
                        else if (key == "status") status = value;
                        else if (key == "end")
                        {
                            DateTime parsed;
                            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out parsed))
                            {
                                end = parsed.ToUniversalTime();
                            }
                        }
                    }
                }
            }
            catch (IOException)
            {
            }

            var token = Path.Combine(AppPaths.DataDirectory, "site.token");
            var loggedIn = !string.IsNullOrEmpty(email) && File.Exists(token);
            try
            {
                if (loggedIn && new FileInfo(token).Length == 0)
                {
                    loggedIn = false;
                }
            }
            catch (IOException)
            {
                loggedIn = false;
            }

            if (string.Equals(status, "device_limit", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return Allows(loggedIn, valid, end, utcNow);
        }

        public static bool Allows(bool loggedIn, bool valid, DateTime? authEndUtc, DateTime utcNow)
        {
            if (!loggedIn || !valid || authEndUtc == null)
            {
                return false;
            }

            return utcNow <= authEndUtc.Value.AddDays(GraceDays);
        }
    }
}
