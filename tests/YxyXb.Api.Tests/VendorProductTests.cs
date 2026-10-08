using System.Net;
using System.Net.Http.Json;
using NUnit.Framework;
using YxyXb.Api.Controllers;

namespace YxyXb.Api.Tests;

public sealed class VendorProductTests
{
    [Test]
    public async Task Vendor_Can_Set_Promo_And_MaxDevices()
    {
        await using var factory = new WebAppFactory();
        var vendor = factory.CreateClient();
        vendor.DefaultRequestHeaders.Add("X-Vendor-Key", VendorController.VendorKey);
        var now = DateTime.UtcNow;
        var updated = await AccountApi.Post(vendor, "/api/vendor/products/STANDARD", new
        {
            price = 98m,
            promoPrice = 50m,
            promoStart = now.AddDays(-1),
            promoEnd = now.AddDays(2),
            maxDevices = 12
        });
        Assert.That(updated.StatusCode, Is.EqualTo(HttpStatusCode.OK), await updated.Content.ReadAsStringAsync());

        var price = await factory.CreateClient().GetFromJsonAsync<PriceDto>("/api/public/price");
        var standard = price!.Plans.Single(p => p.ProductNo == "STANDARD");
        Assert.That(standard.SalePrice, Is.EqualTo(50m));
        Assert.That(standard.MaxDevices, Is.EqualTo(12));
        Assert.That(standard.PromoActive, Is.True);
    }

    sealed record PriceDto(bool Ok, PlanRow[] Plans);
    sealed record PlanRow(string ProductNo, decimal SalePrice, int MaxDevices, bool PromoActive);
}
