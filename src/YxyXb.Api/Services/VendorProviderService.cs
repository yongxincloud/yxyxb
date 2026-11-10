using Microsoft.EntityFrameworkCore;
using YxyXb.Api.Data;

namespace YxyXb.Api.Services;

public sealed record ProviderLevelResult(long Id);
public sealed record ProviderApprovalResult(string ProviderNo);
public sealed record ProviderFreezeResult(int Moved);
public sealed record ProviderApplyRow(long Id, string CompanyName, string TaxNo, string ContactName, string ContactEmail);

public sealed class VendorProviderService
{
    private readonly WebDb _db;
    private readonly TimeProvider _time;

    public VendorProviderService(WebDb db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    public async Task<OperationResult<ProviderLevelResult>> CreateLevelAsync(string? name, decimal rate)
    {
        if (string.IsNullOrWhiteSpace(name) || rate <= 0 || rate >= 1)
            return OperationResult<ProviderLevelResult>.Fail("等级名称必填，佣金比例要在 0 和 1 之间");
        var now = _time.GetUtcNow().UtcDateTime;
        var level = new ProviderLevel
        {
            Name = name.Trim(),
            CommissionRate = rate,
            Status = 1,
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.ProviderLevels.Add(level);
        await _db.SaveChangesAsync();
        return OperationResult<ProviderLevelResult>.Success(new ProviderLevelResult(level.Id));
    }

    public async Task<OperationResult> UpdateLevelAsync(long id, decimal rate)
    {
        if (rate <= 0 || rate >= 1)
            return OperationResult.Fail("佣金比例要在 0 和 1 之间");
        var level = await _db.ProviderLevels.SingleOrDefaultAsync(x => x.Id == id);
        if (level == null) return OperationResult.Fail("等级不存在");
        var before = level.CommissionRate;
        level.CommissionRate = rate;
        level.UpdatedAt = _time.GetUtcNow().UtcDateTime;
        _db.OperationLogs.Add(new OperationLog
        {
            Module = "level",
            Action = "rate",
            BeforeValue = before.ToString("0.00"),
            AfterValue = rate.ToString("0.00"),
            CreatedAt = level.UpdatedAt
        });
        await _db.SaveChangesAsync();
        return OperationResult.Success();
    }

    public async Task<OperationResult<ProviderApprovalResult>> ApproveAsync(long applicationId, long levelId)
    {
        var apply = await _db.ProviderApplies.SingleOrDefaultAsync(x => x.Id == applicationId && x.Status == 0);
        if (apply == null) return OperationResult<ProviderApprovalResult>.Fail("申请不存在或已审批");
        var level = await _db.ProviderLevels.SingleOrDefaultAsync(x => x.Id == levelId && x.Status == 1);
        if (level == null) return OperationResult<ProviderApprovalResult>.Fail("等级不存在");
        var now = _time.GetUtcNow().UtcDateTime;
        var provider = new Provider
        {
            ProviderNo = "P" + now.ToString("yyyyMMddHHmmssfff"),
            AccountId = apply.AccountId,
            Name = apply.CompanyName,
            LevelId = level.Id,
            Status = 1,
            EffectiveAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.Providers.Add(provider);
        apply.Status = 1;
        await _db.SaveChangesAsync();
        return OperationResult<ProviderApprovalResult>.Success(new ProviderApprovalResult(provider.ProviderNo));
    }

    public async Task<OperationResult> SetStatusAsync(long providerId, int status)
    {
        if (status is not (0 or 1)) return OperationResult.Fail("状态只能是启用或停用");
        var provider = await _db.Providers.SingleOrDefaultAsync(x => x.Id == providerId);
        if (provider == null) return OperationResult.Fail("服务商不存在");
        if (provider.Status == 2) return OperationResult.Fail("已解约的服务商不能改状态");
        if (provider.Status == status) return OperationResult.Success();
        var before = provider.Status;
        var now = _time.GetUtcNow().UtcDateTime;
        provider.Status = status;
        provider.UpdatedAt = now;
        _db.OperationLogs.Add(new OperationLog
        {
            Module = "provider",
            Action = status == 1 ? "enable" : "disable",
            BeforeValue = provider.ProviderNo + ":" + before,
            AfterValue = provider.ProviderNo + ":" + status,
            CreatedAt = now
        });
        await _db.SaveChangesAsync();
        return OperationResult.Success();
    }

    public async Task<OperationResult> SetLevelAsync(long providerId, long levelId)
    {
        var provider = await _db.Providers.SingleOrDefaultAsync(x => x.Id == providerId);
        if (provider == null) return OperationResult.Fail("服务商不存在");
        if (provider.Status == 2) return OperationResult.Fail("已解约的服务商不能改等级");
        var level = await _db.ProviderLevels.SingleOrDefaultAsync(x => x.Id == levelId && x.Status == 1);
        if (level == null) return OperationResult.Fail("等级不存在");
        if (provider.LevelId == level.Id) return OperationResult.Success();
        var before = provider.LevelId;
        var now = _time.GetUtcNow().UtcDateTime;
        provider.LevelId = level.Id;
        provider.UpdatedAt = now;
        _db.OperationLogs.Add(new OperationLog
        {
            Module = "provider",
            Action = "level",
            BeforeValue = provider.ProviderNo + ":" + before,
            AfterValue = provider.ProviderNo + ":" + level.Id,
            CreatedAt = now
        });
        await _db.SaveChangesAsync();
        return OperationResult.Success();
    }

    public async Task<OperationResult<ProviderFreezeResult>> FreezeAsync(long providerId)
    {
        var provider = await _db.Providers.SingleOrDefaultAsync(x => x.Id == providerId && x.Status != 2);
        if (provider == null) return OperationResult<ProviderFreezeResult>.Fail("服务商不存在或已解约");
        provider.Status = 2;
        provider.UpdatedAt = _time.GetUtcNow().UtcDateTime;
        var customers = await _db.Customers.Where(x => x.DirectProviderId == provider.Id).ToListAsync();
        foreach (var customer in customers) customer.DirectProviderId = null;
        _db.OperationLogs.Add(new OperationLog
        {
            Module = "provider",
            Action = "freeze",
            AfterValue = provider.ProviderNo,
            CreatedAt = provider.UpdatedAt
        });
        await _db.SaveChangesAsync();
        return OperationResult<ProviderFreezeResult>.Success(new ProviderFreezeResult(customers.Count));
    }

    public async Task<IReadOnlyList<ProviderApplyRow>> ListPendingApplicationsAsync()
    {
        return await _db.ProviderApplies
            .Where(x => x.Status == 0)
            .OrderBy(x => x.Id)
            .Select(x => new ProviderApplyRow(x.Id, x.CompanyName, x.TaxNo, x.ContactName, x.ContactEmail))
            .ToListAsync();
    }
}
