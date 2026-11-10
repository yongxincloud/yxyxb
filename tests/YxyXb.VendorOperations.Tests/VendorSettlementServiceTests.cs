using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using YxyXb.Api.Data;
using YxyXb.Api.Services;

namespace YxyXb.VendorOperations.Tests;

public class VendorSettlementServiceTests
{
    private string _path = "";
    private WebDb _db = null!;
    private VendorSettlementService _service = null!;
    private readonly DateTimeOffset _now = new(2030, 01, 02, 03, 04, 05, TimeSpan.Zero);

    [SetUp]
    public void SetUp()
    {
        _path = Path.Combine(Path.GetTempPath(), "yxy-settlement-" + Guid.NewGuid().ToString("N") + ".db");
        var options = new DbContextOptionsBuilder<WebDb>().UseSqlite("Data Source=" + _path + ";Pooling=False").Options;
        _db = new WebDb(options);
        _db.Database.EnsureCreated();
        _service = new VendorSettlementService(_db, new FixedTimeProvider(_now));
    }

    [TearDown]
    public void TearDown()
    {
        _db.Dispose();
        if (File.Exists(_path)) File.Delete(_path);
    }

    [Test]
    public async Task ApproveSettlement_RequiresInvoiceAndMovesToApproved()
    {
        var settlement = await AddSettlement(1, null);
        Assert.That((await _service.ApproveSettlementAsync(settlement.Id)).Ok, Is.False);

        settlement.InvoiceFile = "invoice.pdf";
        await _db.SaveChangesAsync();
        var result = await _service.ApproveSettlementAsync(settlement.Id);

        Assert.That(result.Ok, Is.True);
        Assert.That((await _db.Settlements.SingleAsync()).Status, Is.EqualTo(2));
    }

    [Test]
    public async Task PaySettlement_RequiresVoucherAndRecordsPayment()
    {
        var settlement = await AddSettlement(2, "invoice.pdf");

        Assert.That((await _service.PaySettlementAsync(settlement.Id, "")).Ok, Is.False);
        var result = await _service.PaySettlementAsync(settlement.Id, "PAY-001");

        Assert.That(result.Ok, Is.True);
        var row = await _db.Settlements.SingleAsync();
        Assert.That(row.Status, Is.EqualTo(3));
        Assert.That(row.PayVoucherNo, Is.EqualTo("PAY-001"));
        Assert.That(row.PaidAt, Is.EqualTo(_now.UtcDateTime));
    }

    [Test]
    public async Task IssueInvoice_MovesPendingApplicationToIssued()
    {
        var invoice = new InvoiceApply
        {
            ApplyNo = "I1",
            CustomerId = 1,
            Amount = 10,
            Status = 1,
            CreatedAt = _now.UtcDateTime,
            UpdatedAt = _now.UtcDateTime
        };
        _db.InvoiceApplies.Add(invoice);
        await _db.SaveChangesAsync();

        var result = await _service.IssueInvoiceAsync(invoice.Id);

        Assert.That(result.Ok, Is.True);
        Assert.That((await _db.InvoiceApplies.SingleAsync()).Status, Is.EqualTo(3));
        Assert.That((await _service.IssueInvoiceAsync(invoice.Id)).Ok, Is.False);
    }

    [Test]
    public async Task Lists_ReturnOnlyExistingVisibleRows()
    {
        await AddSettlement(1, "invoice.pdf");
        _db.InvoiceApplies.AddRange(
            new InvoiceApply { ApplyNo = "I1", CustomerId = 1, Amount = 10, Status = 1, CreatedAt = _now.UtcDateTime, UpdatedAt = _now.UtcDateTime },
            new InvoiceApply { ApplyNo = "I2", CustomerId = 1, Amount = 20, Status = 3, CreatedAt = _now.UtcDateTime, UpdatedAt = _now.UtcDateTime });
        await _db.SaveChangesAsync();

        Assert.That(await _service.ListSettlementsAsync(), Has.Count.EqualTo(1));
        var invoices = await _service.ListPendingInvoicesAsync();
        Assert.That(invoices, Has.Count.EqualTo(1));
        Assert.That(invoices[0].ApplyNo, Is.EqualTo("I1"));
    }

    private async Task<Settlement> AddSettlement(int status, string? invoiceFile)
    {
        var row = new Settlement
        {
            SettlementNo = "S" + status,
            ProviderId = 1,
            PeriodStart = _now.UtcDateTime.AddDays(-30),
            PeriodEnd = _now.UtcDateTime,
            Amount = 10,
            Status = status,
            InvoiceFile = invoiceFile,
            CreatedAt = _now.UtcDateTime,
            UpdatedAt = _now.UtcDateTime
        };
        _db.Settlements.Add(row);
        await _db.SaveChangesAsync();
        return row;
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _value;
        public FixedTimeProvider(DateTimeOffset value) => _value = value;
        public override DateTimeOffset GetUtcNow() => _value;
    }
}
