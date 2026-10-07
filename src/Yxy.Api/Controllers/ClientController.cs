using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Yxy.Api.Data;
using Yxy.Api.Services;

namespace Yxy.Api.Controllers;

public sealed record ClientLoginBody(string? Email, string? Password, string? DeviceId);
public sealed record ClientCodeBody(string? Email, string? Code, string? DeviceId);
public sealed record ClientVerifyBody(string? Token, string? DeviceId);
public sealed record ClientRenewBody(string? Token);
public sealed record ClientDevicesBody(string? Token);
public sealed record ClientKickBody(string? Token, string? DeviceId);

[ApiController]
[Route("api/client")]
public sealed class ClientController : ControllerBase
{
    private readonly ClientAuthService _clients;
    private readonly DeviceRegistryService _devices;
    private readonly WebDb _db;
    public ClientController(ClientAuthService clients, DeviceRegistryService devices, WebDb db)
    {
        _clients = clients;
        _devices = devices;
        _db = db;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] ClientLoginBody body) =>
        ToResult(await _clients.LoginPasswordAsync(body.Email, body.Password, body.DeviceId, ClientIp()));

    [HttpPost("login-code")]
    public async Task<IActionResult> LoginCode([FromBody] ClientCodeBody body) =>
        ToResult(await _clients.LoginCodeAsync(body.Email, body.Code, body.DeviceId, ClientIp()));

    [HttpPost("verify")]
    public async Task<IActionResult> Verify([FromBody] ClientVerifyBody body) =>
        ToResult(await _clients.VerifyAsync(body.Token, body.DeviceId));

    [HttpPost("renew")]
    public async Task<IActionResult> Renew([FromBody] ClientRenewBody body) =>
        ToResult(await _clients.RenewAsync(body.Token));

    [HttpPost("devices")]
    public async Task<IActionResult> Devices([FromBody] ClientDevicesBody body)
    {
        var accountId = await _clients.ResolveAccountIdAsync(body.Token);
        if (accountId == null) return BadRequest(new { ok = false, error = "登录已失效" });
        var max = await CurrentMaxDevicesAsync(accountId.Value);
        var devices = await _devices.ListActiveAsync(accountId.Value);
        return Ok(new { ok = true, maxDevices = max, devices });
    }

    [HttpPost("devices/kick")]
    public async Task<IActionResult> Kick([FromBody] ClientKickBody body)
    {
        var accountId = await _clients.ResolveAccountIdAsync(body.Token);
        if (accountId == null) return BadRequest(new { ok = false, error = "登录已失效" });
        if (string.IsNullOrWhiteSpace(body.DeviceId))
            return BadRequest(new { ok = false, error = "缺少设备标识" });
        var ok = await _devices.KickAsync(accountId.Value, body.DeviceId);
        return ok ? Ok(new { ok = true }) : BadRequest(new { ok = false, error = "设备不存在或已踢出" });
    }

    async Task<int?> CurrentMaxDevicesAsync(long accountId)
    {
        var customer = await _db.Customers.SingleOrDefaultAsync(x => x.AccountId == accountId);
        if (customer == null) return null;
        var auth = await _db.SubscriptionAuths
            .Where(x => x.CustomerId == customer.Id && x.Status == 1)
            .OrderByDescending(x => x.AuthEndTime)
            .FirstOrDefaultAsync();
        if (auth == null) return null;
        var product = await _db.Products.SingleOrDefaultAsync(x => x.Id == auth.ProductId);
        return product?.MaxDevices;
    }

    IActionResult ToResult(ClientSession session)
    {
        if (session.Ok) return Ok(session);
        if (session.ErrorCode == "DEVICE_LIMIT")
            return Conflict(new
            {
                ok = false,
                error = session.Error,
                errorCode = session.ErrorCode,
                maxDevices = session.MaxDevices,
                deviceCount = session.DeviceCount,
                productNo = session.ProductNo
            });
        return BadRequest(new { ok = false, error = session.Error, errorCode = session.ErrorCode });
    }

    string? ClientIp()
    {
        var env = HttpContext.RequestServices.GetRequiredService<IHostEnvironment>();
        if (env.IsEnvironment("Testing") && Request.Headers.TryGetValue("X-Test-Ip", out var testIp))
            return testIp.ToString();
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }
}
