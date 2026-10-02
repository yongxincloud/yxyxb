using NUnit.Framework;
using Yxy.Contracts.Dto;
using Yxy.Mssql;

namespace Yxy.Mssql.Tests
{
    [TestFixture]
    public sealed class MssqlModuleTests
    {
        [Test]
        public void StatusText_NotEmpty()
        {
            Assert.That(MssqlModule.StatusText(), Does.Contain("MSSQL"));
        }

        [Test]
        public void SystemDb_Excluded()
        {
            Assert.That(MssqlConnect.IsSystemDb("master"), Is.True);
            Assert.That(MssqlConnect.IsSystemDb("tempdb"), Is.True);
            Assert.That(MssqlConnect.IsSystemDb("AppDb"), Is.False);
        }

        [Test]
        public void QuoteName_EscapesBracket()
        {
            Assert.That(MssqlConnect.QuoteName("a]b"), Is.EqualTo("[a]]b]"));
        }

        [Test]
        public void ConnectionString_Windows()
        {
            var text = MssqlConnect.BuildConnectionString(new MssqlInstanceDto
            {
                Server = "localhost\\SQLEXPRESS",
                AuthKind = "windows",
                TimeoutSeconds = 15
            }, null);
            Assert.That(text, Does.Contain("localhost\\SQLEXPRESS"));
            Assert.That(text, Does.Contain("Integrated Security=True").Or.Contain("Integrated Security=true"));
        }
    }
}
