using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using YxyXb.Api.Controllers;
using YxyXb.Api.Data;

namespace YxyXb.Api.Tests;

public sealed class AdminFinishTests
{
    [Test]
    public async Task Freeze_Returns_Customers_And_Grant_Writes_Message()
    {
        await using var factory = new WebAppFactory();
        var vendor = factory.CreateClient();
        vendor.DefaultRequestHeaders.Add("X-Vendor-Key", VendorController.VendorKey);
        var level = await AccountApi.Post(vendor, "/api/vendor/levels", new { name = "收尾级", rate = 0.50m });
        var levelId = (await level.Content.ReadFromJsonAsync<LevelCreated>())!.Id;
        var updated = await AccountApi.Post(vendor, "/api/vendor/levels/" + levelId, new { name = "收尾级", rate = 0.40m });
        Assert.That(updated.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var shop = factory.CreateClient();
        await AccountApi.Register(factory, shop, "finish-shop@example.com", "abc12345");
        await AccountApi.Post(shop, "/api/account/login", new { email = "finish-shop@example.com", password = "abc12345" });
        await AccountApi.Post(shop, "/api/portal/provider-apply", new
        {
            companyName = "收尾公司",
            taxNo = "91310000",
            licenseFile = "license.png",
            contactName = "钱七",
            contactEmail = "i@j.com",
            contactPhone = "13500000000"
        });
        long applyId;
        long providerId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WebDb>();
            applyId = (await db.ProviderApplies.SingleAsync()).Id;
        }
        var approved = await AccountApi.Post(vendor, "/api/vendor/provider-applies/" + applyId + "/approve", new { levelId });
        Assert.That(approved.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var buyer = factory.CreateClient();
        var providerNo = (await approved.Content.ReadFromJsonAsync<Approved>())!.ProviderNo;
        await AccountApi.Register(factory, buyer, "finish-buyer@example.com", "abc12345", providerNo);
        await AccountApi.Post(vendor, "/api/vendor/grants", new { email = "finish-buyer@example.com", days = 15, reason = "补偿" });
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WebDb>();
            providerId = (await db.Providers.SingleAsync()).Id;
            Assert.That(await db.Messages.CountAsync(x => x.Title == "授权已发放"), Is.EqualTo(1));
        }
        var frozen = await AccountApi.Post(vendor, "/api/vendor/providers/" + providerId + "/freeze", new { });
        Assert.That(frozen.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        await using var check = factory.Services.CreateAsyncScope();
        var store = check.ServiceProvider.GetRequiredService<WebDb>();
        Assert.That((await store.Providers.SingleAsync()).Status, Is.EqualTo(2));
        Assert.That(await store.Customers.CountAsync(x => x.DirectProviderId != null), Is.EqualTo(0));
        Assert.That(await store.OperationLogs.CountAsync(x => x.Action == "rate"), Is.EqualTo(1));
    }

    sealed record LevelCreated(bool Ok, long Id);
    sealed record Approved(bool Ok, string ProviderNo);
}
