using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using YxyXb.Api.Data;
using YxyXb.Api.Services;

namespace YxyXb.Api.Controllers;

public sealed record LevelBody(string? Name, decimal Rate);
public sealed record ApproveBody(long LevelId);
public sealed record PayBody(string? VoucherNo);
public sealed record GrantBody(string? Email, int? Days, DateTime? EndTime, string? Reason, string? ProductNo, int? MaxDevices);
public sealed record EditionBody(string? ProductNo, string? Reason, int? MaxDevices);
public sealed record PriceBody(decimal Price);
public sealed record ProductUpdateBody(
    decimal? Price,
    decimal? PromoPrice,
    DateTime? PromoStart,
    DateTime? PromoEnd,
    int? MaxDevices,
    int? SortOrder,
    int? Status);
public sealed record AssignBody(string? ProviderNo);
public sealed record ProviderStatusBody(int Status);
public sealed record ProviderLevelBody(long LevelId);
public sealed record VendorLoginBody(string? Username, string? Password);
public sealed record VendorLoginCodeBody(string? Email, string? Code);
public sealed record VendorSendCodeBody(string? Email, string? Scene);
public sealed record VendorResetBody(string? Email, string? Code, string? Password, string? Confirm);
public sealed record VendorProfileBody(string? Email);
public sealed record MailBody(string? Host, int? Port, string? User, string? Password, string? From, bool? Ssl);
public sealed record MailTestBody(string? Email);

