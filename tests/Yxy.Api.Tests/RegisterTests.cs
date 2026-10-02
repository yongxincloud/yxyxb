using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Yxy.Api.Data;

namespace Yxy.Api.Tests;

public sealed class RegisterTests
{
    [Test]
    public async Task Register_Creates_Account_And_Customer()
    {
        await using var factory = new WebAppFactory();
        var client = factory.CreateClient();
        await AccountApi.Register(factory, client, "user@example.com", "abc12345");
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WebDb>();
        var account = await db.Accounts.SingleAsync();
        Assert.That(account.PasswordHash, Is.Not.EqualTo("abc12345"));
        var customer = await db.Customers.SingleAsync();
        Assert.That(customer.DirectProviderId, Is.Null);
        Assert.That(customer.AccountId, Is.EqualTo(account.Id));
    }

    [Test]
    public async Task Duplicate_Email_Is_Rejected()
    {
        await using var factory = new WebAppFactory();
        var client = factory.CreateClient();
        await AccountApi.Register(factory, client, "user@example.com", "abc12345");
        var body = await AccountApi.Body(await AccountApi.Post(client, "/api/account/register", new
        {
            email = "user@example.com",
            password = "abc12345",
            confirm = "abc12345",
            emailCode = "000000"
        }));
        Assert.That(body, Does.Contain("该邮箱已注册"));
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WebDb>();
        Assert.That(await db.Accounts.CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task Weak_Password_Is_Rejected()
    {
        await using var factory = new WebAppFactory();
        var client = factory.CreateClient();
        var code = await AccountApi.SendRegisterCode(factory, client, "user@example.com");
        var body = await AccountApi.Body(await AccountApi.Post(client, "/api/account/register", new
        {
            email = "user@example.com",
            password = "abcdefgh",
            confirm = "abcdefgh",
            emailCode = code
        }));
        Assert.That(body, Does.Contain("密码至少 8 位且同时包含字母和数字"));
    }

    [Test]
    public async Task Provider_No_Binds_Enabled_Provider()
    {
        await using var factory = new WebAppFactory();
        var client = factory.CreateClient();
        var providerId = await AccountApi.SeedProvider(factory, "P001", 1);
        await AccountApi.Register(factory, client, "user@example.com", "abc12345", "P001");
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WebDb>();
        Assert.That((await db.Customers.SingleAsync()).DirectProviderId, Is.EqualTo(providerId));
    }

    [Test]
    public async Task Unknown_Provider_No_Still_Registers()
    {
        await using var factory = new WebAppFactory();
        var client = factory.CreateClient();
        await AccountApi.Register(factory, client, "user@example.com", "abc12345", "NOPE");
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WebDb>();
        Assert.That(await db.Accounts.CountAsync(), Is.EqualTo(1));
        Assert.That((await db.Customers.SingleAsync()).DirectProviderId, Is.Null);
    }

    [Test]
    public async Task Disabled_Provider_Does_Not_Bind()
    {
        await using var factory = new WebAppFactory();
        var client = factory.CreateClient();
        await AccountApi.SeedProvider(factory, "P009", 0);
        await AccountApi.Register(factory, client, "user@example.com", "abc12345", "P009");
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WebDb>();
        Assert.That((await db.Customers.SingleAsync()).DirectProviderId, Is.Null);
    }
}
