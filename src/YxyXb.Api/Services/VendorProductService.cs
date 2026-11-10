using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using YxyXb.Api.Data;

namespace YxyXb.Api.Services;

public sealed record ProductUpdateCommand(
    decimal? Price,
    decimal? PromoPrice,
    DateTime? PromoStart,
    DateTime? PromoEnd,
    int? MaxDevices,
    int? SortOrder,
    int? Status);

public sealed class VendorProductService
{
    private readonly WebDb _db;
    private readonly TimeProvider _time;

    public VendorProductService(WebDb db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    public async Task<OperationResult> ChangePriceAsync(string productNo, decimal price)
    {
        return await UpdateAsync(productNo, new ProductUpdateCommand(price, null, null, null, null, null, null));
    }

    public async Task<OperationResult> UpdateAsync(string productNo, ProductUpdateCommand command)
    {
        var product = await _db.Products.SingleOrDefaultAsync(x => x.ProductNo == productNo);
        if (product == null) return OperationResult.Fail("产品不存在");
        if (command.Price is <= 0) return OperationResult.Fail("价格必须大于 0");
        if (command.MaxDevices is < 1) return OperationResult.Fail("设备上限至少为 1");
        if (command.Status is not (null or 0 or 1)) return OperationResult.Fail("上下架状态不正确");

        var promoPrice = command.PromoPrice ?? product.PromoPrice;
        var promoStart = command.PromoStart ?? product.PromoStart;
        var promoEnd = command.PromoEnd ?? product.PromoEnd;
        if (command.PromoPrice == 0)
        {
            promoPrice = null;
            promoStart = null;
            promoEnd = null;
        }

        if (promoPrice is > 0 && (promoStart == null || promoEnd == null || promoStart >= promoEnd))
            return OperationResult.Fail("促销价需要有效的起止时间");

        var before = Snapshot(product);
        if (command.Price != null) product.Price = command.Price.Value;
        if (command.PromoPrice != null || command.PromoPrice == 0)
        {
            product.PromoPrice = promoPrice;
            product.PromoStart = promoStart;
            product.PromoEnd = promoEnd;
        }
        else
        {
            if (command.PromoStart != null) product.PromoStart = command.PromoStart;
            if (command.PromoEnd != null) product.PromoEnd = command.PromoEnd;
        }

        if (command.MaxDevices != null) product.MaxDevices = command.MaxDevices.Value;
        if (command.SortOrder != null) product.SortOrder = command.SortOrder.Value;
        if (command.Status != null) product.Status = command.Status.Value;
        product.UpdatedAt = _time.GetUtcNow().UtcDateTime;
        _db.OperationLogs.Add(new OperationLog
        {
            Module = "product",
            Action = "update",
            BeforeValue = before,
            AfterValue = Snapshot(product),
            CreatedAt = product.UpdatedAt
        });
        await _db.SaveChangesAsync();
        return OperationResult.Success();
    }

    private static string Snapshot(Product product)
    {
        return JsonSerializer.Serialize(new
        {
            product.Price,
            product.PromoPrice,
            product.PromoStart,
            product.PromoEnd,
            product.MaxDevices,
            product.SortOrder,
            product.Status
        });
    }
}
