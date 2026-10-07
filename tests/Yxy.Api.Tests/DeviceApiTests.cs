using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Yxy.Api.Data;
using Yxy.Api.Services;

namespace Yxy.Api.Tests;

public sealed class DeviceApiTests
{
    [Test]
    public async Task Portal_Can_List_And_Kick_Device()
    {
        await using var factory = new WebAppFactory();
        var client = factory.CreateClient();
        await AccountApi.Register(factory, client, "portal-dev@example.com", "abc12345");
        await AccountApi.Post(client, "/api/account/login", new { email = "portal-dev@example.com", password = "abc12345" });
        await GrantPersonal(factory, "portal-dev@example.com");

        await AccountApi.Post(client, "/api/client/login", new
        {
            email = "portal-dev@example.com",
            password = "abc12345",
            deviceId = "pc-kick"
        });

        var list = await client.GetFromJsonAsync<DevicesDto>("/api/portal/devices");
        Assert.That(list!.Devices.Any(d => d.DeviceId == "pc-kick"), Is.True);
        Assert.That(list.MaxDevices, Is.EqualTo(3));

        var kicked = await AccountApi.Post(client, "/api/portal/devices/kick", new { deviceId = "pc-kick" });
        Assert.That(kicked.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var after = await client.GetFromJsonAsync<DevicesDto>("/api/portal/devices");
        Assert.That(after!.Devices, Is.Empty);
    }

    [Test]
    public async Task Client_Kick_Invalidates_Token()
    {
        await using var factory = new WebAppFactory();
        var client = factory.CreateClient();
        await AccountApi.Register(factory, client, "cli-dev@example.com", "abc12345");
        await GrantPersonal(factory, "cli-dev@example.com");
        var login = await (await AccountApi.Post(client, "/api/client/login", new
        {
            email = "cli-dev@example.com",
            password = "abc12345",
            deviceId = "pc-1"
        })).Content.ReadFromJsonAsync<ClientSession>();

        var kick = await AccountApi.Post(client, "/api/client/devices/kick", new { token = login!.Token, deviceId = "pc-1" });
        Assert.That(kick.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var verify = await AccountApi.Post(client, "/api/client/verify", new { token = login.Token, deviceId = "pc-1" });
        Assert.That(verify.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    static async Task GrantPersonal(WebAppFactory factory, string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WebDb>();
        await scope.ServiceProvider.GetRequiredService<OrderService>().EnsureCatalogAsync();
        var product = await db.Products.SingleAsync(x => x.ProductNo == OrderService.PersonalProductNo);
        var account = await db.Accounts.SingleAsync(x => x.Email == email);
        var customer = await db.Customers.SingleAsync(x => x.AccountId == account.Id);
        var now = DateTime.UtcNow;
        db.SubscriptionAuths.Add(new SubscriptionAuth
        {
            CustomerId = customer.Id,
            ProductId = product.Id,
            AuthStartTime = now,
            AuthEndTime = now.AddDays(30),
            Status = 1,
            GrantType = 2,
            CreatedAt = now,
            UpdatedAt = now
        });
        await db.SaveChangesAsync();
    }

    sealed record DevicesDto(bool Ok, int? MaxDevices, DeviceItem[] Devices);
    sealed record DeviceItem(string DeviceId);
}
