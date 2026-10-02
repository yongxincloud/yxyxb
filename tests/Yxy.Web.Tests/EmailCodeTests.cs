using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Yxy.Web.Data;
using Yxy.Web.Services;

namespace Yxy.Web.Tests;

public sealed class EmailCodeTests
{
    [Test]
    public async Task Issue_Returns_Six_Digits()
    {
        await using var db = NewDb();
        var time = new FakeTime();
        var svc = new EmailCodeService(db, time);
        var issued = await svc.IssueAsync("a@b.com", "register", "1.1.1.1");
        Assert.That(issued.Ok, Is.True);
        Assert.That(issued.Code, Does.Match(new Regex(@"^\d{6}$")));
    }

    [Test]
    public async Task Second_Issue_Within_60_Seconds_Is_Too_Soon()
    {
        await using var db = NewDb();
        var time = new FakeTime();
        var svc = new EmailCodeService(db, time);
        await svc.IssueAsync("a@b.com", "register", "1.1.1.1");
        var again = await svc.IssueAsync("a@b.com", "login", "1.1.1.1");
        Assert.That(again.Error, Is.EqualTo("too_soon"));
    }

    [Test]
    public async Task Consume_Works_Once()
    {
        await using var db = NewDb();
        var time = new FakeTime();
        var svc = new EmailCodeService(db, time);
        var issued = await svc.IssueAsync("a@b.com", "register", "1.1.1.1");
        Assert.That(await svc.ConsumeAsync("a@b.com", "register", issued.Code!), Is.True);
        Assert.That(await svc.ConsumeAsync("a@b.com", "register", issued.Code!), Is.False);
    }

    [Test]
    public async Task Expired_Code_Fails()
    {
        await using var db = NewDb();
        var time = new FakeTime();
        var svc = new EmailCodeService(db, time);
        var issued = await svc.IssueAsync("a@b.com", "register", "1.1.1.1");
        time.Now = time.Now.AddMinutes(6);
        Assert.That(await svc.ConsumeAsync("a@b.com", "register", issued.Code!), Is.False);
    }

    [Test]
    public async Task Eleventh_Send_Same_Utc_Day_Hits_Daily_Cap()
    {
        await using var db = NewDb();
        var time = new FakeTime();
        var svc = new EmailCodeService(db, time);
        for (var i = 0; i < 10; i++)
        {
            var issued = await svc.IssueAsync("a@b.com", "register", "1.1.1.1");
            Assert.That(issued.Ok, Is.True, "send " + i);
            time.Now = time.Now.AddSeconds(61);
        }
        var blocked = await svc.IssueAsync("a@b.com", "register", "1.1.1.1");
        Assert.That(blocked.Error, Is.EqualTo("daily_cap"));
    }

    static WebDb NewDb()
    {
        var path = Path.Combine(Path.GetTempPath(), "yxy-code-" + Guid.NewGuid().ToString("N") + ".db");
        var options = new DbContextOptionsBuilder<WebDb>().UseSqlite("Data Source=" + path).Options;
        var db = new WebDb(options);
        db.Database.EnsureCreated();
        return db;
    }

    sealed class FakeTime : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.Parse("2026-10-02T01:00:00Z");
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
