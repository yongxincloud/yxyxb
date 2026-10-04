using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Yxy.Api.Data;
using Yxy.Api.Services;

namespace Yxy.Api.Tests;

public sealed class ClientAuthTests
{
    [Test]
    public async Task Password_Login_Issues_Token_And_Does_Not_Echo_Password()
    {
        await using var factory = new WebAppFactory();
        var client = factory.CreateClient();
        await AccountApi.Register(factory, client, "user@example.com", "abc12345");
        await GrantAsync(factory, "user@example.com", DateTime.UtcNow.AddDays(40), 1);

        var body = await Read(await AccountApi.Post(client, "/api/client/login", new
        {
            email = "user@example.com",
            password = "abc12345",
            deviceId = "pc-1"
        }));

        Assert.That(body.Ok, Is.True);
        Assert.That(body.Valid, Is.True);
        Assert.That(body.Token, Is.Not.Null.And.Not.EqualTo("abc12345"));
        Assert.That(body.AuthStatus, Is.EqualTo("active"));
        Assert.That(body.NextCheckDays, Is.EqualTo(30));
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WebDb>();
        var stored = await db.AuthTokens.SingleAsync();
        Assert.That(stored.TokenHash, Is.Not.EqualTo(body.Token));
        Assert.That(stored.DeviceId, Is.EqualTo("pc-1"));
        Assert.That((stored.ExpiresAt - stored.IssuedAt).TotalDays, Is.EqualTo(44).Within(0.01));
        Assert.That(body.Name, Is.Null.Or.Empty);
        Assert.That(body.CompanyName, Is.Null.Or.Empty);
    }

