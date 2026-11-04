using NUnit.Framework;
using YxyXb.Contracts.Cloud;

namespace YxyXb.Core.Tests
{
    public class CloudObjectKeysTests
    {
        [Test]
        public void IsPackageObject_IncludesZipAndManifestButNotOtherFiles()
        {
            Assert.That(CloudObjectKeys.IsPackageObject("a.part01.zip"), Is.True);
            Assert.That(CloudObjectKeys.IsPackageObject("a.manifest.json"), Is.True);
            Assert.That(CloudObjectKeys.IsPackageObject("a.txt"), Is.False);
            Assert.That(CloudObjectKeys.IsManifest("a.manifest.json"), Is.True);
        }
    }
}
