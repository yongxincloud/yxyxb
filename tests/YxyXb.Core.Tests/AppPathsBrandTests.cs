using System;
using System.IO;
using NUnit.Framework;
using YxyXb.Core;

namespace YxyXb.Core.Tests
{
    [TestFixture]
    public class AppPathsBrandTests
    {
        [Test]
        public void Display_And_Path_Names_Match_Brand_Spec()
        {
            Assert.That(AppPaths.ProductDisplayName, Is.EqualTo("用心云·循备"));
            Assert.That(AppPaths.ProductPathName, Is.EqualTo("用心云循备"));
            Assert.That(AppPaths.ProductShortName, Is.EqualTo("循备"));
            Assert.That(AppPaths.ServiceDisplayName, Is.EqualTo("用心云·循备 Service"));
            Assert.That(AppPaths.DefaultBakDirectory, Is.EqualTo(@"D:\yxyxb\bak"));
            Assert.That(AppPaths.ServiceName, Is.EqualTo("YxyXbService"));
        }

        [Test]
        public void DatabaseFile_Prefers_Legacy_When_Present()
        {
            var root = Path.Combine(Path.GetTempPath(), "yxy-brand-" + Guid.NewGuid().ToString("N"));
            try
            {
                AppPaths.SetRootForTests(root);
                Directory.CreateDirectory(AppPaths.DataDirectory);
                var legacy = Path.Combine(AppPaths.DataDirectory, "yxy.db");
                File.WriteAllText(legacy, "x");
                Assert.That(AppPaths.DatabaseFile, Is.EqualTo(legacy));
            }
            finally
            {
                AppPaths.SetRootForTests(null);
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        [Test]
        public void DatabaseFile_Uses_New_When_No_Legacy()
        {
            var root = Path.Combine(Path.GetTempPath(), "yxy-brand-" + Guid.NewGuid().ToString("N"));
            try
            {
                AppPaths.SetRootForTests(root);
                Directory.CreateDirectory(AppPaths.DataDirectory);
                Assert.That(AppPaths.DatabaseFile, Is.EqualTo(Path.Combine(AppPaths.DataDirectory, "yxyxb.db")));
            }
            finally
            {
                AppPaths.SetRootForTests(null);
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }
    }
}
