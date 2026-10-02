using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Yxy.Api.Data;
using Yxy.Api.Services;

namespace Yxy.Api.Controllers;

public sealed record ProviderApplyBody(string? CompanyName, string? TaxNo, string? LicenseFile, string? ContactName, string? ContactEmail, string? ContactPhone);
public sealed record InvoiceApplyBody(string? TitleName, string? TaxNo, string? Email, string? LicenseFile);
public sealed record SettlementBody(string? InvoiceFile);

[ApiController]
public sealed class PortalController : ControllerBase
{
    private readonly WebDb _db;
    private readonly ClientAuthService _clients;
    private readonly OrderService _orders;
    public PortalController(WebDb db, ClientAuthService clients, OrderService orders)
    {
        _db = db;
        _clients = clients;
        _orders = orders;
    }

    [AllowAnonymous]
    [HttpGet("/api/public/price")]
    public async Task<IActionResult> Price()
    {
        await _orders.EnsureCatalogAsync();
        var product = await _db.Products.SingleAsync(x => x.ProductNo == OrderService.StandardProductNo);
        return Ok(new { ok = true, price = product.Price, name = product.Name });
    }

    [Authorize]
    [HttpGet("/api/portal/subscription")]
    public async Task<IActionResult> Subscription()
    {
        var described = await _clients.DescribeAsync(AccountId());
        var nextCheck = described.End?.AddDays(-described.NextDays);
        return Ok(new
        {
            ok = true,
            authStatus = described.Status,
            authEndTime = described.End,
            nextCheckDays = described.NextDays,
            nextCheckAt = described.Usable ? nextCheck : null,
            usable = described.Usable
        });
    }

    [Authorize]
    [HttpGet("/api/portal/promo")]
    public async Task<IActionResult> Promo()
    {
        var provider = await _db.Providers.SingleOrDefaultAsync(x => x.AccountId == AccountId() && x.Status == 1);
        if (provider == null) return Ok(new { ok = false, error = "还不是服务商" });
        return Ok(new { ok = true, providerNo = provider.ProviderNo, url = "/register?provider_no=" + provider.ProviderNo });
    }

    [Authorize]
    [HttpGet("/api/portal/commissions")]
    public async Task<IActionResult> Commissions()
    {
        var provider = await _db.Providers.SingleOrDefaultAsync(x => x.AccountId == AccountId());
        if (provider == null) return Ok(new { ok = false, error = "还不是服务商" });
        var rows = await _db.Commissions.Where(x => x.ProviderId == provider.Id)
            .OrderByDescending(x => x.Id)
            .Select(x => new { x.Id, x.OrderId, x.Amount, x.EntryType })
            .ToListAsync();
        return Ok(new { ok = true, rows });
    }

    [Authorize]
    [HttpGet("/api/portal/customers")]
    public async Task<IActionResult> Customers()
    {
        var provider = await _db.Providers.SingleOrDefaultAsync(x => x.AccountId == AccountId());
        if (provider == null) return Ok(new { ok = false, error = "还不是服务商" });
        var ids = await _db.Customers.Where(x => x.DirectProviderId == provider.Id).Select(x => x.AccountId).ToListAsync();
        var emails = await _db.Accounts.Where(x => ids.Contains(x.Id)).Select(x => x.Email).ToListAsync();
        return Ok(new { ok = true, emails });
    }

    [Authorize]
    [HttpGet("/api/portal/settlements")]
    public async Task<IActionResult> MySettlements()
    {
        var provider = await _db.Providers.SingleOrDefaultAsync(x => x.AccountId == AccountId());
        if (provider == null) return Ok(new { ok = false, error = "还不是服务商" });
        var rows = await _db.Settlements.Where(x => x.ProviderId == provider.Id).OrderByDescending(x => x.Id)
            .Select(x => new { x.SettlementNo, x.Amount, x.Status }).ToListAsync();
        return Ok(new { ok = true, rows });
    }

    [Authorize]
    [HttpGet("/api/portal/messages")]
    public async Task<IActionResult> Messages()
    {
        var rows = await _db.Messages.Where(x => x.AccountId == AccountId()).OrderByDescending(x => x.Id)
            .Select(x => new { x.Title, x.Content, x.IsRead, x.CreatedAt }).ToListAsync();
        return Ok(new { ok = true, rows });
    }

