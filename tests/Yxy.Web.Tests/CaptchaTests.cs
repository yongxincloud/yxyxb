using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using Yxy.Web.Services;

namespace Yxy.Web.Tests;

public sealed class CaptchaTests
{
    [Test]
    public void Testing_Captcha_Accepts_0000()
    {
        var svc = new CaptchaService(new MemoryCache(new MemoryCacheOptions()), new StubEnv("Testing"));
        var created = svc.Create();
        Assert.That(svc.VerifyAndConsume(created.Id, "0000"), Is.True);
        Assert.That(svc.VerifyAndConsume(created.Id, "0000"), Is.False);
    }

    [Test]
    public void Wrong_Answer_Fails()
    {
        var svc = new CaptchaService(new MemoryCache(new MemoryCacheOptions()), new StubEnv("Testing"));
        var created = svc.Create();
        Assert.That(svc.VerifyAndConsume(created.Id, "9999"), Is.False);
        Assert.That(svc.VerifyAndConsume(created.Id, "0000"), Is.False);
    }

    [Test]
    public void CaptchaView_Has_No_Answer_Property()
    {
        var names = typeof(CaptchaView).GetProperties().Select(x => x.Name).ToArray();
        Assert.That(names, Is.EquivalentTo(new[] { "Id", "Svg" }));
    }

    sealed class StubEnv : IHostEnvironment
    {
        public StubEnv(string name) => EnvironmentName = name;
        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "Yxy.Web.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
