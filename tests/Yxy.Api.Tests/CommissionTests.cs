using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Yxy.Api.Controllers;
using Yxy.Api.Data;
using Yxy.Api.Services;

namespace Yxy.Api.Tests;

public sealed class CommissionTests
{
    [Test]
    public async Task Paid_Order_Writes_Commission_Once()
    {
        await using var factory = new WebAppFactory();
        var vendor = Vendor(factory);
        var levelId = await CreateLevel(vendor, "金牌", 0.60m);
        var providerClient = factory.CreateClient();
        await AccountApi.Register(factory, providerClient, "shop@example.com", "abc12345");
        await AccountApi.Post(providerClient, "/api/account/login", new { email = "shop@example.com", password = "abc12345" });
        var apply = await AccountApi.Post(providerClient, "/api/portal/provider-apply", new
        {
            companyName = "甲公司",
            taxNo = "91310000",
            licenseFile = "license.png",
            contactName = "张三",
            contactEmail = "a@b.com",
            contactPhone = "13800000000"
        });
        var applyId = await ApplyId(factory);
        var approved = await AccountApi.Post(vendor, "/api/vendor/provider-applies/" + applyId + "/approve", new { levelId });
        var providerNo = (await approved.Content.ReadFromJsonAsync<Approved>())!.ProviderNo;

        var buyer = factory.CreateClient();
        await AccountApi.Register(factory, buyer, "buyer@example.com", "abc12345", providerNo);
        await AccountApi.Post(buyer, "/api/account/login", new { email = "buyer@example.com", password = "abc12345" });
        var created = await buyer.PostAsJsonAsync("/api/orders", new { productNo = "STD", orderType = 1, channel = "alipay" });
        var order = await created.Content.ReadFromJsonAsync<OrderResult>();
        await Notify(buyer, order!.OrderNo!, "C1", "wechat", 12m);
        await Notify(buyer, order.OrderNo!, "C1", "wechat", 12m);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WebDb>();
        var row = await db.Commissions.SingleAsync();
        Assert.That(row.Amount, Is.EqualTo(7.20m));
        Assert.That(12m - row.Amount, Is.EqualTo(4.80m));
    }

    [Test]
    public async Task Trial_And_Disabled_Provider_Do_Not_Earn_Commission()
    {
        await using var factory = new WebAppFactory();
        var vendor = Vendor(factory);
        var levelId = await CreateLevel(vendor, "银牌", 0.50m);
        var providerClient = factory.CreateClient();
        await AccountApi.Register(factory, providerClient, "shop2@example.com", "abc12345");
        await AccountApi.Post(providerClient, "/api/account/login", new { email = "shop2@example.com", password = "abc12345" });
        await AccountApi.Post(providerClient, "/api/portal/provider-apply", new
        {
            companyName = "乙公司",
            taxNo = "91310001",
            licenseFile = "license.jpg",
            contactName = "李四",
            contactEmail = "c@d.com",
            contactPhone = "13900000000"
        });
        var applyId = await ApplyId(factory);
        var approved = await AccountApi.Post(vendor, "/api/vendor/provider-applies/" + applyId + "/approve", new { levelId });
        var providerNo = (await approved.Content.ReadFromJsonAsync<Approved>())!.ProviderNo;
        var buyer = factory.CreateClient();
        await AccountApi.Register(factory, buyer, "buyer2@example.com", "abc12345", providerNo);
        await AccountApi.Post(buyer, "/api/account/login", new { email = "buyer2@example.com", password = "abc12345" });
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WebDb>();
            var provider = await db.Providers.SingleAsync();
            provider.Status = 0;
            await db.SaveChangesAsync();
        }
        var trial = await buyer.PostAsJsonAsync("/api/orders", new { productNo = "STD", orderType = 2, channel = "alipay" });
        var trialOrder = await trial.Content.ReadFromJsonAsync<OrderResult>();
        await Notify(buyer, trialOrder!.OrderNo!, "T1", "alipay", 0.10m);
        var formal = await buyer.PostAsJsonAsync("/api/orders", new { productNo = "STD", orderType = 1, channel = "alipay" });
        var formalOrder = await formal.Content.ReadFromJsonAsync<OrderResult>();
        await Notify(buyer, formalOrder!.OrderNo!, "F1", "wechat", 12m);

        await using var check = factory.Services.CreateAsyncScope();
        var store = check.ServiceProvider.GetRequiredService<WebDb>();
        Assert.That(await store.Commissions.CountAsync(), Is.EqualTo(0));
    }

    static HttpClient Vendor(WebAppFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Vendor-Key", VendorController.VendorKey);
        return client;
    }

    static async Task<long> CreateLevel(HttpClient vendor, string name, decimal rate)
    {
        var response = await AccountApi.Post(vendor, "/api/vendor/levels", new { name, rate });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        return (await response.Content.ReadFromJsonAsync<LevelCreated>())!.Id;
    }

    static async Task<long> ApplyId(WebAppFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WebDb>();
        return (await db.ProviderApplies.OrderByDescending(x => x.Id).FirstAsync()).Id;
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
