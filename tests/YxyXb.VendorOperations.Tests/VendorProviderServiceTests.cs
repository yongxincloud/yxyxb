using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using YxyXb.Api.Data;
using YxyXb.Api.Services;

namespace YxyXb.VendorOperations.Tests;

public class VendorProviderServiceTests
{
    private string _path = "";
    private WebDb _db = null!;
    private VendorProviderService _service = null!;
    private readonly DateTimeOffset _now = new(2030, 01, 02, 03, 04, 05, TimeSpan.Zero);

    [SetUp]
    public void SetUp()
    {
        _path = Path.Combine(Path.GetTempPath(), "yxy-provider-" + Guid.NewGuid().ToString("N") + ".db");
        var options = new DbContextOptionsBuilder<WebDb>().UseSqlite("Data Source=" + _path + ";Pooling=False").Options;
        _db = new WebDb(options);
        _db.Database.EnsureCreated();
        _service = new VendorProviderService(_db, new FixedTimeProvider(_now));
    }

    [TearDown]
    public void TearDown()
    {
        _db.Dispose();
        if (File.Exists(_path)) File.Delete(_path);
    }

    [Test]
    public async Task CreateAndUpdateLevel_EnforcesRateAndWritesAudit()
    {
        var created = await _service.CreateLevelAsync("区域代理", 0.2m);
        Assert.That(created.Ok, Is.True);

        var updated = await _service.UpdateLevelAsync(created.Value!.Id, 0.25m);

        Assert.That(updated.Ok, Is.True);
        Assert.That((await _db.ProviderLevels.SingleAsync()).CommissionRate, Is.EqualTo(0.25m));
        Assert.That(await _db.OperationLogs.CountAsync(x => x.Action == "rate"), Is.EqualTo(1));
        Assert.That((await _service.UpdateLevelAsync(created.Value.Id, 1m)).Ok, Is.False);
    }

    [Test]
    public async Task ApproveApplication_CreatesProviderAndUpdatesApplyAtomically()
    {
        var level = await AddLevel();
        var account = await AddAccount();
        var apply = new ProviderApply
        {
            AccountId = account.Id,
            CompanyName = "服务商甲",
            TaxNo = "T1",
            LicenseFile = "license.png",
            ContactName = "张三",
            ContactEmail = "provider@example.invalid",
            ContactPhone = "13900000000",
            Status = 0,
            CreatedAt = _now.UtcDateTime
        };
        _db.ProviderApplies.Add(apply);
        await _db.SaveChangesAsync();

        var result = await _service.ApproveAsync(apply.Id, level.Id);

        Assert.That(result.Ok, Is.True);
        Assert.That(await _db.Providers.CountAsync(), Is.EqualTo(1));
        Assert.That((await _db.ProviderApplies.SingleAsync()).Status, Is.EqualTo(1));
        Assert.That(result.Value!.ProviderNo, Does.StartWith("P20300102030405"));
    }

    [Test]
    public async Task Freeze_MovesCustomersAndPreventsFurtherProviderChanges()
    {
        var level = await AddLevel();
        var provider = new Provider
        {
            ProviderNo = "P1",
            AccountId = (await AddAccount()).Id,
            Name = "服务商",
            LevelId = level.Id,
            Status = 1,
            CreatedAt = _now.UtcDateTime,
            UpdatedAt = _now.UtcDateTime
        };
        _db.Providers.Add(provider);
        await _db.SaveChangesAsync();
        var customer = new Customer
        {
            CustomerNo = "C1",
            AccountId = (await AddAccount("customer@example.invalid")).Id,
            DirectProviderId = provider.Id,
            Status = 1,
            CreatedAt = _now.UtcDateTime,
            UpdatedAt = _now.UtcDateTime
        };
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        var frozen = await _service.FreezeAsync(provider.Id);

        Assert.That(frozen.Ok, Is.True);
        Assert.That(frozen.Value!.Moved, Is.EqualTo(1));
        Assert.That((await _db.Customers.SingleAsync()).DirectProviderId, Is.Null);
        Assert.That((await _service.SetStatusAsync(provider.Id, 1)).Ok, Is.False);
    }

    private async Task<ProviderLevel> AddLevel()
    {
        var level = new ProviderLevel
        {
            Name = "等级",
            CommissionRate = 0.2m,
            Status = 1,
            CreatedAt = _now.UtcDateTime,
            UpdatedAt = _now.UtcDateTime
        };
        _db.ProviderLevels.Add(level);
        await _db.SaveChangesAsync();
        return level;
    }

    private async Task<Account> AddAccount(string email = "provider@example.invalid")
    {
        var account = new Account
        {
            Email = email,
            PasswordHash = "hash",
            Status = 1,
            CreatedAt = _now.UtcDateTime,
            UpdatedAt = _now.UtcDateTime
        };
        _db.Accounts.Add(account);
        await _db.SaveChangesAsync();
        return account;
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _value;
        public FixedTimeProvider(DateTimeOffset value) => _value = value;
        public override DateTimeOffset GetUtcNow() => _value;
    }
}
