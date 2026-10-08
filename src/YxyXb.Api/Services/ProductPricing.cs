using YxyXb.Api.Data;

namespace YxyXb.Api.Services;

public static class ProductPricing
{
    public static bool IsPromoActive(Product p, DateTime utcNow) =>
        p.PromoPrice is > 0
        && p.PromoStart is DateTime s
        && p.PromoEnd is DateTime e
        && utcNow >= s && utcNow <= e;

    public static decimal SalePrice(Product p, DateTime utcNow) =>
        IsPromoActive(p, utcNow) ? p.PromoPrice!.Value : p.Price;
}
