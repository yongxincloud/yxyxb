using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using YxyXb.Core;
using YxyXb.Core.Data;
using YxyXb.Core.Device;
using YxyXb.SiteSession;
using YxyXb.Core.Store;

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

        public bool DeviceLimited
        {
            get { return string.Equals(AuthStatus, "device_limit", StringComparison.OrdinalIgnoreCase); }
        }

        public bool Usable
        {
            get { return !DeviceLimited && SiteGate.Allows(LoggedIn, Valid, AuthEndUtc, DateTime.UtcNow); }
        }

        public static string DeviceLimitPrompt(int? maxDevices)
        {
            var head = maxDevices == null
                ? "设备数已达上限"
                : "已达 " + maxDevices.Value + " 台设备上限，请踢出设备或升级";
            return head + "。请在「我的设备」踢出设备，或升级更高版本后再登录。";
        }

        public static string DeviceLimitPrompt(string serverError)
        {
            var head = string.IsNullOrWhiteSpace(serverError) ? "设备数已达上限" : serverError.Trim();
            return head + "。请在「我的设备」踢出设备，或升级更高版本后再登录。";
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

                if (DeviceLimited)
                {
                    return DeviceLimitPrompt(MaxDevices) + "文件备份、数据库备份、云、UNC、FTP、邮件已停用。离线介质仍可使用。";
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
                case "CUSTOM": return "定制版";
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

    public sealed class SiteLoginAttempt
    {
        public bool DeviceLimited { get; set; }
        public string Error { get; set; }
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
            return ToClientSession(CreateSessionService().Load());
        }

        public static void SignOut()
        {
            CreateSessionService().SignOut();
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

            if (session.DeviceLimited)
            {
                try
                {
                    ListDevices();
                    return false;
                }
                catch (Exception)
                {
                    SignOut();
                    return true;
                }
            }

            try
            {
                SoftVerify(db);
                var list = ListDevices();
                var local = DeviceId();
                for (var i = 0; i < list.Items.Count; i++)
                {
                    if (string.Equals(list.Items[i].DeviceId, local, StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }

                SignOut();
                return true;
            }
            catch (Exception ex)
            {
                if (NetFail.IsUnreachable(ex))
                {
                    return false;
                }

                SignOut();
                return true;
            }
        }

        private static void SoftVerify(SqliteDb db)
        {
            try
            {
                CreateSessionService().Verify(CurrentDeviceContext(db));
            }
            catch (Exception)
            {
            }
        }

        public static SiteDeviceList ListDevices()
        {
            var list = CreateSessionService().ListDevices();
            var localId = DeviceId();
            var items = new List<SiteDeviceItem>();
            for (var i = 0; i < list.Devices.Count; i++)
            {
                var item = list.Devices[i];
                items.Add(new SiteDeviceItem
                {
                    DeviceId = item.DeviceId,
                    DisplayName = string.IsNullOrEmpty(item.DisplayName) ? item.DeviceId : item.DisplayName,
                    FirstSeenLocal = FormatLocal(item.FirstSeenUtc),
                    LastSeenLocal = FormatLocal(item.LastSeenUtc),
                    IsThisDevice = string.Equals(item.DeviceId, localId, StringComparison.OrdinalIgnoreCase)
                });
            }

            return new SiteDeviceList
            {
                MaxDevices = list.MaxDevices,
                ProductNo = list.ProductNo,
                ProductName = list.ProductName,
                Items = items
            };
        }

        public static void KickDevice(string deviceId)
        {
            CreateSessionService().KickDevice(deviceId);
        }

        private static string FormatLocal(DateTime? utc)
        {
            return utc.HasValue ? utc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm") : "";
        }

        public static SiteLoginAttempt TryLogin(string email, string password, string code)
        {
            var device = CurrentDeviceContext(null);
            var service = CreateSessionService();
            var outcome = string.IsNullOrEmpty(code)
                ? service.LoginPassword(email, password, device)
                : service.LoginCode(email, code, device);
            return new SiteLoginAttempt
            {
                DeviceLimited = outcome.DeviceLimited,
                Error = outcome.Error
            };
        }

        public static string SendCode(string email)
        {
            return CreateSessionService().SendCode(email).DevCode ?? "";
        }

        private static SiteSessionService CreateSessionService()
        {
            var urlFile = Path.Combine(AppPaths.DataDirectory, "site.url");
            var raw = File.Exists(urlFile) ? File.ReadAllText(urlFile) : "";
            var baseUrl = SiteUrlFile.Read(raw, SiteUrlFile.DefaultSite).TrimEnd('/');
            return new SiteSessionService(
                new HttpSiteTransport(new Uri(baseUrl)),
                new FileSiteSessionStore(AppPaths.DataDirectory, TokenFile, SessionFile));
        }

        private static SiteDeviceContext CurrentDeviceContext(SqliteDb db)
        {
            return new SiteDeviceContext
            {
                DeviceId = DeviceId(),
                DisplayName = DeviceDisplayName(),
                Note = CustomerNote(db),
                TaskStatus = CurrentTaskStatus(db)
            };
        }

        private static SiteSession ToClientSession(SiteSessionState state)
        {
            state = state ?? new SiteSessionState();
            return new SiteSession
            {
                LoggedIn = state.LoggedIn,
                Email = state.Email,
                Name = state.Name,
                Nickname = state.Nickname,
                CompanyName = state.CompanyName,
                Valid = state.Valid,
                AuthEndUtc = state.AuthEndUtc,
                AuthStatus = state.AuthStatus,
                NextCheckDays = state.NextCheckDays,
                ProductNo = state.ProductNo,
                ProductName = state.ProductName,
                MaxDevices = state.MaxDevices
            };
        }

        private static string DeviceId()
        {
            return DeviceIdStore.CurrentId();
        }

        static string CurrentTaskStatus(SqliteDb db)
        {
            SqliteDb owned = null;
            try
            {
                var source = db;
                if (source == null)
                {
                    owned = SqliteDb.OpenDefault();
                    source = owned;
                }

                return DeviceReport.TaskStatus(source, DateTime.UtcNow);
            }
            catch (Exception)
            {
                return "";
            }
            finally
            {
                if (owned != null) owned.Dispose();
            }
        }

        static string CustomerNote(SqliteDb db)
        {
            SqliteDb owned = null;
            try
            {
                var source = db;
                if (source == null)
                {
                    owned = SqliteDb.OpenDefault();
                    source = owned;
                }

                var id = SettingsStore.Get(source).SoftwareId;
                if (string.IsNullOrWhiteSpace(id)) id = AppPaths.ProductPathName;
                id = id.Trim();
                return id.Length <= 40 ? id : id.Substring(0, 40);
            }
            catch (Exception)
            {
                return "";
            }
            finally
            {
                if (owned != null) owned.Dispose();
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

    }
}
