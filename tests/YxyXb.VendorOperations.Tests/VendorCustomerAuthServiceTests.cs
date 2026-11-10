using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using YxyXb.Api.Data;
using YxyXb.Api.Services;

namespace YxyXb.VendorOperations.Tests;

public class VendorCustomerAuthServiceTests
{
    private string _path = "";
    private WebDb _db = null!;
    private VendorCustomerAuthService _service = null!;
    private readonly DateTimeOffset _now = new(2030, 01, 02, 03, 04, 05, TimeSpan.Zero);

    [SetUp]
    public void SetUp()
    {
        _path = Path.Combine(Path.GetTempPath(), "yxy-vendor-" + Guid.NewGuid().ToString("N") + ".db");
        var options = new DbContextOptionsBuilder<WebDb>().UseSqlite("Data Source=" + _path + ";Pooling=False").Options;
        _db = new WebDb(options);
        _db.Database.EnsureCreated();
        _service = new VendorCustomerAuthService(_db, new FixedTimeProvider(_now));
        SeedBase();
    }

    [TearDown]
    public void TearDown()
    {
        _db.Dispose();
        if (File.Exists(_path)) File.Delete(_path);
    }

    [Test]
    public async Task Grant_AddsAuthorizationMessageAndAudit()
    {
        var result = await _service.GrantAsync(new CustomerGrantCommand(
            "customer@example.invalid", 30, null, "test", "CUSTOM", 7));

        Assert.That(result.Ok, Is.True);
        Assert.That(result.Value!.EndTime, Is.EqualTo(_now.UtcDateTime.AddDays(130)));
        Assert.That(await _db.SubscriptionAuths.CountAsync(), Is.EqualTo(2));
        Assert.That(await _db.Messages.CountAsync(x => x.Title == "授权已发放"), Is.EqualTo(1));
        Assert.That(await _db.OperationLogs.CountAsync(x => x.Action == "grant"), Is.EqualTo(1));
    }

    [Test]
    public async Task Grant_CustomProductRequiresDeviceLimit()
    {
        var result = await _service.GrantAsync(new CustomerGrantCommand(
            "customer@example.invalid", 30, null, "test", "CUSTOM", null));

        Assert.That(result.Ok, Is.False);
        Assert.That(result.Error, Does.Contain("定制版"));
        Assert.That(await _db.SubscriptionAuths.CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task ChangeEdition_KeepsEndTimeAndUpdatesDeviceLimit()
    {
        var auth = await _db.SubscriptionAuths.SingleAsync();
        var end = auth.AuthEndTime;

        var result = await _service.ChangeEditionAsync(auth.CustomerId, new CustomerEditionCommand("CUSTOM", "upgrade", 7));

        Assert.That(result.Ok, Is.True);
        auth = await _db.SubscriptionAuths.SingleAsync();
        Assert.That(auth.AuthEndTime, Is.EqualTo(end));
        Assert.That(auth.DeviceLimit, Is.EqualTo(7));
        Assert.That(await _db.Messages.CountAsync(x => x.Title == "账号类型已变更"), Is.EqualTo(1));
        Assert.That(await _db.OperationLogs.CountAsync(x => x.Action == "edition"), Is.EqualTo(1));
    }

    [Test]
    public async Task Search_ReturnsCurrentProductAndDeviceCap()
    {
        var rows = await _service.SearchAsync("customer");

        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(rows[0].Email, Is.EqualTo("customer@example.invalid"));
        Assert.That(rows[0].ProductNo, Is.EqualTo("STANDARD"));
        Assert.That(rows[0].MaxDevices, Is.EqualTo(10));
    }

    [Test]
    public async Task DeleteCustomer_RemovesDependentRowsAndAccount()
    {
        var account = await _db.Accounts.SingleAsync(x => x.Email == "customer@example.invalid");
        var customer = await _db.Customers.SingleAsync(x => x.AccountId == account.Id);
        _db.Payments.Add(new PaymentRecord
        {
            OrderId = (await _db.Orders.SingleAsync()).Id,
            Channel = "alipay",
            TradeNo = "trade-1",
            Amount = 10,
            CreatedAt = _now.UtcDateTime
        });
        _db.Messages.Add(new SiteMessage { AccountId = account.Id, Title = "m", CreatedAt = _now.UtcDateTime });
        _db.AuthTokens.Add(new AuthToken { AccountId = account.Id, TokenHash = "hash", ExpiresAt = _now.AddDays(1).UtcDateTime, IssuedAt = _now.UtcDateTime });
        await _db.SaveChangesAsync();

        var result = await _service.DeleteCustomerAsync(customer.Id);

        Assert.That(result.Ok, Is.True);
        Assert.That(await _db.Customers.CountAsync(), Is.Zero);
        Assert.That(await _db.Accounts.CountAsync(x => x.Id == account.Id), Is.Zero);
        Assert.That(await _db.Orders.CountAsync(), Is.Zero);
        Assert.That(await _db.SubscriptionAuths.CountAsync(), Is.Zero);
        Assert.That(await _db.Messages.CountAsync(x => x.AccountId == account.Id), Is.Zero);
    }

    private void SeedBase()
    {
        var now = _now.UtcDateTime;
        _db.Products.AddRange(
            new Product
            {
                ProductNo = "STANDARD",
                Name = "标准版",
                Price = 98,
                MaxDevices = 10,
                PeriodDays = 365,
                Status = 1,
                CreatedAt = now,
                UpdatedAt = now
            },
            new Product
            {
                ProductNo = "CUSTOM",
                Name = "定制版",
                Price = 0,
                MaxDevices = 1,
                PeriodDays = 365,
                Status = 1,
                CreatedAt = now,
                UpdatedAt = now
            });
        var account = new Account
        {
            Email = "customer@example.invalid",
            PasswordHash = "hash",
            Status = 1,
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.Accounts.Add(account);
        _db.SaveChanges();
        var customer = new Customer
        {
            CustomerNo = "C001",
            AccountId = account.Id,
            Name = "customer",
            Status = 1,
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.Customers.Add(customer);
        _db.SaveChanges();
        _db.Orders.Add(new SubscriptionOrder
        {
            OrderNo = "O001",
            CustomerId = customer.Id,
            ProductId = 1,
            OrderType = 1,
            Amount = 10,
            PayStatus = 1,
            CreatedAt = now,
            UpdatedAt = now
        });
        _db.SaveChanges();
        _db.SubscriptionAuths.Add(new SubscriptionAuth
        {
            CustomerId = customer.Id,
            ProductId = 1,
            AuthStartTime = now,
            AuthEndTime = now.AddDays(100),
            Status = 1,
            CreatedAt = now,
            UpdatedAt = now
        });
        _db.SaveChanges();
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _value;
        public FixedTimeProvider(DateTimeOffset value) => _value = value;
        public override DateTimeOffset GetUtcNow() => _value;
    }
}
