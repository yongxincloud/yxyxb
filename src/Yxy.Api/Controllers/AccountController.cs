using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using Yxy.Api.Data;
using Yxy.Api.Services;

namespace Yxy.Api.Controllers;

public sealed record ApiResult(bool Ok, string? Error, string? Email = null);
public sealed record SendCodeBody(string? Email, string? Scene, string? CaptchaId, string? CaptchaAnswer);
public sealed record LoginBody(string? Email, string? Password);
public sealed record LoginCodeBody(string? Email, string? Code);
public sealed record ResetBody(string? Email, string? Code, string? Password, string? Confirm);
public sealed record ProfileBody(string? Name, string? Nickname, string? CompanyName, string? Phone, string? InvoiceTitle, string? InvoiceTaxNo, string? InvoiceEmail, string? InvoiceLicense);

[ApiController]
[Route("api/account")]
public sealed class AccountController : ControllerBase
{
    private readonly AccountService _accounts;
    private readonly CaptchaService _captcha;
    private readonly MailSettingsService _mail;
    private readonly WebDb _db;
    public AccountController(AccountService accounts, CaptchaService captcha, MailSettingsService mail, WebDb db)
    {
        _accounts = accounts;
        _captcha = captcha;
        _mail = mail;
        _db = db;
    }

    [HttpGet("captcha")]
    public IActionResult Captcha()
    {
        var view = _captcha.Create();
        return Ok(new { id = view.Id, svg = view.Svg });
    }

    [HttpPost("send-code")]
    public async Task<IActionResult> SendCode([FromBody] SendCodeBody body)
    {
        var result = await _accounts.SendCodeAsync(body.Email, body.Scene, body.CaptchaId, body.CaptchaAnswer, ClientIp());
        if (!result.Ok) return ToJson(result);
        var env = HttpContext.RequestServices.GetRequiredService<IHostEnvironment>();
        if (env.IsDevelopment() && !await _mail.IsConfiguredAsync() && !string.IsNullOrEmpty(result.DevCode))
            return Ok(new { ok = true, error = (string?)null, devCode = result.DevCode });
        return ToJson(result);
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterInput body)
    {
        var result = await _accounts.RegisterAsync(body, ClientIp());
        return ToJson(result);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginBody body)
    {
        var result = await _accounts.LoginPasswordAsync(body.Email, body.Password, ClientIp());
        if (!result.Ok) return ToJson(result);
        await SignIn(result);
        return ToJson(result);
    }

    [HttpPost("login-code")]
    public async Task<IActionResult> LoginCode([FromBody] LoginCodeBody body)
    {
        var result = await _accounts.LoginCodeAsync(body.Email, body.Code, ClientIp());
        if (!result.Ok) return ToJson(result);
        await SignIn(result);
        return ToJson(result);
    }

    [HttpPost("reset")]
    public async Task<IActionResult> Reset([FromBody] ResetBody body)
    {
        var result = await _accounts.ResetPasswordAsync(body.Email, body.Code, body.Password, body.Confirm);
        return ToJson(result);
    }

    async Task SignIn(AuthResult result)
    {
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, result.AccountId!.Value.ToString()),
                new Claim(ClaimTypes.Email, result.Email!)
            }, CookieAuthenticationDefaults.AuthenticationScheme)));
    }

    [Authorize]
    [HttpGet("me")]
    public IActionResult Me() => Ok(new ApiResult(true, null, User.FindFirstValue(ClaimTypes.Email)));

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Ok(new { ok = true });
    }

    [Authorize]
    [HttpGet("profile")]
    public async Task<IActionResult> Profile()
    {
        var account = await _db.Accounts.SingleAsync(x => x.Id == AccountId());
        var customer = await _db.Customers.SingleAsync(x => x.AccountId == account.Id);
        return Ok(new
        {
            ok = true,
            email = account.Email,
            phone = account.Phone,
            name = customer.Name,
            nickname = customer.Nickname,
            companyName = customer.CompanyName,
            invoiceTitle = customer.InvoiceTitle,
            invoiceTaxNo = customer.InvoiceTaxNo,
            invoiceEmail = customer.InvoiceEmail,
            invoiceLicense = customer.InvoiceLicense
        });
    }

    [Authorize]
    [HttpPost("profile")]
    public async Task<IActionResult> SaveProfile([FromBody] ProfileBody body)
    {
        if (!string.IsNullOrWhiteSpace(body.InvoiceLicense) && !IsImage(body.InvoiceLicense))
            return BadRequest(new { ok = false, error = "营业执照请上传 JPG 或 PNG" });
        var account = await _db.Accounts.SingleAsync(x => x.Id == AccountId());
        var customer = await _db.Customers.SingleAsync(x => x.AccountId == account.Id);
        var now = DateTime.UtcNow;
        account.Phone = Clean(body.Phone);
        account.UpdatedAt = now;
        customer.Name = Clean(body.Name);
        customer.Nickname = Clean(body.Nickname);
        customer.CompanyName = Clean(body.CompanyName);
        customer.InvoiceTitle = Clean(body.InvoiceTitle);
        customer.InvoiceTaxNo = Clean(body.InvoiceTaxNo);
        customer.InvoiceEmail = Clean(body.InvoiceEmail);
        customer.InvoiceLicense = Clean(body.InvoiceLicense);
        customer.UpdatedAt = now;
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }

    static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    static bool IsImage(string name)
    {
        var lower = name.Trim().ToLowerInvariant();
        return lower.EndsWith(".jpg") || lower.EndsWith(".jpeg") || lower.EndsWith(".png");
    }

    long AccountId() => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    protected IActionResult ToJson(AuthResult result)
    {
        var body = new ApiResult(result.Ok, result.Error, result.Ok ? result.Email : null);
        return result.Ok ? Ok(body) : BadRequest(body);
    }

    protected string? ClientIp()
    {
        var env = HttpContext.RequestServices.GetRequiredService<IHostEnvironment>();
        if (env.IsEnvironment("Testing") && Request.Headers.TryGetValue("X-Test-Ip", out var testIp))
            return testIp.ToString();
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }
}
