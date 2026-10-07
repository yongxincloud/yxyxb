using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Yxy.Api.Controllers;
using Yxy.Api.Data;
using Yxy.Api.Services;

namespace Yxy.Api.Tests;

public sealed class SettlementTests
{
    [Test]
    public async Task Settlement_Requires_Invoice_And_One_Hundred()
    {
        await using var factory = new WebAppFactory();
        var vendor = factory.CreateClient();
        vendor.DefaultRequestHeaders.Add("X-Vendor-Key", VendorController.VendorKey);
        var level = await AccountApi.Post(vendor, "/api/vendor/levels", new { name = "结算级", rate = 0.60m });
        var levelId = (await level.Content.ReadFromJsonAsync<LevelCreated>())!.Id;
        var shop = factory.CreateClient();
        await AccountApi.Register(factory, shop, "settle@example.com", "abc12345");
        await AccountApi.Post(shop, "/api/account/login", new { email = "settle@example.com", password = "abc12345" });
        await AccountApi.Post(shop, "/api/portal/provider-apply", new
        {
            companyName = "结算公司",
            taxNo = "91310000",
            licenseFile = "license.png",
            contactName = "王五",
            contactEmail = "e@f.com",
            contactPhone = "13700000000"
        });
        long applyId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WebDb>();
            applyId = (await db.ProviderApplies.SingleAsync()).Id;
        }
        await AccountApi.Post(vendor, "/api/vendor/provider-applies/" + applyId + "/approve", new { levelId });

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WebDb>();
            var product = await db.Products.SingleAsync(x => x.ProductNo == "STANDARD");
            product.Price = 200m;
            await db.SaveChangesAsync();
        }
        var created = await shop.PostAsJsonAsync("/api/orders", new { productNo = "STANDARD", orderType = 1, channel = "alipay" });
        var order = await created.Content.ReadFromJsonAsync<OrderResult>();
        await Notify(shop, order!.OrderNo!, "SET1", "wechat", 200m);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WebDb>();
            var row = await db.Commissions.SingleAsync();
            var lastMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(-1).AddDays(2);
            row.CreatedAt = lastMonth;
            await db.SaveChangesAsync();
        }

        var noInvoice = await AccountApi.Post(shop, "/api/portal/settlements", new { invoiceFile = "" });
        Assert.That(noInvoice.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var started = await AccountApi.Post(shop, "/api/portal/settlements", new { invoiceFile = "invoice.pdf" });
        Assert.That(started.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var again = await AccountApi.Post(shop, "/api/portal/settlements", new { invoiceFile = "invoice.pdf" });
        Assert.That(await AccountApi.Body(again), Does.Contain("没有可结算记录"));

        long settlementId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WebDb>();
            var settlement = await db.Settlements.SingleAsync();
            Assert.That(settlement.Amount, Is.EqualTo(120m));
            Assert.That((await db.Commissions.SingleAsync()).Settled, Is.EqualTo(1));
            settlementId = settlement.Id;
        }
        var approved = await AccountApi.Post(vendor, "/api/vendor/settlements/" + settlementId + "/approve", new { });
        Assert.That(approved.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var paid = await AccountApi.Post(vendor, "/api/vendor/settlements/" + settlementId + "/pay", new { voucherNo = "PAY001" });
        Assert.That(paid.StatusCode, Is.EqualTo(HttpStatusCode.OK));
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

    sealed record LevelCreated(bool Ok, long Id);
}
