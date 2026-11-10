using Microsoft.EntityFrameworkCore;
using YxyXb.Api.Data;

namespace YxyXb.Api.Services;

public sealed record VendorSettlementRow(
    long Id,
    string SettlementNo,
    decimal Amount,
    int Status,
    string? InvoiceFile);

public sealed record VendorInvoiceRow(
    long Id,
    string ApplyNo,
    string? TitleName,
    decimal Amount,
    string? Email);

public sealed class VendorSettlementService
{
    private readonly WebDb _db;
    private readonly TimeProvider _time;

    public VendorSettlementService(WebDb db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    public async Task<IReadOnlyList<VendorSettlementRow>> ListSettlementsAsync()
    {
        return await _db.Settlements.OrderByDescending(x => x.Id)
            .Select(x => new VendorSettlementRow(x.Id, x.SettlementNo, x.Amount, x.Status, x.InvoiceFile))
            .ToListAsync();
    }

    public async Task<IReadOnlyList<VendorInvoiceRow>> ListPendingInvoicesAsync()
    {
        return await _db.InvoiceApplies.Where(x => x.Status == 1).OrderBy(x => x.Id)
            .Select(x => new VendorInvoiceRow(x.Id, x.ApplyNo, x.TitleName, x.Amount, x.Email))
            .ToListAsync();
    }

    public async Task<OperationResult> ApproveSettlementAsync(long settlementId)
    {
        var row = await _db.Settlements.SingleOrDefaultAsync(x => x.Id == settlementId && x.Status == 1);
        if (row == null || string.IsNullOrWhiteSpace(row.InvoiceFile))
            return OperationResult.Fail("结算单不能审批");
        row.Status = 2;
        row.UpdatedAt = _time.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync();
        return OperationResult.Success();
    }

    public async Task<OperationResult> PaySettlementAsync(long settlementId, string? voucherNo)
    {
        if (string.IsNullOrWhiteSpace(voucherNo))
            return OperationResult.Fail("请填写付款流水号");
        var row = await _db.Settlements.SingleOrDefaultAsync(x => x.Id == settlementId && x.Status == 2);
        if (row == null) return OperationResult.Fail("结算单不能付款");
        var now = _time.GetUtcNow().UtcDateTime;
        row.Status = 3;
        row.PayVoucherNo = voucherNo.Trim();
        row.PaidAt = now;
        row.UpdatedAt = now;
        await _db.SaveChangesAsync();
        return OperationResult.Success();
    }

    public async Task<OperationResult> IssueInvoiceAsync(long invoiceId)
    {
        var row = await _db.InvoiceApplies.SingleOrDefaultAsync(x => x.Id == invoiceId && x.Status == 1);
        if (row == null) return OperationResult.Fail("开票申请不存在");
        row.Status = 3;
        row.UpdatedAt = _time.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync();
        return OperationResult.Success();
    }
}
