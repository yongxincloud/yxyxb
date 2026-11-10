using System;
using System.Collections.Generic;
using System.Globalization;
using System.Web.Script.Serialization;

namespace YxyXb.SiteSession
{
    public sealed class SiteSessionService
    {
        private readonly ISiteHttpTransport _transport;
        private readonly ISiteSessionStore _store;

        public SiteSessionService(ISiteHttpTransport transport, ISiteSessionStore store)
        {
            if (transport == null) throw new ArgumentNullException("transport");
            if (store == null) throw new ArgumentNullException("store");
            _transport = transport;
            _store = store;
        }

        public SiteSessionState Load()
        {
            return _store.Load();
        }

        public void SignOut()
        {
            _store.Clear();
        }

        public SiteCodeOutcome SendCode(string email)
        {
            var response = _transport.Post("/api/account/send-code", new Dictionary<string, object>
            {
                { "email", email ?? "" },
                { "scene", "login" }
            });
            var json = ParseObject(response.Body);
            EnsureSuccess(response, json);
            return new SiteCodeOutcome
            {
                Ok = true,
                DevCode = GetString(json, "devCode")
            };
        }

        public SiteLoginOutcome LoginPassword(string email, string password, SiteDeviceContext device)
        {
            return Login("/api/client/login", email, "password", password, device);
        }

        public SiteLoginOutcome LoginCode(string email, string code, SiteDeviceContext device)
        {
            return Login("/api/client/login-code", email, "code", code, device);
        }

        public SiteLoginOutcome Verify(SiteDeviceContext device)
        {
            var token = _store.LoadToken();
            if (string.IsNullOrEmpty(token))
            {
                throw new InvalidOperationException("登录已失效");
            }

            device = device ?? new SiteDeviceContext();
            var response = _transport.Post("/api/client/verify", new Dictionary<string, object>
            {
                { "token", token },
                { "deviceId", device.DeviceId ?? "" },
                { "displayName", device.DisplayName ?? "" },
                { "note", device.Note ?? "" },
                { "taskStatus", device.TaskStatus ?? "" }
            });
            return HandleSessionResponse(response, _store.Load().Email, true);
        }

        public SiteDeviceListState ListDevices()
        {
            var token = RequireToken();
            var response = _transport.Post("/api/client/devices", new Dictionary<string, object>
            {
                { "token", token }
            });
            var json = ParseObject(response.Body);
            EnsureSuccess(response, json);
            var list = new SiteDeviceListState
            {
                MaxDevices = GetInt(json, "maxDevices"),
                ProductNo = GetString(json, "productNo"),
                ProductName = GetString(json, "productName"),
                Devices = new List<SiteDeviceInfo>()
            };
            object devices;
            var rows = TryGet(json, "devices", out devices) ? devices as System.Collections.IEnumerable : null;
            if (rows != null)
            {
                foreach (var value in rows)
                {
                    var row = value as IDictionary<string, object>;
                    if (row == null)
                    {
                        continue;
                    }

                    list.Devices.Add(new SiteDeviceInfo
                    {
                        DeviceId = GetString(row, "deviceId"),
                        DisplayName = GetString(row, "displayName"),
                        Note = GetString(row, "note"),
                        FirstSeenUtc = GetDate(row, "firstSeenAt"),
                        LastSeenUtc = GetDate(row, "lastSeenAt"),
                        WanIp = GetString(row, "wanIp"),
                        TaskStatus = GetString(row, "taskStatus")
                    });
                }
            }

            var session = _store.Load();
            if (session.LoggedIn)
            {
                session.MaxDevices = list.MaxDevices;
                session.ProductNo = list.ProductNo;
                session.ProductName = list.ProductName;
                _store.Save(session);
            }

            return list;
        }

        public void KickDevice(string deviceId)
        {
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                throw new InvalidOperationException("缺少设备标识");
            }

            var response = _transport.Post("/api/client/devices/kick", new Dictionary<string, object>
            {
                { "token", RequireToken() },
                { "deviceId", deviceId }
            });
            EnsureSuccess(response, ParseObject(response.Body));
        }

        private SiteLoginOutcome Login(string path, string email, string credentialName, string credential, SiteDeviceContext device)
        {
            device = device ?? new SiteDeviceContext();
            var response = _transport.Post(path, new Dictionary<string, object>
            {
                { "email", email ?? "" },
                { credentialName, credential ?? "" },
                { "deviceId", device.DeviceId ?? "" },
                { "displayName", device.DisplayName ?? "" },
                { "note", device.Note ?? "" },
                { "taskStatus", device.TaskStatus ?? "" }
            });
            return HandleSessionResponse(response, email, true);
        }

