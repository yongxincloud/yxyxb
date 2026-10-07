using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Yxy.Api.Controllers;
using Yxy.Api.Data;
using Yxy.Api.Services;

namespace Yxy.Api.Tests;

public sealed class UpgradeOrderTests
{
    [Test]
    public async Task Upgrade_Prorates_And_Keeps_End_Date()
    {
        await using var factory = new WebAppFactory();
        var client = await LoginAsync(factory);
        var first = await Read(await AccountApi.Post(client, "/api/orders", new { productNo = "PERSONAL", orderType = 1, channel = "alipay" }));
        await Notify(client, first.OrderNo!, "U1", "wechat", 36m);

        DateTime oldEnd;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WebDb>();
            var auth = await db.SubscriptionAuths.SingleAsync();
            oldEnd = DateTime.UtcNow.AddDays(182);
            auth.AuthEndTime = oldEnd;
            auth.AuthStartTime = oldEnd.AddDays(-183);
            await db.SaveChangesAsync();
        }

        var preview = await client.GetFromJsonAsync<UpgradePreview>("/api/orders/upgrade-preview?productNo=STANDARD");
        Assert.That(preview!.Ok, Is.True);
        Assert.That(preview.DaysLeft, Is.EqualTo(182).Or.EqualTo(181));
        var expected = decimal.Round(preview.DaysLeft!.Value * (98m - 36m) / 365m, 2, MidpointRounding.AwayFromZero);
        Assert.That(preview.Amount, Is.EqualTo(expected));

        var upgrade = await Read(await AccountApi.Post(client, "/api/orders", new { productNo = "STANDARD", orderType = 3, channel = "alipay" }));
        Assert.That(upgrade.Amount, Is.EqualTo(expected));
        await Notify(client, upgrade.OrderNo!, "U2", "wechat", expected);

        await using var check = factory.Services.CreateAsyncScope();
        var store = check.ServiceProvider.GetRequiredService<WebDb>();
        var auths = await store.SubscriptionAuths.ToListAsync();
        Assert.That(auths.Count, Is.EqualTo(1));
        var product = await store.Products.SingleAsync(x => x.Id == auths[0].ProductId);
        Assert.That(product.ProductNo, Is.EqualTo("STANDARD"));
        Assert.That(auths[0].AuthEndTime, Is.EqualTo(oldEnd).Within(TimeSpan.FromSeconds(1)));
    }

    [Test]
    public async Task Renew_Downgrade_Blocked_When_Too_Many_Devices()
    {
        await using var factory = new WebAppFactory();
        var client = await LoginAsync(factory);
        var first = await Read(await AccountApi.Post(client, "/api/orders", new { productNo = "STANDARD", orderType = 1, channel = "alipay" }));
        await Notify(client, first.OrderNo!, "D1", "wechat", 98m);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WebDb>();
            var account = await db.Accounts.SingleAsync();
            var devices = scope.ServiceProvider.GetRequiredService<DeviceRegistryService>();
            for (var i = 1; i <= 5; i++)
                await devices.TryOccupyAsync(account.Id, "pc-" + i, 10, true);
        }

        var renew = await AccountApi.Post(client, "/api/orders", new { productNo = "PERSONAL", orderType = 1, channel = "alipay" });
        Assert.That(renew.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await AccountApi.Body(renew), Does.Contain("请先减少设备"));
    }

    static async Task<HttpClient> LoginAsync(WebAppFactory factory)
    {
        var client = factory.CreateClient();
        var email = "up" + Guid.NewGuid().ToString("N")[..8] + "@example.com";
        await AccountApi.Register(factory, client, email, "abc12345");
        await AccountApi.Post(client, "/api/account/login", new { email, password = "abc12345" });
        return client;
    }

    static async Task<OrderResult> Read(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<OrderResult>();
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
        return body!;
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
}
