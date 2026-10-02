using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Yxy.Api.Data;
using Yxy.Api.Services;

namespace Yxy.Api.Tests;

public sealed class EmailCodeTests
{
    sealed class FakeTime : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.Parse("2026-10-02T01:00:00Z");
        public override DateTimeOffset GetUtcNow() => Now;
    }

    sealed class DbHold : IAsyncDisposable
    {
        public WebAppFactory Factory { get; } = new();
        public FakeTime Time { get; } = new();
        public WebDb Db { get; }
        readonly AsyncServiceScope _scope;
        public DbHold()
        {
            _scope = Factory.Services.CreateAsyncScope();
            Db = _scope.ServiceProvider.GetRequiredService<WebDb>();
        }
        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await Factory.DisposeAsync();
        }
    }

    [Test]
    public async Task Issue_Returns_Six_Digits()
    {
        await using var hold = new DbHold();
        var result = await new EmailCodeService(hold.Db, hold.Time).IssueAsync("a@b.com", "register", "1.1.1.1");
        Assert.That(result.Ok, Is.True);
        Assert.That(result.Code, Does.Match(@"^\d{6}$"));
    }

    [Test]
    public async Task Second_Issue_Within_60_Seconds_Is_Too_Soon()
    {
        await using var hold = new DbHold();
        var svc = new EmailCodeService(hold.Db, hold.Time);
        await svc.IssueAsync("a@b.com", "register", "1.1.1.1");
        var second = await svc.IssueAsync("a@b.com", "login", "1.1.1.1");
        Assert.That(second.Error, Is.EqualTo("too_soon"));
    }

    [Test]
    public async Task Consume_Works_Once()
    {
        await using var hold = new DbHold();
        var svc = new EmailCodeService(hold.Db, hold.Time);
        var issued = await svc.IssueAsync("a@b.com", "register", "1.1.1.1");
        Assert.That(await svc.ConsumeAsync("a@b.com", "register", issued.Code!), Is.True);
        Assert.That(await svc.ConsumeAsync("a@b.com", "register", issued.Code!), Is.False);
    }

    [Test]
    public async Task Expired_Code_Fails()
    {
        await using var hold = new DbHold();
        var svc = new EmailCodeService(hold.Db, hold.Time);
        var issued = await svc.IssueAsync("a@b.com", "register", "1.1.1.1");
        hold.Time.Now = hold.Time.Now.AddMinutes(6);
        Assert.That(await svc.ConsumeAsync("a@b.com", "register", issued.Code!), Is.False);
    }

    [Test]
    public async Task Eleventh_Send_Same_Utc_Day_Hits_Daily_Cap()
    {
        await using var hold = new DbHold();
        var svc = new EmailCodeService(hold.Db, hold.Time);
        for (var i = 0; i < 10; i++)
        {
            var ok = await svc.IssueAsync("a@b.com", "register", "1.1.1.1");
            Assert.That(ok.Ok, Is.True);
            hold.Time.Now = hold.Time.Now.AddSeconds(61);
        }
        var eleventh = await svc.IssueAsync("a@b.com", "register", "1.1.1.1");
        Assert.That(eleventh.Error, Is.EqualTo("daily_cap"));
    }
}
