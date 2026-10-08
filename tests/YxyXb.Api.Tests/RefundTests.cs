using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using YxyXb.Api.Controllers;
using YxyXb.Api.Data;
using YxyXb.Api.Services;

namespace YxyXb.Api.Tests;

public sealed class RefundTests
{
    [Test]
    public async Task Refund_Within_Seven_Days_Cancels_Auth_And_Writes_Negative_Commission()
    {
        await using var factory = new WebAppFactory();
        var vendor = factory.CreateClient();
        vendor.DefaultRequestHeaders.Add("X-Vendor-Key", VendorController.VendorKey);
        var level = await AccountApi.Post(vendor, "/api/vendor/levels", new { name = "退款级", rate = 0.50m });
        var levelId = (await level.Content.ReadFromJsonAsync<LevelCreated>())!.Id;
        var shop = factory.CreateClient();
        await AccountApi.Register(factory, shop, "refund-shop@example.com", "abc12345");
        await AccountApi.Post(shop, "/api/account/login", new { email = "refund-shop@example.com", password = "abc12345" });
        await AccountApi.Post(shop, "/api/portal/provider-apply", new
        {
            companyName = "退款公司",
            taxNo = "91310000",
            licenseFile = "license.png",
            contactName = "赵六",
            contactEmail = "g@h.com",
            contactPhone = "13600000000"
        });
        long applyId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WebDb>();
            applyId = (await db.ProviderApplies.SingleAsync()).Id;
        }
        var approved = await AccountApi.Post(vendor, "/api/vendor/provider-applies/" + applyId + "/approve", new { levelId });
        var providerNo = (await approved.Content.ReadFromJsonAsync<Approved>())!.ProviderNo;

        var buyer = factory.CreateClient();
        await AccountApi.Register(factory, buyer, "refund-buyer@example.com", "abc12345", providerNo);
        await AccountApi.Post(buyer, "/api/account/login", new { email = "refund-buyer@example.com", password = "abc12345" });
        var created = await buyer.PostAsJsonAsync("/api/orders", new { productNo = "STANDARD", orderType = 1, channel = "alipay" });
        var order = await created.Content.ReadFromJsonAsync<OrderResult>();
        await Notify(buyer, order!.OrderNo!, "RF1", "wechat", 98m);
        var refund = await AccountApi.Post(buyer, "/api/orders/" + order.OrderNo + "/refund", new { });
        Assert.That(refund.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var again = await AccountApi.Post(buyer, "/api/orders/" + order.OrderNo + "/refund", new { });
        Assert.That(again.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        await using var check = factory.Services.CreateAsyncScope();
        var store = check.ServiceProvider.GetRequiredService<WebDb>();
        Assert.That((await store.SubscriptionAuths.SingleAsync()).Status, Is.EqualTo(0));
        var rows = await store.Commissions.OrderBy(x => x.EntryType).ToListAsync();
        Assert.That(rows.Count, Is.EqualTo(2));
        Assert.That(rows[0].Amount, Is.EqualTo(49m));
        Assert.That(rows[1].Amount, Is.EqualTo(-49m));
        Assert.That(rows[1].Settled, Is.EqualTo(0));
    }

    [Test]
    public async Task Refund_After_Seven_Days_Is_Rejected()
    {
        await using var factory = new WebAppFactory();
        var buyer = factory.CreateClient();
        await AccountApi.Register(factory, buyer, "late-refund@example.com", "abc12345");
        await AccountApi.Post(buyer, "/api/account/login", new { email = "late-refund@example.com", password = "abc12345" });
        var created = await buyer.PostAsJsonAsync("/api/orders", new { productNo = "STANDARD", orderType = 1, channel = "alipay" });
        var order = await created.Content.ReadFromJsonAsync<OrderResult>();
        await Notify(buyer, order!.OrderNo!, "RF2", "alipay", 98m);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WebDb>();
            var row = await db.Orders.SingleAsync();
            row.PayTime = DateTime.UtcNow.AddDays(-8);
            await db.SaveChangesAsync();
        }
        var refund = await AccountApi.Post(buyer, "/api/orders/" + order.OrderNo + "/refund", new { });
        Assert.That(await AccountApi.Body(refund), Does.Contain("已超过 7 天退款窗口"));
        await using var check = factory.Services.CreateAsyncScope();
        var store = check.ServiceProvider.GetRequiredService<WebDb>();
        Assert.That((await store.SubscriptionAuths.SingleAsync()).Status, Is.EqualTo(1));
    }

    static Task<HttpResponseMessage> Notify(HttpClient client, string orderNo, string tradeNo, string channel, decimal amount)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/payments/notify")
        {
            Content = JsonContent.Create(new { orderNo, tradeNo, channel, amount })
        };
        req.Headers.Add("X-Pay-Sign", OrderService.Sign(OrderController.DevPaySecret, orderNo, tradeNo, channel, amount));
        return client.SendAsync(req);
    }

    sealed record Approved(bool Ok, string ProviderNo);
    sealed record LevelCreated(bool Ok, long Id);
}
