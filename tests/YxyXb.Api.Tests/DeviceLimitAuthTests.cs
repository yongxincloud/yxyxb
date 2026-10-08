using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using YxyXb.Api.Data;
using YxyXb.Api.Services;

namespace YxyXb.Api.Tests;

public sealed class DeviceLimitAuthTests
{
    [Test]
    public async Task Personal_Fourth_Device_Is_Conflict()
    {
        await using var factory = new WebAppFactory();
        var client = factory.CreateClient();
        await AccountApi.Register(factory, client, "limit@example.com", "abc12345");
        await GrantProductAsync(factory, "limit@example.com", OrderService.PersonalProductNo, DateTime.UtcNow.AddDays(40));

        for (var i = 1; i <= 3; i++)
        {
            var ok = await AccountApi.Post(client, "/api/client/login", new
            {
                email = "limit@example.com",
                password = "abc12345",
                deviceId = "pc-" + i
            });
            Assert.That(ok.StatusCode, Is.EqualTo(HttpStatusCode.OK), await ok.Content.ReadAsStringAsync());
        }

        var blocked = await AccountApi.Post(client, "/api/client/login", new
        {
            email = "limit@example.com",
            password = "abc12345",
            deviceId = "pc-4"
        });
        Assert.That(blocked.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        var body = await blocked.Content.ReadFromJsonAsync<LimitDto>();
        Assert.That(body!.ErrorCode, Is.EqualTo("DEVICE_LIMIT"));
        Assert.That(body.MaxDevices, Is.EqualTo(3));
    }

    [Test]
    public async Task Trial_Allows_More_Than_Three_Devices()
    {
        await using var factory = new WebAppFactory();
        var client = factory.CreateClient();
        await AccountApi.Register(factory, client, "trial@example.com", "abc12345");
        await AccountApi.Post(client, "/api/account/login", new { email = "trial@example.com", password = "abc12345" });
        var created = await AccountApi.Post(client, "/api/orders", new { productNo = "STANDARD", orderType = 2, channel = "alipay" });
        var order = await created.Content.ReadFromJsonAsync<OrderDto>();
        await Notify(client, order!.OrderNo!, "TRIAL1", "wechat", 0.10m);

        for (var i = 1; i <= 4; i++)
        {
            var ok = await AccountApi.Post(client, "/api/client/login", new
            {
                email = "trial@example.com",
                password = "abc12345",
                deviceId = "trial-pc-" + i
            });
            Assert.That(ok.StatusCode, Is.EqualTo(HttpStatusCode.OK), await ok.Content.ReadAsStringAsync());
        }
    }

    static async Task GrantProductAsync(WebAppFactory factory, string email, string productNo, DateTime end)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WebDb>();
        await scope.ServiceProvider.GetRequiredService<OrderService>().EnsureCatalogAsync();
        var product = await db.Products.SingleAsync(x => x.ProductNo == productNo);
        var account = await db.Accounts.SingleAsync(x => x.Email == email);
        var customer = await db.Customers.SingleAsync(x => x.AccountId == account.Id);
        var now = DateTime.UtcNow;
        db.SubscriptionAuths.Add(new SubscriptionAuth
        {
            CustomerId = customer.Id,
            ProductId = product.Id,
            AuthStartTime = now,
            AuthEndTime = end,
            Status = 1,
            GrantType = 2,
            CreatedAt = now,
            UpdatedAt = now
        });
        await db.SaveChangesAsync();
    }

    static Task<HttpResponseMessage> Notify(HttpClient client, string orderNo, string tradeNo, string channel, decimal amount)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/payments/notify")
        {
            Content = JsonContent.Create(new { orderNo, tradeNo, channel, amount })
        };
        req.Headers.Add("X-Pay-Sign", OrderService.Sign(YxyXb.Api.Controllers.OrderController.DevPaySecret, orderNo, tradeNo, channel, amount));
        return client.SendAsync(req);
    }

    sealed record LimitDto(bool Ok, string? ErrorCode, int? MaxDevices);
    sealed record OrderDto(bool Ok, string? OrderNo);
}
