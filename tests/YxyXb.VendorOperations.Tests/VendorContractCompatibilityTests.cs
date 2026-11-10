using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using YxyXb.Api.Data;
using YxyXb.Api.Services;

namespace YxyXb.VendorOperations.Tests;

public class VendorContractCompatibilityTests
{
    private string _path = "";
    private WebDb _db = null!;
    private readonly DateTimeOffset _now = new(2030, 01, 02, 03, 04, 05, TimeSpan.Zero);

    [SetUp]
    public void SetUp()
    {
        _path = Path.Combine(Path.GetTempPath(), "yxy-vendor-contract-" + Guid.NewGuid().ToString("N") + ".db");
        var options = new DbContextOptionsBuilder<WebDb>().UseSqlite("Data Source=" + _path + ";Pooling=False").Options;
        _db = new WebDb(options);
        _db.Database.EnsureCreated();
    }

    [TearDown]
    public void TearDown()
    {
        _db.Dispose();
        if (File.Exists(_path)) File.Delete(_path);
    }

    [Test]
    public async Task ProductionShape_RemainsReadableAndDoesNotRewriteHistory()
    {
        SeedProductionShape();
        var originalOrderAmount = await _db.Orders.OrderBy(x => x.Id).Select(x => x.Amount).FirstAsync();
        var originalAuthEnd = await _db.SubscriptionAuths.OrderBy(x => x.Id).Select(x => x.AuthEndTime).FirstAsync();

        var customers = await new VendorCustomerAuthService(_db, new FixedTimeProvider(_now)).SearchAsync(null);
        var applies = await new VendorProviderService(_db, new FixedTimeProvider(_now)).ListPendingApplicationsAsync();
        var invoices = await new VendorSettlementService(_db, new FixedTimeProvider(_now)).ListPendingInvoicesAsync();

        Assert.That(customers, Has.Count.EqualTo(16));
        Assert.That(applies, Has.Count.EqualTo(1));
        Assert.That(invoices, Has.Count.EqualTo(1));
        Assert.That(await _db.Orders.CountAsync(), Is.EqualTo(62));
        Assert.That(await _db.Payments.CountAsync(), Is.EqualTo(15));
        Assert.That(await _db.SubscriptionAuths.CountAsync(), Is.EqualTo(37));
        Assert.That(await _db.Providers.CountAsync(), Is.EqualTo(1));
        Assert.That(await _db.Products.CountAsync(), Is.EqualTo(5));
        Assert.That(await _db.Products.AnyAsync(x => x.ProductNo == "STD" && x.Status == 0), Is.True);
        Assert.That(await _db.Orders.OrderBy(x => x.Id).Select(x => x.Amount).FirstAsync(), Is.EqualTo(originalOrderAmount));
        Assert.That(await _db.SubscriptionAuths.OrderBy(x => x.Id).Select(x => x.AuthEndTime).FirstAsync(), Is.EqualTo(originalAuthEnd));
    }

    private void SeedProductionShape()
    {
        var now = _now.UtcDateTime;
        var products = new[]
        {
            Product("PERSONAL", "个人版", 3, 36, 1),
            Product("STANDARD", "标准版", 10, 98, 2),
            Product("PRO", "专业版", 50, 198, 3),
            Product("CUSTOM", "定制版", 1, 0, 4),
            Product("STD", "旧标准版", 10, 98, 5)
        };
        products[4].Status = 0;
        _db.Products.AddRange(products);
        _db.ProviderLevels.Add(new ProviderLevel
        {
            Name = "默认等级",
            CommissionRate = 0.2m,
            Status = 1,
            CreatedAt = now,
            UpdatedAt = now
        });
        _db.SaveChanges();

        var accounts = new List<Account>();
        var customers = new List<Customer>();
        for (var i = 0; i < 16; i++)
        {
            var account = new Account
            {
                Email = "customer" + i + "@example.invalid",
                PasswordHash = "hash",
                Status = 1,
                CreatedAt = now.AddDays(-i),
                UpdatedAt = now.AddDays(-i)
            };
            accounts.Add(account);
            customers.Add(new Customer
            {
                CustomerNo = "C" + (i + 1).ToString("000"),
                AccountId = 0,
                Name = "客户" + (i + 1),
                CompanyName = i % 2 == 0 ? "公司" + (i + 1) : null,
                Status = 1,
                CreatedAt = now.AddDays(-i),
                UpdatedAt = now.AddDays(-i)
            });
        }
        _db.Accounts.AddRange(accounts);
        _db.SaveChanges();
        for (var i = 0; i < customers.Count; i++) customers[i].AccountId = accounts[i].Id;
        _db.Customers.AddRange(customers);
        _db.SaveChanges();

        var provider = new Provider
        {
            ProviderNo = "P001",
            AccountId = accounts[0].Id,
            Name = "服务商",
            LevelId = _db.ProviderLevels.First().Id,
            Status = 1,
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.Providers.Add(provider);
        _db.ProviderApplies.Add(new ProviderApply
        {
            AccountId = accounts[1].Id,
            CompanyName = "申请公司",
            TaxNo = "T1",
            LicenseFile = "license.png",
            ContactName = "联系人",
            ContactEmail = "contact@example.invalid",
            ContactPhone = "13900000000",
            Status = 0,
            CreatedAt = now
        });
        _db.InvoiceApplies.Add(new InvoiceApply
        {
            ApplyNo = "I001",
            CustomerId = customers[2].Id,
            Amount = 10,
            Status = 1,
            CreatedAt = now,
            UpdatedAt = now
        });
        for (var i = 0; i < 37; i++)
        {
            _db.SubscriptionAuths.Add(new SubscriptionAuth
            {
                CustomerId = customers[i % 16].Id,
                ProductId = products[i % 4].Id,
                AuthStartTime = now.AddDays(-100),
                AuthEndTime = now.AddDays(100 + i),
                Status = 1,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
        for (var i = 0; i < 62; i++)
        {
            _db.Orders.Add(new SubscriptionOrder
            {
                OrderNo = "O" + (i + 1).ToString("000"),
                CustomerId = customers[i % 16].Id,
                ProductId = products[i % 4].Id,
                OrderType = 1,
                Amount = 98 + i,
                PayStatus = i < 15 ? 1 : 0,
                PayTime = i < 15 ? now.AddDays(-i) : null,
                CreatedAt = now.AddDays(-i),
                UpdatedAt = now.AddDays(-i)
            });
        }
        _db.SaveChanges();
        var paidOrders = _db.Orders.Where(x => x.PayStatus == 1).OrderBy(x => x.Id).Take(15).ToList();
        for (var i = 0; i < paidOrders.Count; i++)
        {
            _db.Payments.Add(new PaymentRecord
            {
                OrderId = paidOrders[i].Id,
                Channel = "alipay",
                TradeNo = "T" + (i + 1),
                Amount = paidOrders[i].Amount,
                CreatedAt = now
            });
        }
        _db.SaveChanges();
    }

    private Product Product(string no, string name, int devices, decimal price, int sort)
    {
        return new Product
        {
            ProductNo = no,
            Name = name,
            Price = price,
            MaxDevices = devices,
            SortOrder = sort,
            PeriodDays = 365,
            Status = 1,
            CreatedAt = _now.UtcDateTime,
            UpdatedAt = _now.UtcDateTime
        };
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _value;
        public FixedTimeProvider(DateTimeOffset value) => _value = value;
        public override DateTimeOffset GetUtcNow() => _value;
    }
}
