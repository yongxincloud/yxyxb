using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Yxy.Api.Controllers;
using Yxy.Api.Data;

namespace Yxy.Api.Tests;

public sealed class CustomerDeleteTests
{
    [Test]
    public async Task Vendor_Can_Delete_Customer_And_Related_Rows()
    {
        await using var factory = new WebAppFactory();
        var user = factory.CreateClient();
        await AccountApi.Register(factory, user, "drop@example.com", "abc12345");
        await AccountApi.Post(user, "/api/account/login", new { email = "drop@example.com", password = "abc12345" });
        await user.PostAsJsonAsync("/api/orders", new { productNo = "STD", orderType = 1, channel = "alipay" });

        long customerId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WebDb>();
            customerId = await db.Customers.Select(x => x.Id).SingleAsync();
            Assert.That(await db.Orders.CountAsync(), Is.EqualTo(1));
        }

        var stranger = factory.CreateClient();
        var denied = await AccountApi.Post(stranger, "/api/vendor/customers/" + customerId + "/delete", new { });
        Assert.That(denied.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

        var vendor = factory.CreateClient();
        vendor.DefaultRequestHeaders.Add("X-Vendor-Key", VendorController.VendorKey);
        var deleted = await AccountApi.Post(vendor, "/api/vendor/customers/" + customerId + "/delete", new { });
        Assert.That(deleted.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        await using var check = factory.Services.CreateAsyncScope();
        var store = check.ServiceProvider.GetRequiredService<WebDb>();
        Assert.That(await store.Accounts.CountAsync(), Is.EqualTo(0));
        Assert.That(await store.Customers.CountAsync(), Is.EqualTo(0));
        Assert.That(await store.Orders.CountAsync(), Is.EqualTo(0));
        Assert.That(await store.SubscriptionAuths.CountAsync(), Is.EqualTo(0));
        Assert.That(await store.OperationLogs.CountAsync(x => x.Action == "delete"), Is.EqualTo(1));

        var again = await AccountApi.Post(vendor, "/api/vendor/customers/" + customerId + "/delete", new { });
        Assert.That(again.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }
}
