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

[ApiController]
[Route("api/account")]
public sealed class AccountController : ControllerBase
{
    private readonly AccountService _accounts;
    private readonly CaptchaService _captcha;
    public AccountController(AccountService accounts, CaptchaService captcha)
    {
        _accounts = accounts;
        _captcha = captcha;
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
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, result.AccountId!.Value.ToString()),
                new Claim(ClaimTypes.Email, result.Email!)
            }, CookieAuthenticationDefaults.AuthenticationScheme)));
        return ToJson(result);
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
