using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Yxy.Api.Data;
using Yxy.Api.Services;

namespace Yxy.Api.Tests;

public static class AccountApi
{
    public static async Task<HttpResponseMessage> Post(HttpClient client, string path, object body, string? ip = null)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        if (ip != null) req.Headers.Add("X-Test-Ip", ip);
        return await client.SendAsync(req);
    }

    public static async Task<string> SendRegisterCode(WebAppFactory factory, HttpClient client, string email)
    {
        var captcha = await client.GetFromJsonAsync<CaptchaDto>("/api/account/captcha");
        var send = await Post(client, "/api/account/send-code", new
        {
            email,
            scene = "register",
            captchaId = captcha!.Id,
            captchaAnswer = "0000"
        }, "10.0.0." + Random.Shared.Next(1, 200));
        AssertOk(send);
        var sender = factory.Services.GetRequiredService<FakeEmailSender>();
        var code = Regex.Match(sender.LastBody ?? "", @"\d{6}").Value;
        Assert.That(code, Is.Not.Empty);
        return code;
    }

    public static async Task Register(WebAppFactory factory, HttpClient client, string email, string password, string? providerNo = null)
    {
        var code = await SendRegisterCode(factory, client, email);
        var response = await Post(client, "/api/account/register", new
        {
            email,
            password,
            confirm = password,
            emailCode = code,
            providerNo
        });
        AssertOk(response);
    }

    public static async Task SeedAccount(WebAppFactory factory, string email, string password)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WebDb>();
        var now = DateTime.UtcNow;
        var account = new Account
        {
            Email = email,
            PasswordHash = new BcryptPasswordHasher().Hash(password),
            Status = 1,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        db.Customers.Add(new Customer
        {
            CustomerNo = "C" + Guid.NewGuid().ToString("N")[..12],
            AccountId = account.Id,
            Status = 1,
            CreatedAt = now,
            UpdatedAt = now
        });
        await db.SaveChangesAsync();
    }

    public static async Task<long> SeedProvider(WebAppFactory factory, string providerNo, int status)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WebDb>();
        var now = DateTime.UtcNow;
        var level = new ProviderLevel
        {
            Name = "L-" + providerNo,
            CommissionRate = 0.5m,
            Status = 1,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.ProviderLevels.Add(level);
        await db.SaveChangesAsync();
        var provider = new Provider
        {
            ProviderNo = providerNo,
            AccountId = 900000 + providerNo.Length + status,
            Name = providerNo,
            LevelId = level.Id,
            Status = status,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Providers.Add(provider);
        await db.SaveChangesAsync();
        return provider.Id;
    }

    public static async Task<string> Body(HttpResponseMessage response) => await response.Content.ReadAsStringAsync();

    static void AssertOk(HttpResponseMessage response)
    {
        Assert.That((int)response.StatusCode, Is.EqualTo(200));
    }

    sealed record CaptchaDto(string Id, string Svg);
}