[ApiController]
[Route("api/vendor")]
public sealed class VendorController : ControllerBase
{
    public const string VendorKey = "yxy-vendor";
    public const string VendorScheme = "YxyVendor";
    public const string DefaultAdminUser = "admin";
    public const string DefaultAdminPassword = "Admin@yxxt123";
    public const string LoginScene = "vendor-login";
    public const string ResetScene = "vendor-reset";
    private readonly WebDb _db;
    private readonly MailSettingsService _mail;
    private readonly EmailCodeService _codes;
    private readonly IEmailSender _email;
    private readonly IPasswordHasher _hasher;
    private readonly VendorCustomerAuthService _customerAuth;
    private readonly VendorProviderService _providers;
    private readonly VendorSettlementService _settlements;
    private readonly VendorProductService _products;
    public VendorController(
        WebDb db,
        MailSettingsService mail,
        EmailCodeService codes,
        IEmailSender email,
        IPasswordHasher hasher,
        VendorCustomerAuthService customerAuth,
        VendorProviderService providers,
        VendorSettlementService settlements,
        VendorProductService products)
    {
        _db = db;
        _mail = mail;
        _codes = codes;
        _email = email;
        _hasher = hasher;
        _customerAuth = customerAuth;
        _providers = providers;
        _settlements = settlements;
        _products = products;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] VendorLoginBody body)
    {
        var username = (body.Username ?? "").Trim();
        var admin = await _db.AdminUsers.SingleOrDefaultAsync(x => x.Username == username);
        if (admin == null || !_hasher.Verify(body.Password ?? "", admin.PasswordHash))
            return BadRequest(new { ok = false, error = "账号或密码不正确" });
        await HttpContext.SignInAsync(VendorScheme, new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, admin.Username),
            new Claim(ClaimTypes.Role, "vendor")
        }, VendorScheme)));
        return Ok(new { ok = true, username = admin.Username });
    }

    [HttpPost("send-code")]
    public async Task<IActionResult> SendCode([FromBody] VendorSendCodeBody body)
    {
        var email = AccountService.Norm(body.Email);
        if (string.IsNullOrEmpty(email) || !email.Contains('@'))
            return BadRequest(new { ok = false, error = "请填写邮箱" });
        var scene = (body.Scene ?? "reset").Trim().ToLowerInvariant();
        var codeScene = scene switch
        {
            "login" => LoginScene,
            "reset" => ResetScene,
            _ => null
        };
        if (codeScene == null)
            return BadRequest(new { ok = false, error = "场景不正确" });
        var admin = await _db.AdminUsers.AsNoTracking().SingleOrDefaultAsync(x => x.Email == email);
        if (admin == null) return Ok(new { ok = true });
        var issued = await _codes.IssueAsync(email, codeScene, ClientIp());
        if (!issued.Ok)
            return BadRequest(new { ok = false, error = issued.Error == "too_soon" ? "发送过于频繁" : "今日发送次数已达上限" });
        try
        {
            await _email.SendAsync(email, "用心云·循备厂商验证码", "验证码 " + issued.Code + " ，5 分钟内有效。");
        }
        catch (InvalidOperationException)
        {
            return BadRequest(new { ok = false, error = "邮件发送失败，请检查发信服务器" });
        }
        var env = HttpContext.RequestServices.GetRequiredService<IHostEnvironment>();
        if (env.IsDevelopment() && !await _mail.IsConfiguredAsync())
            return Ok(new { ok = true, devCode = issued.Code });
        return Ok(new { ok = true });
    }

    [HttpPost("login-code")]
    public async Task<IActionResult> LoginCode([FromBody] VendorLoginCodeBody body)
    {
        var email = AccountService.Norm(body.Email);
        if (!await _codes.ConsumeAsync(email, LoginScene, body.Code ?? ""))
            return BadRequest(new { ok = false, error = "邮箱或验证码错误" });
        var admin = await _db.AdminUsers.SingleOrDefaultAsync(x => x.Email == email);
        if (admin == null)
            return BadRequest(new { ok = false, error = "邮箱或验证码错误" });
        await HttpContext.SignInAsync(VendorScheme, new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, admin.Username),
            new Claim(ClaimTypes.Role, "vendor")
        }, VendorScheme)));
        return Ok(new { ok = true, username = admin.Username });
    }

    [HttpPost("reset")]
    public async Task<IActionResult> Reset([FromBody] VendorResetBody body)
    {
        var email = AccountService.Norm(body.Email);
        if (body.Password != body.Confirm)
            return BadRequest(new { ok = false, error = "两次密码不一致" });
        if (!PasswordRules.IsStrong(body.Password))
            return BadRequest(new { ok = false, error = "密码至少 8 位且同时包含字母和数字" });
        if (!await _codes.ConsumeAsync(email, ResetScene, body.Code ?? ""))
            return BadRequest(new { ok = false, error = "邮箱或验证码错误" });
        var admin = await _db.AdminUsers.SingleOrDefaultAsync(x => x.Email == email);
        if (admin == null)
            return BadRequest(new { ok = false, error = "邮箱或验证码错误" });
        admin.PasswordHash = _hasher.Hash(body.Password!);
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(VendorScheme);
        return Ok(new { ok = true });
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        if (!Vendor()) return Unauthorized(new { ok = false, error = "未登录" });
        var username = User.Identity?.Name ?? DefaultAdminUser;
        var admin = await _db.AdminUsers.AsNoTracking().SingleOrDefaultAsync(x => x.Username == username);
        return Ok(new { ok = true, username, email = admin?.Email });
    }

    [HttpPost("profile")]
    public async Task<IActionResult> SaveProfile([FromBody] VendorProfileBody body)
    {
        if (!Vendor()) return Unauthorized(new { ok = false, error = "未登录" });
        var email = AccountService.Norm(body.Email);
        if (string.IsNullOrEmpty(email) || !email.Contains('@'))
            return BadRequest(new { ok = false, error = "请填写有效邮箱" });
        var username = User.Identity?.Name ?? DefaultAdminUser;
        var admin = await _db.AdminUsers.SingleOrDefaultAsync(x => x.Username == username);
        if (admin == null) return BadRequest(new { ok = false, error = "账号不存在" });
        var taken = await _db.AdminUsers.AnyAsync(x => x.Email == email && x.Id != admin.Id);
        if (taken) return BadRequest(new { ok = false, error = "该邮箱已被其它管理员使用" });
        admin.Email = email;
        await _db.SaveChangesAsync();
        return Ok(new { ok = true, email });
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
            .Select(x => new { x.Id, x.CustomerId, x.ProductId, x.AuthEndTime, x.Status, x.GrantType, x.GrantReason }).ToListAsync();
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
        var products = await _db.Products.OrderBy(x => x.SortOrder).ThenBy(x => x.Id)
            .Select(x => new
            {
                x.Id, x.ProductNo, x.Name, x.Price, x.PeriodDays, x.Status,
                x.PromoPrice, x.PromoStart, x.PromoEnd, x.MaxDevices, x.SortOrder
            }).ToListAsync();
        var logs = await _db.OperationLogs.OrderByDescending(x => x.Id).Take(50)
            .Select(x => new { x.Module, x.Action, x.BeforeValue, x.AfterValue, x.CreatedAt }).ToListAsync();
        var applies = await _db.ProviderApplies.Where(x => x.Status == 0).OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.CompanyName, x.TaxNo, x.ContactName, x.ContactEmail }).ToListAsync();
        var productById = products.ToDictionary(x => x.Id, x => x);
        var latestAuth = (await _db.SubscriptionAuths
            .Select(x => new { x.CustomerId, x.ProductId, x.AuthEndTime, x.Status })
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
                productById.TryGetValue(auth?.ProductId ?? 0, out var product);
                return new
                {
                    x.Id, x.CustomerNo, x.Name, x.Status, x.DirectProviderId, x.CreatedAt,
                    email = emailOf.TryGetValue(x.AccountId, out var email) ? email : "",
                    authEndTime = auth == null ? (DateTime?)null : auth.AuthEndTime,
                    authStatus = auth == null ? (int?)null : auth.Status,
                    productNo = product?.ProductNo,
                    productName = product?.Name,
                    maxDevices = product?.MaxDevices
                };
            }),
            orders = orders.Select(x => new
            {
                x.Id, x.OrderNo, x.OrderType, x.Amount, x.PayStatus, x.PayChannel, x.PayTime, x.CreatedAt,
                email = customerEmail.TryGetValue(x.CustomerId, out var email) ? email : ""
            }),
            auths = auths.Select(x =>
            {
                productById.TryGetValue(x.ProductId, out var product);
                return new
                {
                    x.Id, x.AuthEndTime, x.Status, x.GrantType, x.GrantReason,
                    productNo = product?.ProductNo,
                    productName = product?.Name,
                    email = customerEmail.TryGetValue(x.CustomerId, out var email) ? email : ""
                };
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
        var result = await _providers.CreateLevelAsync(body.Name, body.Rate);
        return result.Ok
            ? Ok(new { ok = true, id = result.Value!.Id })
            : BadRequest(new { ok = false, error = result.Error });
    }

    [HttpPost("levels/{id:long}")]
    public async Task<IActionResult> UpdateLevel(long id, [FromBody] LevelBody body)
    {
        if (!Vendor()) return NotFound();
        var result = await _providers.UpdateLevelAsync(id, body.Rate);
        return result.Ok ? Ok(new { ok = true }) : BadRequest(new { ok = false, error = result.Error });
    }

    [HttpPost("provider-applies/{id:long}/approve")]
    public async Task<IActionResult> Approve(long id, [FromBody] ApproveBody body)
    {
        if (!Vendor()) return NotFound();
        var result = await _providers.ApproveAsync(id, body.LevelId);
        return result.Ok
            ? Ok(new { ok = true, providerNo = result.Value!.ProviderNo })
            : BadRequest(new { ok = false, error = result.Error });
    }

    [HttpPost("settlements/{id:long}/approve")]
    public async Task<IActionResult> ApproveSettlement(long id)
    {
        if (!Vendor()) return NotFound();
        var result = await _settlements.ApproveSettlementAsync(id);
        return result.Ok ? Ok(new { ok = true }) : BadRequest(new { ok = false, error = result.Error });
    }

    [HttpPost("settlements/{id:long}/pay")]
    public async Task<IActionResult> PaySettlement(long id, [FromBody] PayBody body)
    {
        if (!Vendor()) return NotFound();
        var result = await _settlements.PaySettlementAsync(id, body.VoucherNo);
        return result.Ok ? Ok(new { ok = true }) : BadRequest(new { ok = false, error = result.Error });
    }

    [HttpPost("invoice-applies/{id:long}/issue")]
    public async Task<IActionResult> IssueInvoice(long id)
    {
        if (!Vendor()) return NotFound();
        var result = await _settlements.IssueInvoiceAsync(id);
        return result.Ok ? Ok(new { ok = true }) : BadRequest(new { ok = false, error = result.Error });
    }

    [HttpPost("grants")]
    public async Task<IActionResult> Grant([FromBody] GrantBody body)
    {
        if (!Vendor()) return NotFound();
        var result = await _customerAuth.GrantAsync(
            new CustomerGrantCommand(body.Email, body.Days, body.EndTime, body.Reason, body.ProductNo, body.MaxDevices));
        return result.Ok
            ? Ok(new { ok = true, authEndTime = result.Value!.EndTime })
            : BadRequest(new { ok = false, error = result.Error });
    }

    [HttpPost("products/{productNo}/price")]
    public async Task<IActionResult> ChangePrice(string productNo, [FromBody] PriceBody body)
    {
        if (!Vendor()) return NotFound();
        var result = await _products.ChangePriceAsync(productNo, body.Price);
        return result.Ok ? Ok(new { ok = true }) : BadRequest(new { ok = false, error = result.Error });
    }

    [HttpPost("products/{productNo}")]
    public async Task<IActionResult> UpdateProduct(string productNo, [FromBody] ProductUpdateBody body)
    {
        if (!Vendor()) return NotFound();
        var result = await _products.UpdateAsync(
            productNo,
            new ProductUpdateCommand(
                body.Price,
                body.PromoPrice,
                body.PromoStart,
                body.PromoEnd,
                body.MaxDevices,
                body.SortOrder,
                body.Status));
        return result.Ok ? Ok(new { ok = true }) : BadRequest(new { ok = false, error = result.Error });
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
            await MailSettingsService.SendAsync(mail, email, "用心云·循备发信测试", "这是一封测试邮件。收到说明发信服务器可用。");
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
        var rows = await _providers.ListPendingApplicationsAsync();
        return Ok(new { ok = true, rows });
    }

    [HttpGet("settlements")]
    public async Task<IActionResult> Settlements()
    {
        if (!Vendor()) return NotFound();
        var rows = await _settlements.ListSettlementsAsync();
        return Ok(new { ok = true, rows });
    }

    [HttpGet("invoice-applies")]
    public async Task<IActionResult> Invoices()
    {
        if (!Vendor()) return NotFound();
        var rows = await _settlements.ListPendingInvoicesAsync();
        return Ok(new { ok = true, rows });
    }

    [HttpGet("devices")]
    public async Task<IActionResult> Devices([FromQuery] string? q, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (!Vendor()) return NotFound();
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 100) pageSize = 20;
        var needle = (q ?? "").Trim().ToLowerInvariant();
        var query =
            from device in _db.AccountDevices
            join account in _db.Accounts on device.AccountId equals account.Id
            join customer in _db.Customers on account.Id equals customer.AccountId into customers
            from customer in customers.DefaultIfEmpty()
            select new { device, account, customer };
        if (needle.Length > 0)
        {
            query = query.Where(x =>
                x.account.Email.ToLower().Contains(needle) ||
                (x.account.Phone ?? "").ToLower().Contains(needle) ||
                (x.customer.Name ?? "").ToLower().Contains(needle) ||
                (x.customer.Nickname ?? "").ToLower().Contains(needle) ||
                (x.customer.CompanyName ?? "").ToLower().Contains(needle) ||
                (x.customer.CustomerNo ?? "").ToLower().Contains(needle) ||
                x.device.DeviceId.ToLower().Contains(needle) ||
                (x.device.DisplayName ?? "").ToLower().Contains(needle) ||
                (x.device.Note ?? "").ToLower().Contains(needle));
        }

        var total = await query.CountAsync();
        var pages = total == 0 ? 1 : (int)Math.Ceiling(total / (double)pageSize);
        if (page > pages) page = pages;
        var rows = await query.OrderByDescending(x => x.device.LastSeenAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                email = x.account.Email,
                name = x.customer == null ? null : x.customer.Name,
                customerNo = x.customer == null ? null : x.customer.CustomerNo,
                deviceId = x.device.DeviceId,
                displayName = x.device.DisplayName,
                note = x.device.Note,
                status = x.device.Status,
                firstSeenAt = x.device.FirstSeenAt,
                lastSeenAt = x.device.LastSeenAt
            }).ToListAsync();
        return Ok(new { ok = true, rows, total, page, pageSize, pages });
    }

    [HttpGet("customers")]
    public async Task<IActionResult> Customers([FromQuery] string? q)
    {
        if (!Vendor()) return NotFound();
        var rows = await _customerAuth.SearchAsync(q);
        return Ok(new { ok = true, rows });
    }

    [HttpPost("customers/{id:long}/edition")]
    public async Task<IActionResult> ChangeEdition(long id, [FromBody] EditionBody body)
    {
        if (!Vendor()) return NotFound();
        var result = await _customerAuth.ChangeEditionAsync(
            id,
            new CustomerEditionCommand(body.ProductNo, body.Reason, body.MaxDevices));
        return result.Ok
            ? Ok(new
            {
                ok = true,
                productNo = result.Value!.ProductNo,
                productName = result.Value.ProductName,
                maxDevices = result.Value.MaxDevices
            })
            : BadRequest(new { ok = false, error = result.Error });
    }

    [HttpPost("customers/{id:long}/provider")]
    public async Task<IActionResult> Assign(long id, [FromBody] AssignBody body)
    {
        if (!Vendor()) return NotFound();
        var result = await _customerAuth.AssignProviderAsync(id, body.ProviderNo);
        return result.Ok ? Ok(new { ok = true }) : BadRequest(new { ok = false, error = result.Error });
    }

    [HttpPost("customers/{id:long}/delete")]
    public async Task<IActionResult> DeleteCustomer(long id)
    {
        if (!Vendor()) return NotFound();
        var result = await _customerAuth.DeleteCustomerAsync(id);
        return result.Ok ? Ok(new { ok = true }) : BadRequest(new { ok = false, error = result.Error });
    }

    [HttpPost("providers/{id:long}/status")]
    public async Task<IActionResult> SetProviderStatus(long id, [FromBody] ProviderStatusBody body)
    {
        if (!Vendor()) return NotFound();
        var result = await _providers.SetStatusAsync(id, body.Status);
        return result.Ok ? Ok(new { ok = true }) : BadRequest(new { ok = false, error = result.Error });
    }

    [HttpPost("providers/{id:long}/level")]
    public async Task<IActionResult> SetProviderLevel(long id, [FromBody] ProviderLevelBody body)
    {
        if (!Vendor()) return NotFound();
        var result = await _providers.SetLevelAsync(id, body.LevelId);
        return result.Ok ? Ok(new { ok = true }) : BadRequest(new { ok = false, error = result.Error });
    }

    [HttpPost("providers/{id:long}/freeze")]
    public async Task<IActionResult> Freeze(long id)
    {
        if (!Vendor()) return NotFound();
        var result = await _providers.FreezeAsync(id);
        return result.Ok
            ? Ok(new { ok = true, moved = result.Value!.Moved })
            : BadRequest(new { ok = false, error = result.Error });
    }

    bool Vendor()
    {
        if (User.Identity?.IsAuthenticated == true && User.IsInRole("vendor")) return true;
        var env = HttpContext.RequestServices.GetRequiredService<IHostEnvironment>();
        if (!env.IsDevelopment() && !env.IsEnvironment("Testing")) return false;
        return Request.Headers.TryGetValue("X-Vendor-Key", out var key) && key == VendorKey;
    }

    string? ClientIp()
    {
        var env = HttpContext.RequestServices.GetRequiredService<IHostEnvironment>();
        if (env.IsEnvironment("Testing") && Request.Headers.TryGetValue("X-Test-Ip", out var testIp))
            return testIp.ToString();
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }
}
