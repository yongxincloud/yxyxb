using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Yxy.Api.Data;
using Yxy.Api.Services;

namespace Yxy.Api.Tests;

public sealed class CodeLoginTests
{
    [Test]
    public async Task Code_Login_Succeeds()
    {
        await using var factory = new WebAppFactory();
        var client = factory.CreateClient();
        await AccountApi.SeedAccount(factory, "user@example.com", "abc12345");
        await AccountApi.Post(client, "/api/account/send-code", new { email = "user@example.com", scene = "login" });
        var code = Regex.Match(factory.Services.GetRequiredService<FakeEmailSender>().LastBody ?? "", @"\d{6}").Value;
        var login = await AccountApi.Post(client, "/api/account/login-code", new { email = "user@example.com", code });
        Assert.That((int)login.StatusCode, Is.EqualTo(200));
        var me = await client.GetFromJsonAsync<MeDto>("/api/account/me");
        Assert.That(me!.Email, Is.EqualTo("user@example.com"));
    }

    [Test]
    public async Task Code_Is_Rejected_After_Five_Minutes()
    {
        await using var factory = new WebAppFactory();
        var client = factory.CreateClient();
        await AccountApi.SeedAccount(factory, "user@example.com", "abc12345");
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WebDb>();
        db.EmailCodes.Add(new EmailCodeRow
        {
            Email = "user@example.com",
            Code = "123456",
            Scene = "login",
            Used = 0,
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1),
            CreatedAt = DateTime.UtcNow.AddMinutes(-6)
        });
        await db.SaveChangesAsync();
        var body = await AccountApi.Body(await AccountApi.Post(client, "/api/account/login-code", new { email = "user@example.com", code = "123456" }));
        Assert.That(body, Does.Contain("邮箱或验证码错误"));
    }

    [Test]
    public async Task Missing_Account_Uses_Same_Message()
    {
        await using var factory = new WebAppFactory();
        var client = factory.CreateClient();
        var body = await AccountApi.Body(await AccountApi.Post(client, "/api/account/login-code", new { email = "none@example.com", code = "000000" }));
        Assert.That(body, Does.Contain("邮箱或验证码错误"));
        Assert.That(body, Does.Not.Contain("不存在"));
    }

    [Test]
    public async Task SendCode_Second_Time_Within_60_Seconds()
    {
        await using var factory = new WebAppFactory();
        var client = factory.CreateClient();
        await AccountApi.SeedAccount(factory, "user@example.com", "abc12345");
        await AccountApi.Post(client, "/api/account/send-code", new { email = "user@example.com", scene = "login" });
        var body = await AccountApi.Body(await AccountApi.Post(client, "/api/account/send-code", new { email = "user@example.com", scene = "login" }));
        Assert.That(body, Does.Contain("发送过于频繁"));
    }

    sealed record MeDto(bool Ok, string? Error, string? Email);
}
