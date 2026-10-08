using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using YxyXb.Core;
using YxyXb.Core.Data;
using YxyXb.Core.Device;

namespace YxyXb.Client
{
    public sealed class SiteSession
    {
        public bool LoggedIn { get; set; }
        public string Email { get; set; }
        public string Name { get; set; }
        public string Nickname { get; set; }
        public string CompanyName { get; set; }
        public bool Valid { get; set; }
        public DateTime? AuthEndUtc { get; set; }
        public string AuthStatus { get; set; }
        public int NextCheckDays { get; set; }
        public string ProductNo { get; set; }
        public string ProductName { get; set; }
        public int? MaxDevices { get; set; }

        public bool Usable
        {
            get { return SiteGate.Allows(LoggedIn, Valid, AuthEndUtc, DateTime.UtcNow); }
        }

        public string EditionText
        {
            get
            {
                var name = (ProductName ?? "").Trim();
                if (name.Length == 0)
                {
                    name = EditionLabel(ProductNo);
                }

                if (name.Length == 0) return "";
                if (MaxDevices == null) return name;
                return name + "（最多 " + MaxDevices.Value + " 台）";
            }
        }

        public string DisplayName
        {
            get
            {
                var company = (CompanyName ?? "").Trim();
                var person = (Name ?? "").Trim();
                var nickname = (Nickname ?? "").Trim();
                if (company.Length == 0 && person.Length == 0 && nickname.Length == 0)
                {
                    return Email ?? "";
                }

                if (nickname.Length > 0)
                {
                    person = person.Length == 0 ? nickname : person + "（" + nickname + "）";
                }

                if (company.Length == 0) return person;
                if (person.Length == 0) return company;
                return company + " " + person;
            }
        }

        public string Summary
        {
            get
            {
                if (!LoggedIn)
                {
                    return "未登录。请先登录或申请帐号";
                }

                var who = DisplayName;
                var edition = EditionText;
                var editionSuffix = edition.Length == 0 ? "" : "，版本 " + edition;
                if (AuthEndUtc == null || !Valid)
                {
                    return "已登录 " + who + editionSuffix + "，当前没有可用授权。文件备份、数据库备份、云、UNC、FTP、邮件已停用。离线介质仍可使用。";
                }

                var local = AuthEndUtc.Value.ToLocalTime();
                var days = (int)Math.Ceiling((AuthEndUtc.Value - DateTime.UtcNow).TotalDays);
                if (days > 0)
                {
                    var until = "已登录 " + who + editionSuffix + "。可用至 " + local.ToString("yyyy-MM-dd");
                    if (days > 30) return until + "。";
                    return until + "，剩余 " + days + " 天。";
                }

                var grace = AuthEndUtc.Value.AddDays(7);
                if (DateTime.UtcNow <= grace)
                {
                    var left = (int)Math.Ceiling((grace - DateTime.UtcNow).TotalDays);
                    return "已登录 " + who + editionSuffix + "。授权已于 " + local.ToString("yyyy-MM-dd") + " 到期，宽限还剩 " + left + " 天。请续费。";
                }

                return "已登录 " + who + editionSuffix + "。授权已到期。文件备份、数据库备份、云、UNC、FTP、邮件已停用。离线介质仍可使用。";
            }
        }

        public static string EditionLabel(string productNo)
        {
            if (string.IsNullOrWhiteSpace(productNo)) return "";
            switch (productNo.Trim().ToUpperInvariant())
            {
                case "PERSONAL": return "个人版";
                case "STANDARD": return "标准版";
                case "PRO": return "专业版";
                default: return productNo.Trim();
            }
        }
    }

    public sealed class SiteDeviceItem
    {
        public string DeviceId { get; set; }
        public string DisplayName { get; set; }
        public string FirstSeenLocal { get; set; }
        public string LastSeenLocal { get; set; }
        public bool IsThisDevice { get; set; }
        public string ThisText
        {
            get { return IsThisDevice ? "是" : ""; }
        }
    }