    [Authorize]
    [HttpPost("/api/portal/settlements")]
    public async Task<IActionResult> StartSettlement([FromBody] SettlementBody body)
    {
        if (string.IsNullOrWhiteSpace(body.InvoiceFile))
            return BadRequest(new { ok = false, error = "请先上传佣金发票" });
        var provider = await _db.Providers.SingleOrDefaultAsync(x => x.AccountId == AccountId() && x.Status == 1);
        if (provider == null) return BadRequest(new { ok = false, error = "还不是服务商" });
        var now = DateTime.UtcNow;
        var start = new DateTime(now.Year, now.Month, 1).AddMonths(-1);
        var end = start.AddMonths(1);
        var rows = _db.Commissions.Where(x => x.ProviderId == provider.Id && x.Settled == 0 && x.CreatedAt >= start && x.CreatedAt < end).ToList();
        if (rows.Count == 0) return BadRequest(new { ok = false, error = "没有可结算记录" });
        var net = rows.Sum(x => x.Amount);
        if (net < 100m) return BadRequest(new { ok = false, error = "未满 100 元不起结" });
        var settlement = new Settlement
        {
            SettlementNo = "S" + now.ToString("yyyyMMddHHmmssfff"),
            ProviderId = provider.Id,
            PeriodStart = start,
            PeriodEnd = end.AddDays(-1),
            Amount = net,
            Status = 1,
            InvoiceFile = body.InvoiceFile.Trim(),
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.Settlements.Add(settlement);
        await _db.SaveChangesAsync();
        foreach (var row in rows)
        {
            row.Settled = 1;
            row.SettlementId = settlement.Id;
        }
        await _db.SaveChangesAsync();
        return Ok(new { ok = true, settlementNo = settlement.SettlementNo, amount = net });
    }

    [Authorize]
    [HttpPost("/api/portal/provider-apply")]
    public async Task<IActionResult> ProviderApply([FromBody] ProviderApplyBody body)
    {
        if (string.IsNullOrWhiteSpace(body.CompanyName) || string.IsNullOrWhiteSpace(body.TaxNo)
            || string.IsNullOrWhiteSpace(body.LicenseFile) || string.IsNullOrWhiteSpace(body.ContactName)
            || string.IsNullOrWhiteSpace(body.ContactEmail) || string.IsNullOrWhiteSpace(body.ContactPhone))
            return BadRequest(new { ok = false, error = "公司名称、税号、营业执照、联系人、邮箱、手机号都要填写" });
        if (!IsImage(body.LicenseFile))
            return BadRequest(new { ok = false, error = "营业执照请上传 JPG 或 PNG" });
        var now = DateTime.UtcNow;
        _db.ProviderApplies.Add(new ProviderApply
        {
            AccountId = AccountId(),
            CompanyName = body.CompanyName.Trim(),
            TaxNo = body.TaxNo.Trim(),
            LicenseFile = body.LicenseFile.Trim(),
            ContactName = body.ContactName.Trim(),
            ContactEmail = body.ContactEmail.Trim(),
            ContactPhone = body.ContactPhone.Trim(),
            Status = 0,
            CreatedAt = now
        });
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }

    [Authorize]
    [HttpPost("/api/portal/invoice-apply")]
    public async Task<IActionResult> InvoiceApply([FromBody] InvoiceApplyBody body)
    {
        if (string.IsNullOrWhiteSpace(body.TitleName) || string.IsNullOrWhiteSpace(body.TaxNo)
            || string.IsNullOrWhiteSpace(body.Email) || string.IsNullOrWhiteSpace(body.LicenseFile))
            return BadRequest(new { ok = false, error = "抬头、税号、邮箱和营业执照都要填写" });
        if (!IsImage(body.LicenseFile))
            return BadRequest(new { ok = false, error = "营业执照请上传 JPG 或 PNG" });
        var customer = await _db.Customers.SingleAsync(x => x.AccountId == AccountId());
        var amount = await _db.Orders.Where(x => x.CustomerId == customer.Id && x.PayStatus == 1).SumAsync(x => (decimal?)x.Amount) ?? 0m;
        var now = DateTime.UtcNow;
        _db.InvoiceApplies.Add(new InvoiceApply
        {
            ApplyNo = "I" + now.ToString("yyyyMMddHHmmssfff"),
            CustomerId = customer.Id,
            LicenseFile = body.LicenseFile.Trim(),
            Amount = amount,
            TitleName = body.TitleName.Trim(),
            TaxNo = body.TaxNo.Trim(),
            Email = body.Email.Trim(),
            Issuer = 1,
            Status = 1,
            CreatedAt = now,
            UpdatedAt = now
        });
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }

    static bool IsImage(string name)
    {
        var lower = name.Trim().ToLowerInvariant();
        return lower.EndsWith(".jpg") || lower.EndsWith(".jpeg") || lower.EndsWith(".png");
    }

    long AccountId() => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
