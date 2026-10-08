using NUnit.Framework;
using YxyXb.Media;

namespace YxyXb.Media.Tests
{
    [TestFixture]
    public sealed class MediaModuleTests
    {
        [Test]
        public void StatusText_NotEmpty()
        {
            Assert.That(MediaModule.StatusText(), Does.Contain("离线介质"));
        }
    }
}
