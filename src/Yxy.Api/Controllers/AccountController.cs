using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Yxy.Api.Services;

namespace Yxy.Api.Controllers;

public sealed record ApiResult(bool Ok, string? Error, string? Email = null);
public sealed record SendCodeBody(string? Email, string? Scene, string? CaptchaId, string? CaptchaAnswer);
public sealed record LoginBody(string? Email, string? Password);
public sealed record LoginCodeBody(string? Email, string? Code);
public sealed record ResetBody(string? Email, string? Code, string? Password, string? Confirm);

[ApiController]
[Route("api/account")]
public sealed class AccountController : ControllerBase
{
    private readonly AccountService _accounts;
    private readonly CaptchaService _captcha;
    private readonly MailSettingsService _mail;
    public AccountController(AccountService accounts, CaptchaService captcha, MailSettingsService mail)
    {
        _accounts = accounts;
        _captcha = captcha;
        _mail = mail;
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
