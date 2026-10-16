using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using YxyXb.Api.Data;
using YxyXb.Api.Services;

namespace YxyXb.Api.Controllers;

public sealed record ClientLoginBody(string? Email, string? Password, string? DeviceId, string? DisplayName, string? Note);
public sealed record ClientCodeBody(string? Email, string? Code, string? DeviceId, string? DisplayName, string? Note);
public sealed record ClientVerifyBody(string? Token, string? DeviceId, string? DisplayName, string? Note);
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
        ToResult(await _clients.LoginPasswordAsync(body.Email, body.Password, body.DeviceId, ClientIp(), body.DisplayName, body.Note));

    [HttpPost("login-code")]
    public async Task<IActionResult> LoginCode([FromBody] ClientCodeBody body) =>
        ToResult(await _clients.LoginCodeAsync(body.Email, body.Code, body.DeviceId, ClientIp(), body.DisplayName, body.Note));

    [HttpPost("verify")]
    public async Task<IActionResult> Verify([FromBody] ClientVerifyBody body) =>
        ToResult(await _clients.VerifyAsync(body.Token, body.DeviceId, body.DisplayName, body.Note));

    [HttpPost("renew")]
    public async Task<IActionResult> Renew([FromBody] ClientRenewBody body) =>
        ToResult(await _clients.RenewAsync(body.Token));

    [HttpPost("devices")]
    public async Task<IActionResult> Devices([FromBody] ClientDevicesBody body)
    {
        var accountId = await _clients.ResolveAccountIdAsync(body.Token);
        if (accountId == null) return BadRequest(new { ok = false, error = "登录已失效" });
        var edition = await CurrentEditionAsync(accountId.Value);
        var product = edition.Product;
        var devices = await _devices.ListActiveAsync(accountId.Value);
        return Ok(new
        {
            ok = true,
            maxDevices = edition.MaxDevices,
            productNo = product?.ProductNo,
            productName = product?.Name,
            devices
        });
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

    async Task<(Product? Product, int? MaxDevices)> CurrentEditionAsync(long accountId)
    {
        var customer = await _db.Customers.SingleOrDefaultAsync(x => x.AccountId == accountId);
        if (customer == null) return (null, null);
        var auth = await _db.SubscriptionAuths
            .Where(x => x.CustomerId == customer.Id && x.Status == 1)
            .OrderByDescending(x => x.AuthEndTime)
            .FirstOrDefaultAsync();
        if (auth == null) return (null, null);
        var product = await _db.Products.SingleOrDefaultAsync(x => x.Id == auth.ProductId);
        if (product == null) return (null, null);
        return (product, OrderService.DeviceCap(product.ProductNo, product.MaxDevices, auth.DeviceLimit));
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
                token = session.Token,
                valid = false,
                authStatus = "device_limit",
                authEndTime = session.AuthEndTime,
                nextCheckDays = session.NextCheckDays,
                name = session.Name,
                nickname = session.Nickname,
                companyName = session.CompanyName,
                maxDevices = session.MaxDevices,
                deviceCount = session.DeviceCount,
                productNo = session.ProductNo,
                productName = session.ProductName
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
