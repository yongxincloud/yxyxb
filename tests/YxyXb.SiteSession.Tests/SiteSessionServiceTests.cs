using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using YxyXb.SiteSession;

namespace YxyXb.SiteSession.Tests
{
    public class SiteSessionServiceTests
    {
        [Test]
        public void PasswordLogin_UsesClientLoginAndStoresSession()
        {
            var transport = new FakeTransport(200, LoginJson("token-password", true));
            var store = new FakeStore();
            var service = new SiteSessionService(transport, store);

            var result = service.LoginPassword("a@example.invalid", "secret", Device());

            Assert.That(result.Ok, Is.True);
            Assert.That(transport.Path, Is.EqualTo("/api/client/login"));
            Assert.That(transport.Payload["password"], Is.EqualTo("secret"));
            Assert.That(store.Session.Token, Is.EqualTo("token-password"));
            Assert.That(store.Session.ProductNo, Is.EqualTo("STANDARD"));
        }

        [Test]
        public void CodeLogin_UsesCodeEndpointAndDeviceContext()
        {
            var transport = new FakeTransport(200, LoginJson("token-code", true));
            var store = new FakeStore();
            var service = new SiteSessionService(transport, store);

            service.LoginCode("a@example.invalid", "123456", Device());

            Assert.That(transport.Path, Is.EqualTo("/api/client/login-code"));
            Assert.That(transport.Payload["code"], Is.EqualTo("123456"));
            Assert.That(transport.Payload["deviceId"], Is.EqualTo("device-1"));
            Assert.That(transport.Payload["note"], Is.EqualTo("client-note"));
        }

        [Test]
        public void DeviceLimit_IsSavedAndReturnedAsUserOutcome()
        {
            var transport = new FakeTransport(409,
                "{\"ok\":false,\"error\":\"已达 3 台设备上限\",\"errorCode\":\"DEVICE_LIMIT\","
                + "\"token\":\"limited-token\",\"valid\":false,\"authStatus\":\"device_limit\","
                + "\"maxDevices\":3}");
            var store = new FakeStore();
            var service = new SiteSessionService(transport, store);

            var result = service.LoginPassword("a@example.invalid", "secret", Device());

            Assert.That(result.Ok, Is.False);
            Assert.That(result.DeviceLimited, Is.True);
            Assert.That(result.Error, Does.Contain("3 台"));
            Assert.That(store.Session.Token, Is.EqualTo("limited-token"));
            Assert.That(store.Session.AuthStatus, Is.EqualTo("device_limit"));
        }

        [Test]
        public void ServerError_IsReportedWithoutStoringSession()
        {
            var transport = new FakeTransport(400, "{\"ok\":false,\"error\":\"邮箱或密码错误\"}");
            var store = new FakeStore();
            var service = new SiteSessionService(transport, store);

            var error = Assert.Throws<InvalidOperationException>(
                delegate { service.LoginPassword("a@example.invalid", "bad", Device()); });

            Assert.That(error.Message, Does.Contain("邮箱或密码错误"));
            Assert.That(store.Session, Is.Null);
        }

        [Test]
        public void SendCode_ReturnsDevelopmentCode()
        {
            var transport = new FakeTransport(200, "{\"ok\":true,\"devCode\":\"654321\"}");
            var service = new SiteSessionService(transport, new FakeStore());

            var result = service.SendCode("a@example.invalid");

            Assert.That(result.DevCode, Is.EqualTo("654321"));
            Assert.That(transport.Path, Is.EqualTo("/api/account/send-code"));
        }

        [Test]
        public void FileStore_ReadsLegacySessionAndTokenFiles()
        {
            var root = Path.Combine(Path.GetTempPath(), "yxy-site-session-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var token = Path.Combine(root, "site.token");
            var session = Path.Combine(root, "site.session");
            try
            {
                File.WriteAllText(token, "legacy-token");
                File.WriteAllText(session,
                    "email=a@example.invalid\r\n"
                    + "name=测试客户\r\n"
                    + "company=测试公司\r\n"
                    + "valid=true\r\n"
                    + "end=2030-01-02T03:04:05Z\r\n"
                    + "status=active\r\n"
                    + "days=30\r\n"
                    + "productNo=STANDARD\r\n"
                    + "productName=标准版\r\n"
                    + "maxDevices=10\r\n");

                var loaded = new FileSiteSessionStore(root, token, session).Load();

                Assert.That(loaded.LoggedIn, Is.True);
                Assert.That(loaded.Token, Is.EqualTo("legacy-token"));
                Assert.That(loaded.Email, Is.EqualTo("a@example.invalid"));
                Assert.That(loaded.CompanyName, Is.EqualTo("测试公司"));
                Assert.That(loaded.ProductNo, Is.EqualTo("STANDARD"));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        private static SiteDeviceContext Device()
        {
            return new SiteDeviceContext
            {
                DeviceId = "device-1",
                DisplayName = "PC-1",
                Note = "client-note",
                TaskStatus = "ok"
            };
        }

        private static string LoginJson(string token, bool valid)
        {
            return "{\"ok\":true,\"token\":\"" + token + "\",\"valid\":" + (valid ? "true" : "false")
                + ",\"authEndTime\":\"2030-01-02T03:04:05Z\",\"authStatus\":\"active\","
                + "\"nextCheckDays\":30,\"name\":\"测试客户\",\"companyName\":\"测试公司\","
                + "\"productNo\":\"STANDARD\",\"productName\":\"标准版\",\"maxDevices\":10}";
        }

        private sealed class FakeTransport : ISiteHttpTransport
        {
            private readonly int _status;
            private readonly string _body;

            public FakeTransport(int status, string body)
            {
                _status = status;
                _body = body;
            }

            public string Path { get; private set; }
            public IDictionary<string, object> Payload { get; private set; }

            public SiteHttpResponse Post(string path, IDictionary<string, object> payload)
            {
                Path = path;
                Payload = payload;
                return new SiteHttpResponse { StatusCode = _status, Body = _body };
            }
        }

        private sealed class FakeStore : ISiteSessionStore
        {
            public SiteSessionState Session { get; private set; }

            public SiteSessionState Load()
            {
                return Session ?? new SiteSessionState();
            }

            public string LoadToken()
            {
                return Session == null ? "" : Session.Token;
            }

            public void Save(SiteSessionState session)
            {
                Session = session;
            }

            public void Clear()
            {
                Session = null;
            }
        }
    }
}
