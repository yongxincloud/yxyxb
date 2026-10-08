using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using YxyXb.Api.Data;
using YxyXb.Api.Services;

namespace YxyXb.Api.Tests;

public sealed class PublicPriceTests
{
    [Test]
    public async Task Public_Price_Returns_Three_Plans()
    {
        await using var factory = new WebAppFactory();
        var client = factory.CreateClient();
        var json = await client.GetFromJsonAsync<PriceDto>("/api/public/price");
        Assert.That(json!.Ok, Is.True);
        Assert.That(json.Plans.Length, Is.EqualTo(3));
        Assert.That(json.Plans.Select(p => p.ProductNo), Is.EquivalentTo(new[] { "PERSONAL", "STANDARD", "PRO" }));
        Assert.That(json.Plans.Single(p => p.ProductNo == "STANDARD").ListPrice, Is.EqualTo(98m));
        Assert.That(json.Plans.Single(p => p.ProductNo == "PERSONAL").MaxDevices, Is.EqualTo(3));
        Assert.That(json.Plans.Single(p => p.ProductNo == "PRO").MaxDevices, Is.EqualTo(50));
    }

    [Test]
    public async Task Formal_Order_Uses_Promo_Sale_Price()
    {
        await using var factory = new WebAppFactory();
        var now = DateTime.UtcNow;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WebDb>();
            await scope.ServiceProvider.GetRequiredService<OrderService>().EnsureCatalogAsync();
            var product = await db.Products.SingleAsync(x => x.ProductNo == OrderService.StandardProductNo);
            product.PromoPrice = 49m;
            product.PromoStart = now.AddDays(-1);
            product.PromoEnd = now.AddDays(1);
            await db.SaveChangesAsync();
        }

        var client = await LoginAsync(factory);
        var created = await Read(await AccountApi.Post(client, "/api/orders", new { productNo = "STANDARD", orderType = 1, channel = "alipay" }));
        Assert.That(created.Amount, Is.EqualTo(49m));
    }

    static async Task<HttpClient> LoginAsync(WebAppFactory factory)
    {
        var client = factory.CreateClient();
        await AccountApi.Register(factory, client, "buyer@example.com", "abc12345");
        await AccountApi.Post(client, "/api/account/login", new { email = "buyer@example.com", password = "abc12345" });
        return client;
    }

    static async Task<Created> Read(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Created>())!;
    }

    sealed record PriceDto(bool Ok, PlanRow[] Plans);
    sealed record PlanRow(string ProductNo, string Name, int MaxDevices, decimal ListPrice, decimal SalePrice, bool PromoActive, DateTime? PromoEnd, int SortOrder);
    sealed record Created(bool Ok, string? OrderNo, decimal? Amount);
}
