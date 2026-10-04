using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
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
public sealed record AssignBody(string? ProviderNo);
public sealed record VendorLoginBody(string? Username, string? Password);
public sealed record MailBody(string? Host, int? Port, string? User, string? Password, string? From, bool? Ssl);
public sealed record MailTestBody(string? Email);

[ApiController]
[Route("api/vendor")]
public sealed class VendorController : ControllerBase
{
    public const string VendorKey = "yxy-vendor";
    public const string VendorScheme = "YxyVendor";
    public const string DefaultAdminUser = "admin";
    public const string DefaultAdminPassword = "Admin@yxy123";
    private readonly WebDb _db;
    private readonly MailSettingsService _mail;
    public VendorController(WebDb db, MailSettingsService mail)
    {
        _db = db;
        _mail = mail;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] VendorLoginBody body)
    {
        var username = (body.Username ?? "").Trim();
        var admin = await _db.AdminUsers.SingleOrDefaultAsync(x => x.Username == username);
        var hasher = HttpContext.RequestServices.GetRequiredService<IPasswordHasher>();
        if (admin == null || !hasher.Verify(body.Password ?? "", admin.PasswordHash))
            return BadRequest(new { ok = false, error = "账号或密码不正确" });
        await HttpContext.SignInAsync(VendorScheme, new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, admin.Username),
            new Claim(ClaimTypes.Role, "vendor")
        }, VendorScheme)));
        return Ok(new { ok = true, username = admin.Username });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(VendorScheme);
        return Ok(new { ok = true });
    }

    [HttpGet("me")]
    public IActionResult Me()
    {
        if (!Vendor()) return Unauthorized(new { ok = false, error = "未登录" });
        return Ok(new { ok = true, username = User.Identity?.Name ?? DefaultAdminUser });
    }

    [HttpGet("board")]
    public async Task<IActionResult> Board()
    {
        if (!Vendor()) return Unauthorized(new { ok = false, error = "未登录" });
        var accounts = await _db.Accounts.OrderByDescending(x => x.Id).Take(200)
            .Select(x => new { x.Id, x.Email, x.Status, x.CreatedAt }).ToListAsync();
        var emailOf = accounts.ToDictionary(x => x.Id, x => x.Email);
        var customers = await _db.Customers.OrderByDescending(x => x.Id).Take(200)
            .Select(x => new { x.Id, x.CustomerNo, x.AccountId, x.Name, x.DirectProviderId, x.Status, x.CreatedAt }).ToListAsync();
        var customerEmail = customers.ToDictionary(x => x.Id, x => emailOf.TryGetValue(x.AccountId, out var email) ? email : "");
        var orders = await _db.Orders.OrderByDescending(x => x.Id).Take(200)
            .Select(x => new { x.Id, x.OrderNo, x.CustomerId, x.OrderType, x.Amount, x.PayStatus, x.PayChannel, x.PayTime, x.CreatedAt }).ToListAsync();
        var auths = await _db.SubscriptionAuths.OrderByDescending(x => x.Id).Take(200)
            .Select(x => new { x.Id, x.CustomerId, x.AuthEndTime, x.Status, x.GrantType, x.GrantReason }).ToListAsync();
        var providers = await _db.Providers.OrderByDescending(x => x.Id).Take(200)
            .Select(x => new { x.Id, x.ProviderNo, x.Name, x.LevelId, x.Status, x.AccountId }).ToListAsync();
        var levels = await _db.ProviderLevels.OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Name, x.CommissionRate, x.Status }).ToListAsync();
        var commissions = await _db.Commissions.OrderByDescending(x => x.Id).Take(200)
            .Select(x => new { x.Id, x.OrderId, x.ProviderId, x.EntryType, x.Amount, x.Settled }).ToListAsync();
        var settlements = await _db.Settlements.OrderByDescending(x => x.Id).Take(200)
            .Select(x => new { x.Id, x.SettlementNo, x.ProviderId, x.Amount, x.Status, x.InvoiceFile }).ToListAsync();
        var invoices = await _db.InvoiceApplies.OrderByDescending(x => x.Id).Take(200)
            .Select(x => new { x.Id, x.ApplyNo, x.TitleName, x.Amount, x.Email, x.Status }).ToListAsync();
        var products = await _db.Products.OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.ProductNo, x.Name, x.Price, x.PeriodDays, x.Status }).ToListAsync();
        var logs = await _db.OperationLogs.OrderByDescending(x => x.Id).Take(50)
            .Select(x => new { x.Module, x.Action, x.BeforeValue, x.AfterValue, x.CreatedAt }).ToListAsync();
        var applies = await _db.ProviderApplies.Where(x => x.Status == 0).OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.CompanyName, x.TaxNo, x.ContactName, x.ContactEmail }).ToListAsync();
        var latestAuth = (await _db.SubscriptionAuths
            .Select(x => new { x.CustomerId, x.AuthEndTime, x.Status })
            .ToListAsync())
            .GroupBy(x => x.CustomerId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.AuthEndTime).First());
        return Ok(new
        {
            ok = true,
            accounts,
            customers = customers.Select(x =>
            {
                latestAuth.TryGetValue(x.Id, out var auth);
                return new
                {
                    x.Id, x.CustomerNo, x.Name, x.Status, x.DirectProviderId, x.CreatedAt,
                    email = emailOf.TryGetValue(x.AccountId, out var email) ? email : "",
                    authEndTime = auth == null ? (DateTime?)null : auth.AuthEndTime,
                    authStatus = auth == null ? (int?)null : auth.Status
                };
            }),
            orders = orders.Select(x => new
            {
                x.Id, x.OrderNo, x.OrderType, x.Amount, x.PayStatus, x.PayChannel, x.PayTime, x.CreatedAt,
                email = customerEmail.TryGetValue(x.CustomerId, out var email) ? email : ""
            }),
            auths = auths.Select(x => new
            {
                x.Id, x.AuthEndTime, x.Status, x.GrantType, x.GrantReason,
                email = customerEmail.TryGetValue(x.CustomerId, out var email) ? email : ""
            }),
            providers = providers.Select(x => new
            {
                x.Id, x.ProviderNo, x.Name, x.LevelId, x.Status,
                email = emailOf.TryGetValue(x.AccountId, out var email) ? email : ""
            }),
            levels,
            commissions,
            settlements,
            invoices,
            products,
            logs,
            applies
        });
    }

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
        var before = level.CommissionRate;
        level.CommissionRate = body.Rate;
        level.UpdatedAt = DateTime.UtcNow;
        _db.OperationLogs.Add(new OperationLog
        {
            Module = "level",
            Action = "rate",
            BeforeValue = before.ToString("0.00"),
            AfterValue = body.Rate.ToString("0.00"),
            CreatedAt = level.UpdatedAt
        });
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

    [HttpGet("mail")]
    public async Task<IActionResult> Mail()
    {
        if (!Vendor()) return Unauthorized(new { ok = false, error = "未登录" });
        var mail = await _mail.LoadAsync();
        return Ok(new
        {
            ok = true,
            host = mail.Host,
            port = mail.Port,
            user = mail.User,
            from = mail.From,
            ssl = mail.Ssl,
            configured = mail.Configured,
            hasPassword = !string.IsNullOrEmpty(mail.Password)
        });
    }

    [HttpPost("mail")]
    public async Task<IActionResult> SaveMail([FromBody] MailBody body)
    {
        if (!Vendor()) return NotFound();
        var error = await _mail.SaveAsync(body.Host, body.Port, body.User, body.Password, body.From, body.Ssl != false);
        if (error != null) return BadRequest(new { ok = false, error });
        return Ok(new { ok = true });
    }

    [HttpPost("mail/test")]
    public async Task<IActionResult> TestMail([FromBody] MailTestBody body)
    {
        if (!Vendor()) return NotFound();
        var email = (body.Email ?? "").Trim();
        if (!email.Contains('@')) return BadRequest(new { ok = false, error = "请填写测试收件邮箱" });
        var mail = await _mail.LoadAsync();
        if (!mail.Configured) return BadRequest(new { ok = false, error = "请先保存发信服务器" });
        try
        {
            await MailSettingsService.SendAsync(mail, email, "用心云备份发信测试", "这是一封测试邮件。收到说明发信服务器可用。");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { ok = false, error = ex.Message });
        }
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

    [HttpGet("provider-applies")]
    public async Task<IActionResult> Applies()
    {
        if (!Vendor()) return NotFound();
        var rows = await _db.ProviderApplies.Where(x => x.Status == 0).OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.CompanyName, x.TaxNo, x.ContactName, x.ContactEmail }).ToListAsync();
        return Ok(new { ok = true, rows });
    }

    [HttpGet("settlements")]
    public async Task<IActionResult> Settlements()
    {
        if (!Vendor()) return NotFound();
        var rows = await _db.Settlements.OrderByDescending(x => x.Id)
            .Select(x => new { x.Id, x.SettlementNo, x.Amount, x.Status, x.InvoiceFile }).ToListAsync();
        return Ok(new { ok = true, rows });
    }

    [HttpGet("invoice-applies")]
    public async Task<IActionResult> Invoices()
    {
        if (!Vendor()) return NotFound();
        var rows = await _db.InvoiceApplies.Where(x => x.Status == 1).OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.ApplyNo, x.TitleName, x.Amount, x.Email }).ToListAsync();
        return Ok(new { ok = true, rows });
    }

    [HttpGet("customers")]
    public async Task<IActionResult> Customers()
    {
        if (!Vendor()) return NotFound();
        var rows = await _db.Customers.OrderByDescending(x => x.Id).Take(100)
            .Select(x => new { x.Id, x.CustomerNo, x.DirectProviderId }).ToListAsync();
        return Ok(new { ok = true, rows });
    }

    [HttpPost("customers/{id:long}/provider")]
    public async Task<IActionResult> Assign(long id, [FromBody] AssignBody body)
    {
        if (!Vendor()) return NotFound();
        var customer = await _db.Customers.SingleOrDefaultAsync(x => x.Id == id);
        if (customer == null) return BadRequest(new { ok = false, error = "客户不存在" });
        long? providerId = null;
        if (!string.IsNullOrWhiteSpace(body.ProviderNo))
        {
            var provider = await _db.Providers.SingleOrDefaultAsync(x => x.ProviderNo == body.ProviderNo);
            if (provider == null) return BadRequest(new { ok = false, error = "服务商不存在" });
            providerId = provider.Id;
        }
        var before = customer.DirectProviderId?.ToString() ?? "";
        customer.DirectProviderId = providerId;
        customer.UpdatedAt = DateTime.UtcNow;
        _db.OperationLogs.Add(new OperationLog
        {
            Module = "customer",
            Action = "assign",
            BeforeValue = before,
            AfterValue = providerId?.ToString() ?? "",
            CreatedAt = customer.UpdatedAt
        });
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }

    [HttpPost("customers/{id:long}/delete")]
    public async Task<IActionResult> DeleteCustomer(long id)
    {
        if (!Vendor()) return NotFound();
        var customer = await _db.Customers.SingleOrDefaultAsync(x => x.Id == id);
        if (customer == null) return BadRequest(new { ok = false, error = "客户不存在" });
        var account = await _db.Accounts.SingleOrDefaultAsync(x => x.Id == customer.AccountId);
        if (await _db.Providers.AnyAsync(x => x.AccountId == customer.AccountId))
            return BadRequest(new { ok = false, error = "该账号是服务商，不能直接删除" });
        var orderIds = await _db.Orders.Where(x => x.CustomerId == customer.Id).Select(x => x.Id).ToListAsync();
        var payments = await _db.Payments.Where(x => orderIds.Contains(x.OrderId)).ToListAsync();
        var commissions = await _db.Commissions.Where(x => orderIds.Contains(x.OrderId)).ToListAsync();
        var orders = await _db.Orders.Where(x => x.CustomerId == customer.Id).ToListAsync();
        var auths = await _db.SubscriptionAuths.Where(x => x.CustomerId == customer.Id).ToListAsync();
        var invoices = await _db.InvoiceApplies.Where(x => x.CustomerId == customer.Id).ToListAsync();
        var messages = await _db.Messages.Where(x => x.AccountId == customer.AccountId).ToListAsync();
        var tokens = await _db.AuthTokens.Where(x => x.AccountId == customer.AccountId).ToListAsync();
        var loginLogs = await _db.LoginLogs.Where(x => x.AccountId == customer.AccountId).ToListAsync();
        var applies = await _db.ProviderApplies.Where(x => x.AccountId == customer.AccountId).ToListAsync();
        var email = account?.Email ?? "";
        var codes = string.IsNullOrEmpty(email)
            ? new List<EmailCodeRow>()
            : await _db.EmailCodes.Where(x => x.Email == email).ToListAsync();
        _db.Payments.RemoveRange(payments);
        _db.Commissions.RemoveRange(commissions);
        _db.Orders.RemoveRange(orders);
        _db.SubscriptionAuths.RemoveRange(auths);
        _db.InvoiceApplies.RemoveRange(invoices);
        _db.Messages.RemoveRange(messages);
        _db.AuthTokens.RemoveRange(tokens);
        _db.LoginLogs.RemoveRange(loginLogs);
        _db.ProviderApplies.RemoveRange(applies);
        _db.EmailCodes.RemoveRange(codes);
        _db.Customers.Remove(customer);
        if (account != null) _db.Accounts.Remove(account);
        var now = DateTime.UtcNow;
        _db.OperationLogs.Add(new OperationLog
        {
            Module = "customer",
            Action = "delete",
            BeforeValue = email,
            AfterValue = "",
            CreatedAt = now
        });
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }

    [HttpPost("providers/{id:long}/freeze")]
    public async Task<IActionResult> Freeze(long id)
    {
        if (!Vendor()) return NotFound();
        var provider = await _db.Providers.SingleOrDefaultAsync(x => x.Id == id && x.Status != 2);
        if (provider == null) return BadRequest(new { ok = false, error = "服务商不存在或已解约" });
        provider.Status = 2;
        provider.UpdatedAt = DateTime.UtcNow;
        var customers = _db.Customers.Where(x => x.DirectProviderId == provider.Id).ToList();
        foreach (var customer in customers) customer.DirectProviderId = null;
        _db.OperationLogs.Add(new OperationLog
        {
            Module = "provider",
            Action = "freeze",
            AfterValue = provider.ProviderNo,
            CreatedAt = provider.UpdatedAt
        });
        await _db.SaveChangesAsync();
        return Ok(new { ok = true, moved = customers.Count });
    }

    bool Vendor()
    {
        if (User.Identity?.IsAuthenticated == true && User.IsInRole("vendor")) return true;
        var env = HttpContext.RequestServices.GetRequiredService<IHostEnvironment>();
        if (!env.IsDevelopment() && !env.IsEnvironment("Testing")) return false;
        return Request.Headers.TryGetValue("X-Vendor-Key", out var key) && key == VendorKey;
    }
}
