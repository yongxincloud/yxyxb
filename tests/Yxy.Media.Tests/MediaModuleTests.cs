using NUnit.Framework;
using Yxy.Media;

namespace Yxy.Media.Tests
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
