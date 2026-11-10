using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using YxyXb.Api.Data;
using YxyXb.Api.Services;

namespace YxyXb.VendorOperations.Tests;

public class VendorProductServiceTests
{
    private string _path = "";
    private WebDb _db = null!;
    private VendorProductService _service = null!;
    private readonly DateTimeOffset _now = new(2030, 01, 02, 03, 04, 05, TimeSpan.Zero);

    [SetUp]
    public void SetUp()
    {
        _path = Path.Combine(Path.GetTempPath(), "yxy-product-" + Guid.NewGuid().ToString("N") + ".db");
        var options = new DbContextOptionsBuilder<WebDb>().UseSqlite("Data Source=" + _path + ";Pooling=False").Options;
        _db = new WebDb(options);
        _db.Database.EnsureCreated();
        _service = new VendorProductService(_db, new FixedTimeProvider(_now));
        _db.Products.Add(new Product
        {
            ProductNo = "STANDARD",
            Name = "标准版",
            Price = 98,
            MaxDevices = 10,
            PeriodDays = 365,
            Status = 1,
            CreatedAt = _now.UtcDateTime,
            UpdatedAt = _now.UtcDateTime
        });
        _db.SaveChanges();
    }

    [TearDown]
    public void TearDown()
    {
        _db.Dispose();
        if (File.Exists(_path)) File.Delete(_path);
    }

    [Test]
    public async Task UpdateProduct_RecordsPricePromotionAndLimits()
    {
        var result = await _service.UpdateAsync("STANDARD", new ProductUpdateCommand(
            128,
            88,
            _now.UtcDateTime,
            _now.AddDays(7).UtcDateTime,
            20,
            3,
            0));

        Assert.That(result.Ok, Is.True);
        var product = await _db.Products.SingleAsync();
        Assert.That(product.Price, Is.EqualTo(128));
        Assert.That(product.PromoPrice, Is.EqualTo(88));
        Assert.That(product.MaxDevices, Is.EqualTo(20));
        Assert.That(product.SortOrder, Is.EqualTo(3));
        Assert.That(product.Status, Is.Zero);
        Assert.That(await _db.OperationLogs.CountAsync(x => x.Action == "update"), Is.EqualTo(1));
    }

    [Test]
    public async Task InvalidPromotion_IsRejected()
    {
        var result = await _service.UpdateAsync("STANDARD", new ProductUpdateCommand(
            null,
            88,
            _now.AddDays(7).UtcDateTime,
            _now.UtcDateTime,
            null,
            null,
            null));

        Assert.That(result.Ok, Is.False);
        Assert.That(result.Error, Does.Contain("促销"));
        Assert.That((await _db.Products.SingleAsync()).PromoPrice, Is.Null);
    }

    [Test]
    public async Task ZeroPromotion_ClearsPromotionWindow()
    {
        var product = await _db.Products.SingleAsync();
        product.PromoPrice = 88;
        product.PromoStart = _now.UtcDateTime;
        product.PromoEnd = _now.AddDays(7).UtcDateTime;
        await _db.SaveChangesAsync();

        var result = await _service.UpdateAsync("STANDARD", new ProductUpdateCommand(
            null, 0, null, null, null, null, null));

        Assert.That(result.Ok, Is.True);
        product = await _db.Products.SingleAsync();
        Assert.That(product.PromoPrice, Is.Null);
        Assert.That(product.PromoStart, Is.Null);
        Assert.That(product.PromoEnd, Is.Null);
    }

    [Test]
    public async Task ChangePrice_KeepsHistoricalProductRecord()
    {
        var result = await _service.ChangePriceAsync("STANDARD", 108);

        Assert.That(result.Ok, Is.True);
        Assert.That(await _db.Products.CountAsync(), Is.EqualTo(1));
        Assert.That((await _db.Products.SingleAsync()).ProductNo, Is.EqualTo("STANDARD"));
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _value;
        public FixedTimeProvider(DateTimeOffset value) => _value = value;
        public override DateTimeOffset GetUtcNow() => _value;
    }
}
