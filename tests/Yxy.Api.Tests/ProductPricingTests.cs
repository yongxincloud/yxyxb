using NUnit.Framework;
using Yxy.Api.Data;
using Yxy.Api.Services;

namespace Yxy.Api.Tests;

public sealed class ProductPricingTests
{
    [Test]
    public void SalePrice_Uses_Promo_Inside_Window()
    {
        var now = new DateTime(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);
        var p = new Product
        {
            Price = 98m,
            PromoPrice = 49m,
            PromoStart = now.AddDays(-1),
            PromoEnd = now.AddDays(1)
        };
        Assert.That(ProductPricing.SalePrice(p, now), Is.EqualTo(49m));
        Assert.That(ProductPricing.IsPromoActive(p, now), Is.True);
    }

    [Test]
    public void SalePrice_Falls_Back_Outside_Window()
    {
        var now = new DateTime(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);
        var p = new Product
        {
            Price = 98m,
            PromoPrice = 49m,
            PromoStart = now.AddDays(-10),
            PromoEnd = now.AddDays(-1)
        };
        Assert.That(ProductPricing.SalePrice(p, now), Is.EqualTo(98m));
    }
}
