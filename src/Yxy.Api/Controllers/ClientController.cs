using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Yxy.Api.Services;

namespace Yxy.Api.Controllers;

public sealed record ClientLoginBody(string? Email, string? Password, string? DeviceId);
public sealed record ClientCodeBody(string? Email, string? Code, string? DeviceId);
public sealed record ClientVerifyBody(string? Token, string? DeviceId);
public sealed record ClientRenewBody(string? Token);

[ApiController]
[Route("api/client")]
public sealed class ClientController : ControllerBase
{
    private readonly ClientAuthService _clients;
    public ClientController(ClientAuthService clients) => _clients = clients;

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] ClientLoginBody body)
    {
        var session = await _clients.LoginPasswordAsync(body.Email, body.Password, body.DeviceId, ClientIp());
        return session.Ok ? Ok(session) : BadRequest(session);
    }

    [HttpPost("login-code")]
    public async Task<IActionResult> LoginCode([FromBody] ClientCodeBody body)
    {
        var session = await _clients.LoginCodeAsync(body.Email, body.Code, body.DeviceId, ClientIp());
        return session.Ok ? Ok(session) : BadRequest(session);
    }

    [HttpPost("verify")]
    public async Task<IActionResult> Verify([FromBody] ClientVerifyBody body)
    {
        var session = await _clients.VerifyAsync(body.Token, body.DeviceId);
        return session.Ok ? Ok(session) : BadRequest(session);
    }

    [HttpPost("renew")]
    public async Task<IActionResult> Renew([FromBody] ClientRenewBody body)
    {
        var session = await _clients.RenewAsync(body.Token);
        return session.Ok ? Ok(session) : BadRequest(session);
    }

    string? ClientIp()
    {
        var env = HttpContext.RequestServices.GetRequiredService<IHostEnvironment>();
        if (env.IsEnvironment("Testing") && Request.Headers.TryGetValue("X-Test-Ip", out var testIp))
            return testIp.ToString();
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }
}
