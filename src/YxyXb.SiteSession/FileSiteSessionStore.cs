using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace YxyXb.SiteSession
{
    public sealed class FileSiteSessionStore : ISiteSessionStore
    {
        private readonly string _dataDirectory;
        private readonly string _tokenFile;
        private readonly string _sessionFile;

        public FileSiteSessionStore(string dataDirectory, string tokenFile, string sessionFile)
        {
            _dataDirectory = dataDirectory ?? "";
            _tokenFile = tokenFile ?? "";
            _sessionFile = sessionFile ?? "";
        }

        public SiteSessionState Load()
        {
            var session = new SiteSessionState();
            try
            {
                if (!File.Exists(_sessionFile))
                {
                    return session;
                }

                var lines = File.ReadAllLines(_sessionFile);
                for (var i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];
                    var index = line.IndexOf('=');
                    if (index <= 0)
                    {
                        continue;
                    }

                    var key = line.Substring(0, index);
                    var value = line.Substring(index + 1);
                    if (key == "email") session.Email = value;
                    else if (key == "name") session.Name = value;
                    else if (key == "nickname") session.Nickname = value;
                    else if (key == "company") session.CompanyName = value;
                    else if (key == "valid") session.Valid = value == "true";
                    else if (key == "status") session.AuthStatus = value;
                    else if (key == "productNo") session.ProductNo = value;
                    else if (key == "productName") session.ProductName = value;
                    else if (key == "maxDevices") session.MaxDevices = ParseInt(value);
                    else if (key == "days") session.NextCheckDays = ParseInt(value) ?? 0;
                    else if (key == "end")
                    {
                        DateTime end;
                        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out end))
                        {
                            session.AuthEndUtc = end.ToUniversalTime();
                        }
                    }
                }

                session.Token = LoadToken();
                session.LoggedIn = !string.IsNullOrEmpty(session.Token) && !string.IsNullOrEmpty(session.Email);
            }
            catch (IOException)
            {
            }

            return session;
        }

        public string LoadToken()
        {
            try
            {
                return File.Exists(_tokenFile) ? File.ReadAllText(_tokenFile).Trim() : "";
            }
            catch (IOException)
            {
                return "";
            }
        }

        public void Save(SiteSessionState session)
        {
            if (session == null || string.IsNullOrEmpty(session.Token))
            {
                throw new InvalidOperationException("登录失败");
            }

            EnsureDirectory();
            File.WriteAllText(_tokenFile, session.Token);
            File.WriteAllText(_sessionFile,
                "email=" + OneLine(session.Email) + "\r\n"
                + "name=" + OneLine(session.Name) + "\r\n"
                + "nickname=" + OneLine(session.Nickname) + "\r\n"
                + "company=" + OneLine(session.CompanyName) + "\r\n"
                + "valid=" + (session.Valid ? "true" : "false") + "\r\n"
                + "end=" + (session.AuthEndUtc.HasValue ? session.AuthEndUtc.Value.ToString("o") : "") + "\r\n"
                + "status=" + OneLine(session.AuthStatus) + "\r\n"
                + "days=" + session.NextCheckDays.ToString(CultureInfo.InvariantCulture) + "\r\n"
                + "productNo=" + OneLine(session.ProductNo) + "\r\n"
                + "productName=" + OneLine(session.ProductName) + "\r\n"
                + "maxDevices=" + (session.MaxDevices.HasValue ? session.MaxDevices.Value.ToString(CultureInfo.InvariantCulture) : "") + "\r\n");
        }

        public void Clear()
        {
            try
            {
                if (File.Exists(_tokenFile)) File.Delete(_tokenFile);
                if (File.Exists(_sessionFile)) File.Delete(_sessionFile);
            }
            catch (IOException)
            {
            }
        }

        private void EnsureDirectory()
        {
            if (!string.IsNullOrEmpty(_dataDirectory))
            {
                Directory.CreateDirectory(_dataDirectory);
            }
        }

        private static int? ParseInt(string value)
        {
            int parsed;
            return int.TryParse(value, out parsed) ? (int?)parsed : null;
        }

        private static string OneLine(string value)
        {
            return (value ?? "").Replace("\r", "").Replace("\n", "").Trim();
        }
    }
}
