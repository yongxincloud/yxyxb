using System.Net;
using System.Net.Http.Json;
using NUnit.Framework;

namespace Yxy.Api.Tests;

public sealed class PasswordLoginTests
{
    [Test]
    public async Task Wrong_Password_And_Missing_Account_Share_Message()
    {
        await using var factory = new WebAppFactory();
        var client = factory.CreateClient();
        await AccountApi.Register(factory, client, "user@example.com", "abc12345");
        var wrong = await AccountApi.Body(await AccountApi.Post(client, "/api/account/login", new { email = "user@example.com", password = "badpass1" }));
        var missing = await AccountApi.Body(await AccountApi.Post(client, "/api/account/login", new { email = "none@example.com", password = "abc12345" }));
        Assert.That(wrong, Does.Contain("邮箱或密码错误"));
        Assert.That(missing, Does.Contain("邮箱或密码错误"));
        Assert.That(wrong, Does.Not.Contain("不存在"));
        Assert.That(missing, Does.Not.Contain("不存在"));
    }

    [Test]
    public async Task Five_Failures_Lock_For_Fifteen_Minutes()
    {
        await using var factory = new WebAppFactory();
        var client = factory.CreateClient();
        await AccountApi.Register(factory, client, "user@example.com", "abc12345");
        for (var i = 0; i < 5; i++)
            await AccountApi.Post(client, "/api/account/login", new { email = "user@example.com", password = "badpass1" });
        var locked = await AccountApi.Body(await AccountApi.Post(client, "/api/account/login", new { email = "user@example.com", password = "abc12345" }));
        Assert.That(locked, Does.Contain("登录已锁定"));
    }

    [Test]
    public async Task Right_Password_Sets_Cookie_And_Me()
    {
        await using var factory = new WebAppFactory();
        var client = factory.CreateClient();
        await AccountApi.Register(factory, client, "user@example.com", "abc12345");
        var login = await AccountApi.Post(client, "/api/account/login", new { email = "user@example.com", password = "abc12345" });
        Assert.That(login.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var me = await client.GetFromJsonAsync<MeDto>("/api/account/me");
        Assert.That(me!.Email, Is.EqualTo("user@example.com"));
    }

    sealed record MeDto(bool Ok, string? Error, string? Email);
}
