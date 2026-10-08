using System;
using System.Collections.Generic;
using NUnit.Framework;
using YxyXb.Contracts.Dto;
using YxyXb.Core.Backup;

namespace YxyXb.Core.Tests
{
    [TestFixture]
    public sealed class BackupNamingTests
    {
        [Test]
        public void MssqlZip_WithoutDbFlag_TwoDatabases_MustNotShareName()
        {
            var parts = NameParts.FromFlags(NameParts.DefaultFlags & ~NameParts.FileDb);
            var when = new DateTime(2026, 10, 5, 3, 0, 0, DateTimeKind.Utc);
            var a = BackupNaming.MssqlZipName("sid", "task", "dbA", when, parts);
            var b = BackupNaming.MssqlZipName("sid", "task", "dbB", when, parts);
            Assert.That(a, Is.Not.EqualTo(b));
            Assert.That(a, Does.Contain("dbA"));
            Assert.That(b, Does.Contain("dbB"));
        }

        [Test]
        public void PickSelected_KeepsEveryMatchingUserDb()
        {
            var all = new[] { "Alpha", "Beta", "Gamma" };
            var picked = DatabaseScope.Pick(all, "selected", "Alpha, Gamma", delegate { return false; });
            Assert.That(picked, Is.EqualTo(new[] { "Alpha", "Gamma" }));
        }

        [Test]
        public void PickSelected_AcceptsChineseComma()
        {
            var all = new[] { "A", "B" };
            var picked = DatabaseScope.Pick(all, "selected", "A，B", delegate { return false; });
            Assert.That(picked, Is.EqualTo(new[] { "A", "B" }));
        }
    }
}