    [Test]
    public async Task Login_Shows_Company_And_Person_After_Profile_Is_Filled()
    {
        await using var factory = new WebAppFactory();
        var client = factory.CreateClient();
        await AccountApi.Register(factory, client, "named@example.com", "abc12345");
        await AccountApi.Post(client, "/api/account/login", new { email = "named@example.com", password = "abc12345" });
        var saved = await AccountApi.Post(client, "/api/account/profile", new
        {
            name = "张三",
            nickname = "小张",
            companyName = "甲公司"
        });
        Assert.That(saved.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await Read(await AccountApi.Post(client, "/api/client/login", new
        {
            email = "named@example.com",
            password = "abc12345",
            deviceId = "pc-1"
        }));
        Assert.That(body.Name, Is.EqualTo("张三"));
        Assert.That(body.Nickname, Is.EqualTo("小张"));
        Assert.That(body.CompanyName, Is.EqualTo("甲公司"));
    }

    [Test]
    public async Task Short_Remaining_Uses_Seven_Day_Check()
    {
        await using var factory = new WebAppFactory();
        var client = factory.CreateClient();
        await AccountApi.Register(factory, client, "user@example.com", "abc12345");
        await GrantAsync(factory, "user@example.com", DateTime.UtcNow.AddDays(10), 1);
        var body = await Read(await AccountApi.Post(client, "/api/client/login", new
        {
            email = "user@example.com",
            password = "abc12345",
            deviceId = "pc-1"
        }));
        Assert.That(body.NextCheckDays, Is.EqualTo(7));
        Assert.That(body.Valid, Is.True);
    }

    [Test]
    public async Task Grace_Stays_Valid_And_Past_Grace_Does_Not()
    {
        await using var graceFactory = new WebAppFactory();
        var graceClient = graceFactory.CreateClient();
        await AccountApi.Register(graceFactory, graceClient, "grace@example.com", "abc12345");
        await GrantAsync(graceFactory, "grace@example.com", DateTime.UtcNow.AddDays(-3), 1);
        var grace = await Read(await AccountApi.Post(graceClient, "/api/client/login", new
        {
            email = "grace@example.com",
            password = "abc12345",
            deviceId = "pc-1"
        }));
        Assert.That(grace.AuthStatus, Is.EqualTo("grace"));
        Assert.That(grace.Valid, Is.True);

        await using var expiredFactory = new WebAppFactory();
        var expiredClient = expiredFactory.CreateClient();
        await AccountApi.Register(expiredFactory, expiredClient, "old@example.com", "abc12345");
        await GrantAsync(expiredFactory, "old@example.com", DateTime.UtcNow.AddDays(-8), 1);
        var expired = await Read(await AccountApi.Post(expiredClient, "/api/client/login", new
        {
            email = "old@example.com",
            password = "abc12345",
            deviceId = "pc-1"
        }));
        Assert.That(expired.AuthStatus, Is.EqualTo("expired"));
        Assert.That(expired.Valid, Is.False);
        Assert.That(expired.Token, Is.Not.Null);
    }

    [Test]
    public async Task Paused_Auth_Is_Not_Valid()
    {
        await using var factory = new WebAppFactory();
        var client = factory.CreateClient();
        await AccountApi.Register(factory, client, "user@example.com", "abc12345");
        await GrantAsync(factory, "user@example.com", DateTime.UtcNow.AddDays(40), 3);
        var body = await Read(await AccountApi.Post(client, "/api/client/login", new
        {
            email = "user@example.com",
            password = "abc12345",
            deviceId = "pc-1"
        }));
        Assert.That(body.AuthStatus, Is.EqualTo("paused"));
        Assert.That(body.Valid, Is.False);
    }

    [Test]
    public async Task Verify_Issues_New_Token_And_Keeps_Old_One()
    {
        await using var factory = new WebAppFactory();
        var client = factory.CreateClient();
        await AccountApi.Register(factory, client, "user@example.com", "abc12345");
        var first = await Read(await AccountApi.Post(client, "/api/client/login", new
        {
            email = "user@example.com",
            password = "abc12345",
            deviceId = "pc-1"
        }));
        var second = await Read(await AccountApi.Post(client, "/api/client/verify", new
        {
            token = first.Token,
            deviceId = "pc-1"
        }));
        Assert.That(second.Token, Is.Not.EqualTo(first.Token));
        var oldStillWorks = await AccountApi.Post(client, "/api/client/verify", new { token = first.Token, deviceId = "pc-2" });
        Assert.That(oldStillWorks.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Two_Devices_Can_Both_Login()
    {
        await using var factory = new WebAppFactory();
        var client = factory.CreateClient();
        await AccountApi.Register(factory, client, "user@example.com", "abc12345");
        var a = await Read(await AccountApi.Post(client, "/api/client/login", new
        {
            email = "user@example.com",
            password = "abc12345",
            deviceId = "pc-a"
        }));
        var b = await Read(await AccountApi.Post(client, "/api/client/login", new
        {
            email = "user@example.com",
            password = "abc12345",
            deviceId = "pc-b"
        }));
        Assert.That((await AccountApi.Post(client, "/api/client/verify", new { token = a.Token, deviceId = "pc-a" })).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await AccountApi.Post(client, "/api/client/verify", new { token = b.Token, deviceId = "pc-b" })).StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Renew_Returns_New_Token()
    {
        await using var factory = new WebAppFactory();
        var client = factory.CreateClient();
        await AccountApi.Register(factory, client, "user@example.com", "abc12345");
        var login = await Read(await AccountApi.Post(client, "/api/client/login", new
        {
            email = "user@example.com",
            password = "abc12345",
            deviceId = "pc-1"
        }));
        var renewed = await Read(await AccountApi.Post(client, "/api/client/renew", new { token = login.Token }));
        Assert.That(renewed.Ok, Is.True);
        Assert.That(renewed.Token, Is.Not.EqualTo(login.Token));
    }

    [Test]
    public async Task Reset_Password_Revokes_Client_Tokens()
    {
        await using var factory = new WebAppFactory();
        var client = factory.CreateClient();
        await AccountApi.Register(factory, client, "user@example.com", "abc12345");
        var login = await Read(await AccountApi.Post(client, "/api/client/login", new
        {
            email = "user@example.com",
            password = "abc12345",
            deviceId = "pc-1"
        }));
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WebDb>();
            var now = DateTime.UtcNow;
            db.EmailCodes.Add(new EmailCodeRow
            {
                Email = "user@example.com",
                Code = "654321",
                Scene = "reset",
                Used = 0,
                ExpiresAt = now.AddMinutes(5),
                CreatedAt = now
            });
            await db.SaveChangesAsync();
        }
        var reset = await AccountApi.Post(client, "/api/account/reset", new
        {
            email = "user@example.com",
            code = "654321",
            password = "newpass12",
            confirm = "newpass12"
        });
        Assert.That(reset.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var verify = await AccountApi.Post(client, "/api/client/verify", new { token = login.Token, deviceId = "pc-1" });
        Assert.That(verify.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Code_Login_Issues_Token()
    {
        await using var factory = new WebAppFactory();
        var client = factory.CreateClient();
        await AccountApi.SeedAccount(factory, "user@example.com", "abc12345");
        await AccountApi.Post(client, "/api/account/send-code", new { email = "user@example.com", scene = "login" });
        var code = Regex.Match(factory.Services.GetRequiredService<FakeEmailSender>().LastBody ?? "", @"\d{6}").Value;
        var body = await Read(await AccountApi.Post(client, "/api/client/login-code", new
        {
            email = "user@example.com",
            code,
            deviceId = "pc-1"
        }));
        Assert.That(body.Ok, Is.True);
        Assert.That(body.Token, Is.Not.Empty);
    }

    static async Task<ClientSession> Read(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<ClientSession>();
        Assert.That(body, Is.Not.Null);
        return body!;
    }

    static async Task GrantAsync(WebAppFactory factory, string email, DateTime end, int status)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WebDb>();
        var account = await db.Accounts.SingleAsync(x => x.Email == email);
        var customer = await db.Customers.SingleAsync(x => x.AccountId == account.Id);
        var now = DateTime.UtcNow;
        db.SubscriptionAuths.Add(new SubscriptionAuth
        {
            CustomerId = customer.Id,
            ProductId = 1,
            AuthStartTime = now,
            AuthEndTime = end,
            Status = status,
            GrantType = 2,
            CreatedAt = now,
            UpdatedAt = now
        });
        await db.SaveChangesAsync();
    }
}