    public sealed class SiteDeviceList
    {
        public int? MaxDevices { get; set; }
        public string ProductNo { get; set; }
        public string ProductName { get; set; }
        public List<SiteDeviceItem> Items { get; set; }

        public string EditionText
        {
            get
            {
                var name = (ProductName ?? "").Trim();
                if (name.Length == 0) name = SiteSession.EditionLabel(ProductNo);
                if (name.Length == 0) return "—";
                return name;
            }
        }
    }

    public static class SiteLogin
    {
        public static string WebsiteRoot
        {
            get { return SiteUrlFile.DefaultSite; }
        }

        public static string RegisterUrl
        {
            get { return WebsiteRoot + "/register"; }
        }

        public static string TokenFile
        {
            get { return Path.Combine(AppPaths.DataDirectory, "site.token"); }
        }

        public static string SessionFile
        {
            get { return Path.Combine(AppPaths.DataDirectory, "site.session"); }
        }

        public static SiteSession Load()
        {
            var session = new SiteSession();
            try
            {
                if (!File.Exists(SessionFile))
                {
                    return session;
                }

                var lines = File.ReadAllLines(SessionFile);
                foreach (var line in lines)
                {
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
                    else if (key == "maxDevices")
                    {
                        int max;
                        if (int.TryParse(value, out max)) session.MaxDevices = max;
                    }
                    else if (key == "days")
                    {
                        int days;
                        if (int.TryParse(value, out days)) session.NextCheckDays = days;
                    }
                    else if (key == "end")
                    {
                        DateTime end;
                        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out end))
                        {
                            session.AuthEndUtc = end.ToUniversalTime();
                        }
                    }
                }

                session.LoggedIn = File.Exists(TokenFile) && !string.IsNullOrEmpty(session.Email);
            }
            catch (IOException)
            {
            }

