using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Yxy.Api.Data;

namespace Yxy.Api.Tests;

public sealed class PortalTests
{
    [Test]
    public async Task Home_Price_Comes_From_Database()
    {
        await using var factory = new WebAppFactory();
        var body = await factory.CreateClient().GetFromJsonAsync<PriceDto>("/api/public/price");
        Assert.That(body!.Price, Is.EqualTo(12m));
    }

    [Test]
    public async Task Portal_Shows_No_Auth_Before_Payment()
    {
        await using var factory = new WebAppFactory();
        var client = factory.CreateClient();
        var email = "portal@example.com";
        await AccountApi.Register(factory, client, email, "abc12345");
        var login = await AccountApi.Post(client, "/api/account/login", new { email, password = "abc12345" });
        Assert.That(login.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var sub = await client.GetFromJsonAsync<SubDto>("/api/portal/subscription");
        Assert.That(sub!.AuthStatus, Is.EqualTo("none"));
        Assert.That(sub.Usable, Is.False);
    }

    [Test]
    public async Task Provider_Apply_Requires_Every_Field()
    {
        await using var factory = new WebAppFactory();
        var client = factory.CreateClient();
        await AccountApi.Register(factory, client, "apply@example.com", "abc12345");
        await AccountApi.Post(client, "/api/account/login", new { email = "apply@example.com", password = "abc12345" });
        var missing = await AccountApi.Post(client, "/api/portal/provider-apply", new { companyName = "甲公司" });
        Assert.That(missing.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var ok = await AccountApi.Post(client, "/api/portal/provider-apply", new
        {
            companyName = "甲公司",
            taxNo = "91310000",
            licenseFile = "license.png",
            contactName = "张三",
            contactEmail = "a@b.com",
            contactPhone = "13800000000"
        });
        Assert.That(ok.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WebDb>();
        Assert.That(await db.ProviderApplies.CountAsync(), Is.EqualTo(1));
    }

    sealed record PriceDto(bool Ok, decimal Price, string Name);
    sealed record SubDto(bool Ok, string AuthStatus, bool Usable);
}
