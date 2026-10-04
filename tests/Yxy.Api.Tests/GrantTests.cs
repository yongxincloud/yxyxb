using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Yxy.Api.Controllers;
using Yxy.Api.Data;

namespace Yxy.Api.Tests;

public sealed class GrantTests
{
    [Test]
    public async Task Grant_Requires_Existing_Account_And_Skips_Commission()
    {
        await using var factory = new WebAppFactory();
        var vendor = factory.CreateClient();
        vendor.DefaultRequestHeaders.Add("X-Vendor-Key", VendorController.VendorKey);
        var missing = await AccountApi.Post(vendor, "/api/vendor/grants", new { email = "none@example.com", days = 30, reason = "赠送" });
        Assert.That(missing.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WebDb>();
            Assert.That(await db.Accounts.CountAsync(), Is.EqualTo(0));
        }

        var user = factory.CreateClient();
        await AccountApi.Register(factory, user, "gift@example.com", "abc12345");
        var granted = await AccountApi.Post(vendor, "/api/vendor/grants", new { email = "gift@example.com", days = 10, reason = "补偿" });
        Assert.That(granted.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        await using var check = factory.Services.CreateAsyncScope();
        var store = check.ServiceProvider.GetRequiredService<WebDb>();
        var auth = await store.SubscriptionAuths.SingleAsync();
        Assert.That(auth.GrantType, Is.EqualTo(2));
        Assert.That(auth.OrderId, Is.Null);
        Assert.That(await store.Orders.CountAsync(), Is.EqualTo(0));
        Assert.That(await store.Commissions.CountAsync(), Is.EqualTo(0));
        Assert.That(await store.OperationLogs.CountAsync(), Is.EqualTo(1));
        Assert.That((auth.AuthEndTime - auth.AuthStartTime).TotalDays, Is.EqualTo(10).Within(0.01));
    }

    [Test]
    public async Task Price_Change_Is_Logged_And_Does_Not_Rewrite_Open_Orders()
    {
        await using var factory = new WebAppFactory();
        var user = factory.CreateClient();
        await AccountApi.Register(factory, user, "price@example.com", "abc12345");
        await AccountApi.Post(user, "/api/account/login", new { email = "price@example.com", password = "abc12345" });
        var created = await user.PostAsJsonAsync("/api/orders", new { productNo = "STD", orderType = 1, channel = "alipay" });
        Assert.That((await created.Content.ReadFromJsonAsync<OrderAmount>())!.Amount, Is.EqualTo(12m));
        var vendor = factory.CreateClient();
        vendor.DefaultRequestHeaders.Add("X-Vendor-Key", VendorController.VendorKey);
        var changed = await AccountApi.Post(vendor, "/api/vendor/products/STD/price", new { price = 120m });
        Assert.That(changed.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var next = await user.PostAsJsonAsync("/api/orders", new { productNo = "STD", orderType = 1, channel = "alipay" });
        Assert.That((await next.Content.ReadFromJsonAsync<OrderAmount>())!.Amount, Is.EqualTo(120m));
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WebDb>();
        var amounts = await db.Orders.OrderBy(x => x.Id).Select(x => x.Amount).ToListAsync();
        Assert.That(amounts, Is.EqualTo(new[] { 12m, 120m }));
        Assert.That(await db.OperationLogs.CountAsync(x => x.Action == "price"), Is.EqualTo(1));
    }

    sealed record OrderAmount(bool Ok, decimal Amount);
}
