using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using YxyXb.Api.Data;
using YxyXb.Api.Services;

namespace YxyXb.Api.Tests;

public sealed class ResetPasswordTests
{
    [Test]
    public async Task Reset_Changes_Password()
    {
        await using var factory = new WebAppFactory();
        var client = factory.CreateClient();
        await AccountApi.SeedAccount(factory, "user@example.com", "abc12345");
        await AccountApi.Post(client, "/api/account/send-code", new { email = "user@example.com", scene = "reset" });
        var code = Regex.Match(factory.Services.GetRequiredService<FakeEmailSender>().LastBody ?? "", @"\d{6}").Value;
        var reset = await AccountApi.Post(client, "/api/account/reset", new
        {
            email = "user@example.com",
            code,
            password = "newpass12",
            confirm = "newpass12"
        });
        Assert.That((int)reset.StatusCode, Is.EqualTo(200));
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WebDb>();
        Assert.That((await db.Accounts.SingleAsync()).PasswordHash, Is.Not.EqualTo("newpass12"));
        var oldLogin = await AccountApi.Body(await AccountApi.Post(client, "/api/account/login", new { email = "user@example.com", password = "abc12345" }));
        Assert.That(oldLogin, Does.Contain("邮箱或密码错误"));
        var fresh = factory.CreateClient();
        var newLogin = await AccountApi.Post(fresh, "/api/account/login", new { email = "user@example.com", password = "newpass12" });
        Assert.That((int)newLogin.StatusCode, Is.EqualTo(200));
    }

    [Test]
    public async Task Reset_Unknown_Email_Same_Send_Response()
    {
        await using var factory = new WebAppFactory();
        var client = factory.CreateClient();
        await AccountApi.SeedAccount(factory, "user@example.com", "abc12345");
        var known = await AccountApi.Post(client, "/api/account/send-code", new { email = "user@example.com", scene = "reset" });
        var unknown = await AccountApi.Post(client, "/api/account/send-code", new { email = "none@example.com", scene = "reset" });
        Assert.That((int)known.StatusCode, Is.EqualTo(200));
        Assert.That((int)unknown.StatusCode, Is.EqualTo(200));
        Assert.That(await AccountApi.Body(unknown), Does.Not.Contain("不存在"));
        Assert.That(factory.Services.GetRequiredService<FakeEmailSender>().LastTo, Is.Not.EqualTo("none@example.com"));
    }

    [Test]
    public async Task Reset_Weak_Password_Rejected()
    {
        await using var factory = new WebAppFactory();
        var client = factory.CreateClient();
        await AccountApi.SeedAccount(factory, "user@example.com", "abc12345");
        await AccountApi.Post(client, "/api/account/send-code", new { email = "user@example.com", scene = "reset" });
        var code = Regex.Match(factory.Services.GetRequiredService<FakeEmailSender>().LastBody ?? "", @"\d{6}").Value;
        var body = await AccountApi.Body(await AccountApi.Post(client, "/api/account/reset", new
        {
            email = "user@example.com",
            code,
            password = "abcdefgh",
            confirm = "abcdefgh"
        }));
        Assert.That(body, Does.Contain("密码至少 8 位且同时包含字母和数字"));
        var login = await AccountApi.Post(client, "/api/account/login", new { email = "user@example.com", password = "abc12345" });
        Assert.That((int)login.StatusCode, Is.EqualTo(200));
    }
}