            return session;
        }

        public static void SignOut()
        {
            try
            {
                if (File.Exists(TokenFile)) File.Delete(TokenFile);
                if (File.Exists(SessionFile)) File.Delete(SessionFile);
            }
            catch (IOException)
            {
            }
        }

        public static string LocalDeviceId()
        {
            return DeviceId();
        }

        /// <summary>
        /// 启动时校验：本机设备身份是否被换机/拷贝打乱，以及当前设备是否仍在账号设备列表中。
        /// 返回 true 表示已注销，须强制重新登录。
        /// </summary>
        public static bool ReconcileDeviceSession(SqliteDb db)
        {
            bool resetBecauseCopied;
            DeviceIdStore.GetOrCreate(db, out resetBecauseCopied);
            if (resetBecauseCopied)
            {
                SignOut();
                return true;
            }

            var session = Load();
            if (!session.LoggedIn)
            {
                return false;
            }

            try
            {
                var list = ListDevices();
                var local = DeviceId();
                for (var i = 0; i < list.Items.Count; i++)
                {
                    if (string.Equals(list.Items[i].DeviceId, local, StringComparison.OrdinalIgnoreCase))
                    {
                        SoftVerify(session.Email);
                        return false;
                    }
                }

                SignOut();
                return true;
            }
            catch (Exception)
            {
                SignOut();
                return true;
            }
        }

        private static void SoftVerify(string email)
        {
            try
            {
                if (string.IsNullOrEmpty(email) || !File.Exists(TokenFile)) return;
                var token = File.ReadAllText(TokenFile).Trim();
                if (string.IsNullOrEmpty(token)) return;
                var body = Post("/api/client/verify",
                    "{\"token\":\"" + Escape(token)
                    + "\",\"deviceId\":\"" + Escape(DeviceId())
                    + "\",\"displayName\":\"" + Escape(DeviceDisplayName()) + "\"}");
                Save(email, body);
            }
            catch (Exception)
            {
            }
        }

        public static SiteDeviceList ListDevices()
        {
            var token = File.Exists(TokenFile) ? File.ReadAllText(TokenFile).Trim() : "";
            if (string.IsNullOrEmpty(token))
            {
                throw new InvalidOperationException("请先登录。");
            }

            var body = Post("/api/client/devices", "{\"token\":\"" + Escape(token) + "\"}");
            var maxText = Match(body, "\"maxDevices\"\\s*:\\s*(\\d+)");
            int? max = null;
            int parsed;
            if (int.TryParse(maxText, out parsed)) max = parsed;
            var productNo = Match(body, "\"productNo\"\\s*:\\s*\"([^\"]*)\"");
            var productName = Match(body, "\"productName\"\\s*:\\s*\"([^\"]*)\"");
            RememberEdition(productNo, productName, max);

            var localId = DeviceId();
            var items = new List<SiteDeviceItem>();
            foreach (Match block in Regex.Matches(body ?? "", "\\{[^{}]*\"deviceId\"\\s*:\\s*\"([^\"]+)\"[^{}]*\\}"))
            {
                var id = block.Groups[1].Value;
                var name = Match(block.Value, "\"displayName\"\\s*:\\s*\"([^\"]*)\"");
                items.Add(new SiteDeviceItem
                {
                    DeviceId = id,
                    DisplayName = string.IsNullOrEmpty(name) ? id : name,
                    FirstSeenLocal = FormatUtcLocal(Match(block.Value, "\"firstSeenAt\"\\s*:\\s*\"([^\"]*)\"")),
                    LastSeenLocal = FormatUtcLocal(Match(block.Value, "\"lastSeenAt\"\\s*:\\s*\"([^\"]*)\"")),
                    IsThisDevice = string.Equals(id, localId, StringComparison.OrdinalIgnoreCase)
                });
            }

            return new SiteDeviceList
            {
                MaxDevices = max,
                ProductNo = productNo,
                ProductName = productName,
                Items = items
            };
        }

        public static void KickDevice(string deviceId)
        {
            var token = File.Exists(TokenFile) ? File.ReadAllText(TokenFile).Trim() : "";
            if (string.IsNullOrEmpty(token))
            {
                throw new InvalidOperationException("请先登录。");
            }

            if (string.IsNullOrWhiteSpace(deviceId))
            {
                throw new InvalidOperationException("缺少设备标识");
            }

            Post("/api/client/devices/kick",
                "{\"token\":\"" + Escape(token) + "\",\"deviceId\":\"" + Escape(deviceId) + "\"}");
        }

        private static string FormatUtcLocal(string raw)
        {
            DateTime utc;
            if (string.IsNullOrEmpty(raw)
                || !DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out utc))
            {
                return raw ?? "";
            }

            if (utc.Kind == DateTimeKind.Unspecified) utc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
            return utc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        }

        public static void Prompt(Window owner, Action saved)
        {
            var dialog = new Window
            {
                Title = "登录",
                Owner = owner,
                Width = 440,
                Height = 460,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                Background = BrushOf("PageBg", "#F7F9FC"),
                FontFamily = new FontFamily("Microsoft YaHei")
            };
            var card = new Border
            {
                Background = BrushOf("CardBg", "#FFFFFF"),
                BorderBrush = BrushOf("CardLine", "#E5E7EB"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(20),
                Padding = new Thickness(20)
            };
            var panel = new StackPanel();
            panel.Children.Add(new TextBlock { Text = "网站账号登录", FontSize = 20, FontWeight = FontWeights.SemiBold });
            panel.Children.Add(new TextBlock
            {
                Text = "不保存密码，只保存登录令牌。",
                Foreground = BrushOf("Muted", "#6B7280"),
                Margin = new Thickness(0, 6, 0, 14)
            });
            var passwordMode = new RadioButton { Content = "密码登录", IsChecked = true, Margin = new Thickness(0, 0, 16, 8) };
            var codeMode = new RadioButton { Content = "验证码登录", Margin = new Thickness(0, 0, 0, 8) };
            var modes = new StackPanel { Orientation = Orientation.Horizontal };
            modes.Children.Add(passwordMode);
            modes.Children.Add(codeMode);
            panel.Children.Add(modes);
            panel.Children.Add(Label("邮箱"));
            var email = new TextBox { Height = 36, Margin = new Thickness(0, 4, 0, 10) };
            panel.Children.Add(email);
            var passwordLabel = Label("密码");
            panel.Children.Add(passwordLabel);
            var password = new PasswordBox { Height = 36, Margin = new Thickness(0, 4, 0, 10) };
            panel.Children.Add(password);
            var codeLabel = Label("验证码");
            codeLabel.Visibility = Visibility.Collapsed;
            panel.Children.Add(codeLabel);
            var code = new TextBox { Height = 36, Margin = new Thickness(0, 4, 0, 8), Visibility = Visibility.Collapsed };
            panel.Children.Add(code);
            var send = new Button { Content = "获取邮箱验证码", Height = 34, Margin = new Thickness(0, 0, 0, 10), Visibility = Visibility.Collapsed };
            panel.Children.Add(send);
            var error = new TextBlock { Foreground = Brushes.Firebrick, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) };
            panel.Children.Add(error);
            var login = new Button { Content = "登录", Height = 40, IsDefault = true, Background = BrushOf("Accent", "#0E7490"), Foreground = Brushes.White };
            panel.Children.Add(login);
            card.Child = panel;
            dialog.Content = card;

            passwordMode.Checked += delegate
            {
                passwordLabel.Visibility = Visibility.Visible;
                password.Visibility = Visibility.Visible;
                codeLabel.Visibility = Visibility.Collapsed;
                code.Visibility = Visibility.Collapsed;
                send.Visibility = Visibility.Collapsed;
            };
            codeMode.Checked += delegate
            {
                passwordLabel.Visibility = Visibility.Collapsed;
                password.Visibility = Visibility.Collapsed;
                codeLabel.Visibility = Visibility.Visible;
                code.Visibility = Visibility.Visible;
                send.Visibility = Visibility.Visible;
            };
            send.Click += delegate
            {
                try
                {
                    var filled = SendCode(email.Text);
                    if (!string.IsNullOrEmpty(filled)) code.Text = filled;
                    error.Text = string.IsNullOrEmpty(filled) ? "验证码已发送。" : "开发环境验证码已填入。";
                }
                catch (Exception ex)
                {
                    error.Text = ex.Message;
                }
            };
            login.Click += delegate { SubmitLogin(); };
            dialog.PreviewKeyDown += delegate(object sender, KeyEventArgs args)
            {
                if (args.Key != Key.Enter) return;
                args.Handled = true;
                SubmitLogin();
            };
            dialog.ShowDialog();

            void SubmitLogin()
            {
                try
                {
                    var useCode = codeMode.IsChecked == true;
                    Login(email.Text, useCode ? null : password.Password, useCode ? code.Text : null);
                    if (saved != null) saved();
                    dialog.Close();
                }
                catch (Exception ex)
                {
                    error.Text = ex.Message;
                }
            }
        }

        public static void Login(string email, string password, string code)
        {
            string path;
            string json;
            var deviceLabel = Escape(DeviceDisplayName());
            var deviceId = Escape(DeviceId());
            if (string.IsNullOrEmpty(code))
            {
                path = "/api/client/login";
                json = "{\"email\":\"" + Escape(email) + "\",\"password\":\"" + Escape(password)
                    + "\",\"deviceId\":\"" + deviceId + "\",\"displayName\":\"" + deviceLabel + "\"}";
            }
            else
            {
                path = "/api/client/login-code";
                json = "{\"email\":\"" + Escape(email) + "\",\"code\":\"" + Escape(code)
                    + "\",\"deviceId\":\"" + deviceId + "\",\"displayName\":\"" + deviceLabel + "\"}";
            }

            var body = Post(path, json);
            Save(email, body);
        }

        public static string SendCode(string email)
        {
            var json = "{\"email\":\"" + Escape(email) + "\",\"scene\":\"login\"}";
            var body = Post("/api/account/send-code", json);
            var dev = Regex.Match(body, "\"devCode\"\\s*:\\s*\"([^\"]+)\"");
            return dev.Success ? dev.Groups[1].Value : "";
        }

        private static string Post(string path, string json)
        {
            TlsPolicy.Apply();

            var urlFile = Path.Combine(AppPaths.DataDirectory, "site.url");
            var raw = File.Exists(urlFile) ? File.ReadAllText(urlFile) : "";
            var baseUrl = SiteUrlFile.Read(raw, SiteUrlFile.DefaultSite);
            var url = baseUrl.TrimEnd('/') + path;
            var bytes = Encoding.UTF8.GetBytes(json ?? "");
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "POST";
                request.ContentType = "application/json; charset=utf-8";
                request.Timeout = 30000;
                request.ReadWriteTimeout = 30000;
                request.KeepAlive = false;
                request.ContentLength = bytes.Length;
                using (var stream = request.GetRequestStream())
                {
                    stream.Write(bytes, 0, bytes.Length);
                }

                using (var response = (HttpWebResponse)request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream() ?? Stream.Null, Encoding.UTF8))
                {
                    return ReadLoginBody(response.StatusCode, reader.ReadToEnd());
                }
            }
            catch (WebException ex)
            {
                var http = ex.Response as HttpWebResponse;
                if (http != null)
                {
                    using (var reader = new StreamReader(http.GetResponseStream() ?? Stream.Null, Encoding.UTF8))
                    {
                        return ReadLoginBody(http.StatusCode, reader.ReadToEnd());
                    }
                }

                throw new InvalidOperationException("无法连接网站 " + baseUrl + "：" + ExceptionText.RootMessage(ex));
            }
            catch (Exception ex)
            {
                if (ex is InvalidOperationException)
                {
                    throw;
                }

                throw new InvalidOperationException("无法连接网站 " + baseUrl + "：" + ExceptionText.RootMessage(ex));
            }
        }

        private static string ReadLoginBody(HttpStatusCode status, string body)
        {
            var code = (int)status;
            if (code >= 200 && code < 300)
            {
                return body;
            }

            var err = Regex.Match(body ?? "", "\"error\"\\s*:\\s*\"([^\"]*)\"");
            var errorCode = Regex.Match(body ?? "", "\"errorCode\"\\s*:\\s*\"([^\"]*)\"");
            if (errorCode.Success && errorCode.Groups[1].Value == "DEVICE_LIMIT")
            {
                var msg = err.Success ? err.Groups[1].Value : "设备数已达上限";
                throw new InvalidOperationException(msg + "。请在网站「我的设备」踢出设备，或升级更高版本后再登录。");
            }

            throw new InvalidOperationException(err.Success ? err.Groups[1].Value : "登录失败");
        }

        private static void Save(string email, string body)
        {
            var token = Match(body, "\"token\"\\s*:\\s*\"([^\"]+)\"");
            if (string.IsNullOrEmpty(token))
            {
                throw new InvalidOperationException("登录失败");
            }

            AppPaths.EnsureDirectories();
            File.WriteAllText(TokenFile, token);
            var valid = Match(body, "\"valid\"\\s*:\\s*(true|false)");
            var end = Match(body, "\"authEndTime\"\\s*:\\s*\"([^\"]+)\"");
            var status = Match(body, "\"authStatus\"\\s*:\\s*\"([^\"]+)\"");
            var days = Match(body, "\"nextCheckDays\"\\s*:\\s*(\\d+)");
            var name = Match(body, "\"name\"\\s*:\\s*\"([^\"]*)\"");
            var nickname = Match(body, "\"nickname\"\\s*:\\s*\"([^\"]*)\"");
            var company = Match(body, "\"companyName\"\\s*:\\s*\"([^\"]*)\"");
            var productNo = Match(body, "\"productNo\"\\s*:\\s*\"([^\"]*)\"");
            var productName = Match(body, "\"productName\"\\s*:\\s*\"([^\"]*)\"");
            var maxDevices = Match(body, "\"maxDevices\"\\s*:\\s*(\\d+)");
            WriteSession(email, name, nickname, company, valid, end, status, days, productNo, productName, maxDevices);
        }

        private static void RememberEdition(string productNo, string productName, int? maxDevices)
        {
            try
            {
                if (!File.Exists(SessionFile)) return;
                var session = Load();
                if (!session.LoggedIn) return;
                WriteSession(
                    session.Email,
                    session.Name,
                    session.Nickname,
                    session.CompanyName,
                    session.Valid ? "true" : "false",
                    session.AuthEndUtc == null ? "" : session.AuthEndUtc.Value.ToString("o"),
                    session.AuthStatus,
                    session.NextCheckDays.ToString(),
                    productNo,
                    productName,
                    maxDevices == null ? "" : maxDevices.Value.ToString());
            }
            catch (IOException)
            {
            }
        }

        private static void WriteSession(
            string email,
            string name,
            string nickname,
            string company,
            string valid,
            string end,
            string status,
            string days,
            string productNo,
            string productName,
            string maxDevices)
        {
            AppPaths.EnsureDirectories();
            var text = "email=" + OneLine(email) + "\r\n"
                + "name=" + OneLine(name) + "\r\n"
                + "nickname=" + OneLine(nickname) + "\r\n"
                + "company=" + OneLine(company) + "\r\n"
                + "valid=" + OneLine(valid) + "\r\n"
                + "end=" + OneLine(end) + "\r\n"
                + "status=" + OneLine(status) + "\r\n"
                + "days=" + OneLine(days) + "\r\n"
                + "productNo=" + OneLine(productNo) + "\r\n"
                + "productName=" + OneLine(productName) + "\r\n"
                + "maxDevices=" + OneLine(maxDevices) + "\r\n";
            File.WriteAllText(SessionFile, text);
        }

        private static string OneLine(string value)
        {
            return (value ?? "").Replace("\r", "").Replace("\n", "").Trim();
        }

        private static string Match(string body, string pattern)
        {
            var found = Regex.Match(body ?? "", pattern);
            return found.Success ? found.Groups[1].Value : "";
        }

        private static string DeviceId()
        {
            try
            {
                if (!File.Exists(AppPaths.DeviceIdFile)) return "pc";
                var raw = File.ReadAllBytes(AppPaths.DeviceIdFile);
                using (var sha = SHA256.Create())
                {
                    var hash = sha.ComputeHash(raw);
                    var text = new StringBuilder(hash.Length * 2);
                    foreach (var item in hash) text.Append(item.ToString("x2"));
                    return text.ToString();
                }
            }
            catch (IOException)
            {
                return "pc";
            }
        }

        /// <summary>电脑名 · MAC · Windows 登录用户，便于门户辨认设备。</summary>
        public static string DeviceDisplayName()
        {
            var machine = Environment.MachineName ?? "";
            var user = Environment.UserName ?? "";
            var mac = PrimaryMacAddress();
            return (machine + " · " + mac + " · " + user).Trim(' ', '·');
        }

        static string PrimaryMacAddress()
        {
            try
            {
                var nic = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n =>
                        n.OperationalStatus == OperationalStatus.Up
                        && n.NetworkInterfaceType != NetworkInterfaceType.Loopback
                        && n.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                    .OrderByDescending(n => n.Speed)
                    .FirstOrDefault();
                if (nic == null) return "无MAC";
                var bytes = nic.GetPhysicalAddress().GetAddressBytes();
                if (bytes == null || bytes.Length == 0) return "无MAC";
                return string.Join("-", bytes.Select(b => b.ToString("X2")));
            }
            catch
            {
                return "无MAC";
            }
        }

        private static string Escape(string value)
        {
            var source = value ?? "";
            var text = new StringBuilder(source.Length);
            foreach (var ch in source)
            {
                if (ch == '\\' || ch == '"') text.Append('\\').Append(ch);
                else if (ch < 32) text.Append("\\u").Append(((int)ch).ToString("x4"));
                else text.Append(ch);
            }
            return text.ToString();
        }

        private static TextBlock Label(string text)
        {
            return new TextBlock { Text = text, FontSize = 13 };
        }

        private static Brush BrushOf(string key, string fallback)
        {
            if (Application.Current != null && Application.Current.Resources.Contains(key))
            {
                return (Brush)Application.Current.Resources[key];
            }

            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(fallback));
        }
    }
}
