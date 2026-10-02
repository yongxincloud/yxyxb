using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Yxy.Api.Controllers;
using Yxy.Api.Data;
using Yxy.Api.Services;

namespace Yxy.Api.Tests;

public sealed class OrderTests
{
    [Test]
    public async Task Anonymous_Order_Is_Rejected()
    {
        await using var factory = new WebAppFactory();
        var response = await AccountApi.Post(factory.CreateClient(), "/api/orders", new { productNo = "STD", orderType = 1, channel = "wechat" });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Pay_Creates_Auth_And_Repeat_Callback_Does_Not()
    {
        await using var factory = new WebAppFactory();
        var client = await LoginAsync(factory);
        var created = await Read(await AccountApi.Post(client, "/api/orders", new { productNo = "STD", orderType = 1, channel = "wechat" }));
        Assert.That(created.Amount, Is.EqualTo(12m));
        var first = await Notify(client, created.OrderNo!, "T1", "wechat", 12m);
        var second = await Notify(client, created.OrderNo!, "T1", "wechat", 12m);
        Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WebDb>();
        Assert.That(await db.SubscriptionAuths.CountAsync(), Is.EqualTo(1));
        Assert.That(await db.Payments.CountAsync(), Is.EqualTo(1));
        var end = await db.SubscriptionAuths.SingleAsync();
        Assert.That((end.AuthEndTime - end.AuthStartTime).TotalDays, Is.EqualTo(365).Within(0.01));
    }

    [Test]
    public async Task Closed_Order_Ignores_Late_Callback()
    {
        await using var factory = new WebAppFactory();
        var client = await LoginAsync(factory);
        var created = await Read(await AccountApi.Post(client, "/api/orders", new { productNo = "STD", orderType = 1, channel = "alipay" }));
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WebDb>();
            var order = await db.Orders.SingleAsync();
            order.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }
        var late = await Notify(client, created.OrderNo!, "LATE", "alipay", 12m);
        Assert.That(late.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        await using var check = factory.Services.CreateAsyncScope();
        var store = check.ServiceProvider.GetRequiredService<WebDb>();
        Assert.That(await store.SubscriptionAuths.CountAsync(), Is.EqualTo(0));
        Assert.That((await store.Orders.SingleAsync()).PayStatus, Is.EqualTo(3));
    }

    [Test]
    public async Task Price_Change_Does_Not_Change_Existing_Order()
    {
        await using var factory = new WebAppFactory();
        var client = await LoginAsync(factory);
        var created = await Read(await AccountApi.Post(client, "/api/orders", new { productNo = "STD", orderType = 1, channel = "wechat" }));
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WebDb>();
            var product = await db.Products.SingleAsync();
            product.Price = 120m;
            await db.SaveChangesAsync();
        }
        var again = await Read(await AccountApi.Post(client, "/api/orders", new { productNo = "STD", orderType = 1, channel = "wechat" }));
        Assert.That(created.Amount, Is.EqualTo(12m));
        Assert.That(again.Amount, Is.EqualTo(120m));
    }

    [Test]
    public async Task Renew_Extends_Original_End_After_Grace()
    {
        await using var factory = new WebAppFactory();
        var client = await LoginAsync(factory);
        var first = await Read(await AccountApi.Post(client, "/api/orders", new { productNo = "STD", orderType = 1, channel = "wechat" }));
        await Notify(client, first.OrderNo!, "R1", "wechat", 12m);
        DateTime oldEnd;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WebDb>();
            var auth = await db.SubscriptionAuths.SingleAsync();
            oldEnd = DateTime.UtcNow.AddDays(-10);
            auth.AuthEndTime = oldEnd;
            await db.SaveChangesAsync();
        }
        var second = await Read(await AccountApi.Post(client, "/api/orders", new { productNo = "STD", orderType = 1, channel = "wechat" }));
        var paid = await Read(await Notify(client, second.OrderNo!, "R2", "wechat", 12m));
        Assert.That(paid.AuthEndTime, Is.EqualTo(oldEnd.AddDays(365)).Within(TimeSpan.FromSeconds(1)));
    }

    [Test]
    public async Task Trial_Is_Limited_To_Two_Paid_Orders()
    {
        await using var factory = new WebAppFactory();
        var client = await LoginAsync(factory);
        var first = await Read(await AccountApi.Post(client, "/api/orders", new { productNo = "STD", orderType = 2, channel = "wechat" }));
        Assert.That(first.Amount, Is.EqualTo(0.10m));
        await Notify(client, first.OrderNo!, "TR1", "wechat", 0.10m);
        var second = await Read(await AccountApi.Post(client, "/api/orders", new { productNo = "STD", orderType = 2, channel = "alipay" }));
        Assert.That(second.Amount, Is.EqualTo(0.50m));
        await Notify(client, second.OrderNo!, "TR2", "alipay", 0.50m);
        var third = await AccountApi.Post(client, "/api/orders", new { productNo = "STD", orderType = 2, channel = "wechat" });
        Assert.That(await AccountApi.Body(third), Does.Contain("试用次数已用完"));
    }

    static async Task<HttpClient> LoginAsync(WebAppFactory factory)
    {
        var client = factory.CreateClient();
        var email = "buyer" + Guid.NewGuid().ToString("N")[..8] + "@example.com";
        await AccountApi.Register(factory, client, email, "abc12345");
        var login = await AccountApi.Post(client, "/api/account/login", new { email, password = "abc12345" });
        Assert.That(login.StatusCode, Is.EqualTo(HttpStatusCode.OK));
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
