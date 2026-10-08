using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using YxyXb.Api.Services;

namespace YxyXb.Api.Tests;

public sealed class CaptchaTests
{
    sealed class Env : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "t";
        public string ContentRootPath { get; set; } = ".";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    [Test]
    public void Testing_Captcha_Accepts_0000()
    {
        var svc = new CaptchaService(new MemoryCache(new MemoryCacheOptions()), new Env());
        var view = svc.Create();
        Assert.That(svc.VerifyAndConsume(view.Id, "0000"), Is.True);
        Assert.That(svc.VerifyAndConsume(view.Id, "0000"), Is.False);
    }

    [Test]
    public void Wrong_Answer_Fails_And_Burns_Ticket()
    {
        var svc = new CaptchaService(new MemoryCache(new MemoryCacheOptions()), new Env());
        var view = svc.Create();
        Assert.That(svc.VerifyAndConsume(view.Id, "9999"), Is.False);
        Assert.That(svc.VerifyAndConsume(view.Id, "0000"), Is.False);
    }

    [Test]
    public void CaptchaView_Has_No_Answer_Property()
    {
        var names = typeof(CaptchaView).GetProperties().Select(p => p.Name).ToArray();
        Assert.That(names, Is.EqualTo(new[] { "Id", "Svg" }));
    }
}
