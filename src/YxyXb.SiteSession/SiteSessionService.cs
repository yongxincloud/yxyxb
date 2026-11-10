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
            var json = ParseObject(response.Body);
            var errorCode = GetString(json, "errorCode");
            var error = GetString(json, "error");
            if (!string.Equals(errorCode, "DEVICE_LIMIT", StringComparison.Ordinal)
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
                Ok = !string.Equals(errorCode, "DEVICE_LIMIT", StringComparison.Ordinal),
                DeviceLimited = string.Equals(errorCode, "DEVICE_LIMIT", StringComparison.Ordinal),
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
