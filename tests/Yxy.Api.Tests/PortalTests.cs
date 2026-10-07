using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Yxy.Api.Controllers;
using Yxy.Api.Data;
using Yxy.Api.Services;

namespace Yxy.Api.Tests;

public sealed class PortalTests
{
    [Test]
    public async Task Home_Price_Comes_From_Database()
    {
        await using var factory = new WebAppFactory();
        var body = await factory.CreateClient().GetFromJsonAsync<PriceDto>("/api/public/price");
        Assert.That(body!.Plans, Is.Not.Null.And.Not.Empty);
        Assert.That(body.Plans.Single(p => p.ProductNo == "STANDARD").ListPrice, Is.EqualTo(98m));
    }

    [Test]
    public async Task Portal_Shows_No_Auth_Before_Payment()
    {
        await using var factory = new WebAppFactory();
        var client = factory.CreateClient();
        var email = "portal@example.com";
        await AccountApi.Register(factory, client, email, "abc12345");
        var login = await AccountApi.Post(client, "/api/account/login", new { email, password = "abc12345" });
        Assert.That(login.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var sub = await client.GetFromJsonAsync<SubDto>("/api/portal/subscription");
        Assert.That(sub!.AuthStatus, Is.EqualTo("none"));
        Assert.That(sub.Usable, Is.False);
    }

    [Test]
    public async Task Provider_Apply_Requires_Every_Field()
    {
        await using var factory = new WebAppFactory();
        var client = factory.CreateClient();
        await AccountApi.Register(factory, client, "apply@example.com", "abc12345");
        await AccountApi.Post(client, "/api/account/login", new { email = "apply@example.com", password = "abc12345" });
        var missing = await AccountApi.Post(client, "/api/portal/provider-apply", new { companyName = "甲公司" });
        Assert.That(missing.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var ok = await AccountApi.Post(client, "/api/portal/provider-apply", new
        {
            companyName = "甲公司",
            taxNo = "91310000",
            licenseFile = "license.png",
            contactName = "张三",
            contactEmail = "a@b.com",
            contactPhone = "13800000000"
        });
        Assert.That(ok.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WebDb>();
        Assert.That(await db.ProviderApplies.CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task Invoice_Uses_Paid_Orders_That_Are_Not_Yet_Invoiced()
    {
        await using var factory = new WebAppFactory();
        var client = factory.CreateClient();
        await AccountApi.Register(factory, client, "invoice@example.com", "abc12345");
        await AccountApi.Post(client, "/api/account/login", new { email = "invoice@example.com", password = "abc12345" });
        var created = await client.PostAsJsonAsync("/api/orders", new { productNo = "STANDARD", orderType = 1, channel = "alipay" });
        var order = await created.Content.ReadFromJsonAsync<OrderResult>();
        var unpaid = await client.PostAsJsonAsync("/api/orders", new { productNo = "STANDARD", orderType = 1, channel = "alipay" });
        var unpaidOrder = await unpaid.Content.ReadFromJsonAsync<OrderResult>();
        await Notify(client, order!.OrderNo!, "INV1", 98m);
        await Notify(client, unpaidOrder!.OrderNo!, "INV0", 0m);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WebDb>();
            var zero = await db.Orders.SingleAsync(x => x.OrderNo == unpaidOrder.OrderNo);
            zero.Amount = 0m;
            zero.PayStatus = 1;
            await db.SaveChangesAsync();
        }

        var missing = await AccountApi.Post(client, "/api/portal/invoice-apply", new { orderNos = new[] { order.OrderNo } });
        Assert.That(await AccountApi.Body(missing), Does.Contain("请先在账号信息里填写开票信息"));
        var saved = await AccountApi.Post(client, "/api/account/profile", new
        {
            name = "张三",
            nickname = "小张",
            companyName = "甲公司",
            invoiceTitle = "甲公司",
            invoiceTaxNo = "91310000",
            invoiceEmail = "a@b.com",
            invoiceLicense = "license.png"
        });
        Assert.That(saved.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var listed = await client.GetFromJsonAsync<InvoiceOrdersDto>("/api/portal/invoice-orders");
        Assert.That(listed!.Orders.Select(x => x.OrderNo), Is.EqualTo(new[] { order.OrderNo }));

        var ok = await AccountApi.Post(client, "/api/portal/invoice-apply", new { orderNos = new[] { order.OrderNo } });
        Assert.That(ok.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var again = await client.GetFromJsonAsync<InvoiceOrdersDto>("/api/portal/invoice-orders");
        Assert.That(again!.Orders, Is.Empty);
        var me = await client.GetAsync("/api/account/me");
        Assert.That(me.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var bye = await AccountApi.Post(client, "/api/account/logout", new { });
        Assert.That(bye.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var after = await client.GetAsync("/api/account/me");
        Assert.That(after.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    static Task<HttpResponseMessage> Notify(HttpClient client, string orderNo, string tradeNo, decimal amount)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/payments/notify")
        {
            Content = JsonContent.Create(new { orderNo, tradeNo, channel = "alipay", amount })
        };
        req.Headers.Add("X-Pay-Sign", OrderService.Sign(OrderController.DevPaySecret, orderNo, tradeNo, "alipay", amount));
        return client.SendAsync(req);
    }

    sealed record PriceDto(bool Ok, PlanRow[] Plans);
    sealed record PlanRow(string ProductNo, decimal ListPrice);
    sealed record InvoiceOrdersDto(bool Ok, InvoiceOrderDto[] Orders);
    sealed record InvoiceOrderDto(string OrderNo, decimal Amount);
    sealed record SubDto(bool Ok, string AuthStatus, bool Usable);
}
