using System;
using System.IO;
using NUnit.Framework;
using YxyXb.Core;

namespace YxyXb.Core.Tests
{
    [TestFixture]
    public sealed class SiteGateTests
    {
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "yxy-gate-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            AppPaths.SetRootForTests(_root);
            AppPaths.EnsureDirectories();
        }

        [TearDown]
        public void TearDown()
        {
            AppPaths.SetRootForTests(null);
            try
            {
                if (Directory.Exists(_root)) Directory.Delete(_root, true);
            }
            catch (IOException)
            {
            }
        }

        [Test]
        public void NoSession_BlocksChannels()
        {
            Assert.That(SiteGate.AllowsChannels(new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc)), Is.False);
        }

        [Test]
        public void ActiveAuth_Allows_UntilGraceEnds()
        {
            var end = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
            WriteSession(end, true);
            Assert.That(SiteGate.AllowsChannels(end.AddDays(7)), Is.True);
            Assert.That(SiteGate.AllowsChannels(end.AddDays(7).AddMinutes(1)), Is.False);
        }

        [Test]
        public void InvalidFlag_BlocksChannels()
        {
            var end = new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc);
            WriteSession(end, false);
            Assert.That(SiteGate.AllowsChannels(end), Is.False);
        }

        static void WriteSession(DateTime endUtc, bool valid)
        {
            File.WriteAllText(Path.Combine(AppPaths.DataDirectory, "site.token"), "token");
            File.WriteAllText(Path.Combine(AppPaths.DataDirectory, "site.session"),
                "email=a@example.com\r\nvalid=" + (valid ? "true" : "false") + "\r\nend=" + endUtc.ToString("o") + "\r\n");
        }
    }
}
