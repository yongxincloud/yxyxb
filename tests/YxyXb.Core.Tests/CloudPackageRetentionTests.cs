using System;
using System.Collections.Generic;
using NUnit.Framework;
using YxyXb.Contracts.Cloud;

namespace YxyXb.Core.Tests
{
    public class CloudPackageRetentionTests
    {
        [Test]
        public void GetExpiredUnitFiles_RequiresEveryPartToBeOld_AndPutsManifestLast()
        {
            var old = DateTime.UtcNow.AddDays(-10);
            var now = DateTime.UtcNow;
            var objects = new Dictionary<string, DateTime>
            {
                { "p/a.part01.zip", old },
                { "p/a.part02.zip", old },
                { "p/a.manifest.json", old }
            };

            var files = CloudPackageRetention.GetExpiredUnitFiles("p/a.manifest.json", objects, DateTime.UtcNow.AddDays(-5));

            Assert.That(files, Is.EqualTo(new[] { "p/a.part01.zip", "p/a.part02.zip", "p/a.manifest.json" }));

            objects["p/a.part02.zip"] = now;
            Assert.That(CloudPackageRetention.GetExpiredUnitFiles(
                "p/a.manifest.json", objects, DateTime.UtcNow.AddDays(-5)), Is.Empty);
        }
    }
}