        private SiteLoginOutcome HandleSessionResponse(SiteHttpResponse response, string email, bool allowDeviceLimit)
        {
            var json = ParseObject(response.Body);
            var errorCode = GetString(json, "errorCode");
            var error = GetString(json, "error");
            var deviceLimited = allowDeviceLimit && string.Equals(errorCode, "DEVICE_LIMIT", StringComparison.Ordinal);
            if (!deviceLimited
                && (response.StatusCode < 200 || response.StatusCode > 299 || !GetBool(json, "ok", true)))
            {
                throw new InvalidOperationException(string.IsNullOrEmpty(error) ? "登录失败" : error);
            }

            var session = ToSession(email, json);
            if (string.IsNullOrEmpty(session.Token))
            {
                throw new InvalidOperationException(string.IsNullOrEmpty(error) ? "登录失败" : error);
            }

            _store.Save(session);
            return new SiteLoginOutcome
            {
                Ok = !deviceLimited,
                DeviceLimited = deviceLimited,
                ErrorCode = errorCode,
                Error = error,
                Session = session
            };
        }

        private static SiteSessionState ToSession(string email, IDictionary<string, object> json)
        {
            var session = new SiteSessionState
            {
                LoggedIn = true,
                Email = string.IsNullOrEmpty(email) ? GetString(json, "email") : email,
                Token = GetString(json, "token"),
                Name = GetString(json, "name"),
                Nickname = GetString(json, "nickname"),
                CompanyName = GetString(json, "companyName"),
                Valid = GetBool(json, "valid", false),
                AuthStatus = GetString(json, "authStatus"),
                NextCheckDays = GetInt(json, "nextCheckDays") ?? 0,
                ProductNo = GetString(json, "productNo"),
                ProductName = GetString(json, "productName"),
                MaxDevices = GetInt(json, "maxDevices")
            };

            var end = GetString(json, "authEndTime");
            DateTime parsed;
            if (!string.IsNullOrEmpty(end)
                && DateTime.TryParse(end, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out parsed))
            {
                session.AuthEndUtc = parsed.ToUniversalTime();
            }

            return session;
        }

        private static IDictionary<string, object> ParseObject(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return new Dictionary<string, object>();
            }

            try
            {
                return new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(body)
                    ?? new Dictionary<string, object>();
            }
            catch (Exception)
            {
                throw new InvalidOperationException("服务器返回了无法识别的内容");
            }
        }

        private static void EnsureSuccess(SiteHttpResponse response, IDictionary<string, object> json)
        {
            if (response.StatusCode >= 200 && response.StatusCode <= 299 && GetBool(json, "ok", true))
            {
                return;
            }

            var error = GetString(json, "error");
            throw new InvalidOperationException(string.IsNullOrEmpty(error) ? "登录失败" : error);
        }

        private static string GetString(IDictionary<string, object> json, string key)
        {
            object value;
            if (json != null && TryGet(json, key, out value) && value != null)
            {
                return Convert.ToString(value, CultureInfo.InvariantCulture);
            }

            return "";
        }

        private static bool GetBool(IDictionary<string, object> json, string key, bool fallback)
        {
            object value;
            if (json != null && TryGet(json, key, out value) && value != null)
            {
                bool parsed;
                if (bool.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out parsed))
                {
                    return parsed;
                }
            }

            return fallback;
        }

        private static int? GetInt(IDictionary<string, object> json, string key)
        {
            object value;
            if (json != null && TryGet(json, key, out value) && value != null)
            {
                int parsed;
                if (int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out parsed))
                {
                    return parsed;
                }
            }

            return null;
        }

        private static DateTime? GetDate(IDictionary<string, object> json, string key)
        {
            var text = GetString(json, key);
            DateTime parsed;
            if (!string.IsNullOrEmpty(text)
                && DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out parsed))
            {
                return parsed.ToUniversalTime();
            }

            return null;
        }

        private string RequireToken()
        {
            var token = _store.LoadToken();
            if (string.IsNullOrEmpty(token))
            {
                throw new InvalidOperationException("请先登录。");
            }

            return token;
        }

        private static bool TryGet(IDictionary<string, object> json, string key, out object value)
        {
            if (json.TryGetValue(key, out value))
            {
                return true;
            }

            foreach (var pair in json)
            {
                if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    value = pair.Value;
                    return true;
                }
            }

            value = null;
            return false;
        }
    }
}
