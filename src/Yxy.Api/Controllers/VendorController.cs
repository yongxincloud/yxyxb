using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Yxy.Api.Data;
using Yxy.Api.Services;

namespace Yxy.Api.Controllers;

public sealed record LevelBody(string? Name, decimal Rate);
public sealed record ApproveBody(long LevelId);
public sealed record PayBody(string? VoucherNo);
public sealed record GrantBody(string? Email, int? Days, DateTime? EndTime, string? Reason);
public sealed record PriceBody(decimal Price);

[ApiController]
[Route("api/vendor")]
public sealed class VendorController : ControllerBase
{
    public const string VendorKey = "yxy-vendor";
    private readonly WebDb _db;
    public VendorController(WebDb db) => _db = db;

    [HttpPost("levels")]
    public async Task<IActionResult> CreateLevel([FromBody] LevelBody body)
    {
        if (!Vendor()) return NotFound();
        if (string.IsNullOrWhiteSpace(body.Name) || body.Rate <= 0 || body.Rate >= 1)
            return BadRequest(new { ok = false, error = "等级名称必填，佣金比例要在 0 和 1 之间" });
        var now = DateTime.UtcNow;
        var level = new ProviderLevel
        {
            Name = body.Name.Trim(),
            CommissionRate = body.Rate,
            Status = 1,
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.ProviderLevels.Add(level);
        await _db.SaveChangesAsync();
        return Ok(new { ok = true, id = level.Id });
    }

    [HttpPost("levels/{id:long}")]
    public async Task<IActionResult> UpdateLevel(long id, [FromBody] LevelBody body)
    {
        if (!Vendor()) return NotFound();
        if (body.Rate <= 0 || body.Rate >= 1)
            return BadRequest(new { ok = false, error = "佣金比例要在 0 和 1 之间" });
        var level = await _db.ProviderLevels.SingleOrDefaultAsync(x => x.Id == id);
        if (level == null) return BadRequest(new { ok = false, error = "等级不存在" });
        level.CommissionRate = body.Rate;
        level.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }

    [HttpPost("provider-applies/{id:long}/approve")]
    public async Task<IActionResult> Approve(long id, [FromBody] ApproveBody body)
    {
        if (!Vendor()) return NotFound();
        var apply = await _db.ProviderApplies.SingleOrDefaultAsync(x => x.Id == id && x.Status == 0);
        if (apply == null) return BadRequest(new { ok = false, error = "申请不存在或已审批" });
        var level = await _db.ProviderLevels.SingleOrDefaultAsync(x => x.Id == body.LevelId && x.Status == 1);
        if (level == null) return BadRequest(new { ok = false, error = "等级不存在" });
        var now = DateTime.UtcNow;
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
        await _db.SaveChangesAsync();
        apply.Status = 1;
        await _db.SaveChangesAsync();
        return Ok(new { ok = true, providerNo = provider.ProviderNo });
    }

    [HttpPost("settlements/{id:long}/approve")]
    public async Task<IActionResult> ApproveSettlement(long id)
    {
        if (!Vendor()) return NotFound();
        var row = await _db.Settlements.SingleOrDefaultAsync(x => x.Id == id && x.Status == 1);
        if (row == null || string.IsNullOrWhiteSpace(row.InvoiceFile))
            return BadRequest(new { ok = false, error = "结算单不能审批" });
        row.Status = 2;
        row.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }

    [HttpPost("settlements/{id:long}/pay")]
    public async Task<IActionResult> PaySettlement(long id, [FromBody] PayBody body)
    {
        if (!Vendor()) return NotFound();
        if (string.IsNullOrWhiteSpace(body.VoucherNo))
            return BadRequest(new { ok = false, error = "请填写付款流水号" });
        var row = await _db.Settlements.SingleOrDefaultAsync(x => x.Id == id && x.Status == 2);
        if (row == null) return BadRequest(new { ok = false, error = "结算单不能付款" });
        var now = DateTime.UtcNow;
        row.Status = 3;
        row.PayVoucherNo = body.VoucherNo.Trim();
        row.PaidAt = now;
        row.UpdatedAt = now;
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }

    [HttpPost("invoice-applies/{id:long}/issue")]
    public async Task<IActionResult> IssueInvoice(long id)
    {
        if (!Vendor()) return NotFound();
        var row = await _db.InvoiceApplies.SingleOrDefaultAsync(x => x.Id == id && x.Status == 1);
        if (row == null) return BadRequest(new { ok = false, error = "开票申请不存在" });
        row.Status = 3;
        row.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }

    [HttpPost("grants")]
    public async Task<IActionResult> Grant([FromBody] GrantBody body)
    {
        if (!Vendor()) return NotFound();
        var email = (body.Email ?? "").Trim().ToLowerInvariant();
        var account = await _db.Accounts.SingleOrDefaultAsync(x => x.Email == email);
        if (account == null) return BadRequest(new { ok = false, error = "账号不存在，后台不能新建账号" });
        var customer = await _db.Customers.SingleOrDefaultAsync(x => x.AccountId == account.Id);
        if (customer == null) return BadRequest(new { ok = false, error = "账号不存在，后台不能新建账号" });
        var product = await _db.Products.SingleAsync(x => x.ProductNo == OrderService.StandardProductNo);
        var now = DateTime.UtcNow;
        DateTime end;
        if (body.EndTime != null)
            end = body.EndTime.Value;
        else if (body.Days is > 0)
        {
            var latest = await _db.SubscriptionAuths.Where(x => x.CustomerId == customer.Id)
                .OrderByDescending(x => x.AuthEndTime).Select(x => (DateTime?)x.AuthEndTime).FirstOrDefaultAsync();
            var baseTime = latest != null && now <= latest.Value.AddDays(7) ? latest.Value : now;
            end = baseTime.AddDays(body.Days.Value);
        }
        else return BadRequest(new { ok = false, error = "请填写时长或到期日" });
        if (end <= now) return BadRequest(new { ok = false, error = "到期日必须晚于现在" });
        _db.SubscriptionAuths.Add(new SubscriptionAuth
        {
            CustomerId = customer.Id,
            ProductId = product.Id,
            AuthStartTime = now,
            AuthEndTime = end,
            Status = 1,
            GrantType = 2,
            GrantReason = body.Reason,
            CreatedAt = now,
            UpdatedAt = now
        });
        _db.OperationLogs.Add(new OperationLog
        {
            Module = "auth",
            Action = "grant",
            AfterValue = email + " " + end.ToString("yyyy-MM-dd"),
            CreatedAt = now
        });
        _db.Messages.Add(new SiteMessage
        {
            AccountId = account.Id,
            Title = "授权已发放",
            Content = "可用至 " + end.ToString("yyyy-MM-dd"),
            CreatedAt = now
        });
        await _db.SaveChangesAsync();
        return Ok(new { ok = true, authEndTime = end });
    }

    [HttpPost("products/{productNo}/price")]
    public async Task<IActionResult> ChangePrice(string productNo, [FromBody] PriceBody body)
    {
        if (!Vendor()) return NotFound();
        if (body.Price <= 0) return BadRequest(new { ok = false, error = "价格必须大于 0" });
        var product = await _db.Products.SingleOrDefaultAsync(x => x.ProductNo == productNo);
        if (product == null) return BadRequest(new { ok = false, error = "产品不存在" });
        var before = product.Price;
        product.Price = body.Price;
        product.UpdatedAt = DateTime.UtcNow;
        _db.OperationLogs.Add(new OperationLog
        {
            Module = "product",
            Action = "price",
            BeforeValue = before.ToString("0.00"),
            AfterValue = body.Price.ToString("0.00"),
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }

    [HttpGet("logs")]
    public async Task<IActionResult> Logs()
    {
        if (!Vendor()) return NotFound();
        var rows = await _db.OperationLogs.OrderByDescending(x => x.Id).Take(50)
            .Select(x => new { x.Module, x.Action, x.BeforeValue, x.AfterValue, x.CreatedAt })
            .ToListAsync();
        return Ok(new { ok = true, rows });
    }

    bool Vendor()
    {
        var env = HttpContext.RequestServices.GetRequiredService<IHostEnvironment>();
        if (!env.IsDevelopment() && !env.IsEnvironment("Testing")) return false;
        return Request.Headers.TryGetValue("X-Vendor-Key", out var key) && key == VendorKey;
    }
}
