using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Yxy.Api.Data;
using Yxy.Api.Services;

namespace Yxy.Api.Controllers;

public sealed record CreateOrderBody(string? ProductNo, int OrderType, string? Channel);
public sealed record PayNotifyBody(string? OrderNo, string? TradeNo, string? Channel, decimal Amount);

[ApiController]
[Route("api/orders")]
public sealed class OrderController : ControllerBase
{
    public const string DevPaySecret = "yxy-dev-pay";

    private readonly OrderService _orders;
    private readonly WebDb _db;
    public OrderController(OrderService orders, WebDb db)
    {
        _orders = orders;
        _db = db;
    }

    [Authorize]
    [HttpGet("catalog")]
    public async Task<IActionResult> Catalog()
    {
        await _orders.EnsureCatalogAsync();
        var product = await _db.Products.SingleAsync(x => x.ProductNo == OrderService.StandardProductNo);
        var trial1 = await _db.Configs.SingleAsync(x => x.CfgKey == "trial_price_1");
        var trial2 = await _db.Configs.SingleAsync(x => x.CfgKey == "trial_price_2");
        return Ok(new
        {
            ok = true,
            productNo = product.ProductNo,
            name = product.Name,
            price = product.Price,
            trialPrice1 = decimal.Parse(trial1.CfgValue),
            trialPrice2 = decimal.Parse(trial2.CfgValue)
        });
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrderBody body)
    {
        var result = await _orders.CreateAsync(AccountId(), body.ProductNo, body.OrderType, body.Channel);
        return result.Ok ? Ok(result) : BadRequest(result);
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> List()
    {
        await _orders.CloseExpiredAsync();
        var customer = await _db.Customers.SingleAsync(x => x.AccountId == AccountId());
        var rows = await _db.Orders.Where(x => x.CustomerId == customer.Id)
            .OrderByDescending(x => x.Id)
            .Select(x => new { x.OrderNo, x.OrderType, x.Amount, x.PayStatus, x.PayChannel })
            .ToListAsync();
        return Ok(new { ok = true, orders = rows });
    }

    [Authorize]
    [HttpPost("{orderNo}/refund")]
    public async Task<IActionResult> Refund(string orderNo)
    {
        var result = await _orders.RefundAsync(AccountId(), orderNo);
        return result.Ok ? Ok(result) : BadRequest(result);
    }

    [Authorize]
    [HttpPost("{orderNo}/mock-pay")]
    public async Task<IActionResult> MockPay(string orderNo)
    {
        var env = HttpContext.RequestServices.GetRequiredService<IHostEnvironment>();
        if (!env.IsDevelopment() && !env.IsEnvironment("Testing")) return NotFound();
        var order = await _db.Orders.SingleOrDefaultAsync(x => x.OrderNo == orderNo);
        if (order == null) return BadRequest(new OrderResult(false, "订单不存在"));
        var customer = await _db.Customers.SingleAsync(x => x.AccountId == AccountId());
        if (order.CustomerId != customer.Id) return BadRequest(new OrderResult(false, "订单不存在"));
        var trade = "DEV" + order.OrderNo;
        var result = await _orders.MarkPaidAsync(order.OrderNo, trade, order.PayChannel ?? "wechat", order.Amount);
        return result.Ok ? Ok(result) : BadRequest(result);
    }

    [HttpPost("/api/payments/notify")]
    public async Task<IActionResult> Notify([FromBody] PayNotifyBody body)
    {
        var payload = OrderService.Sign(DevPaySecret, body.OrderNo ?? "", body.TradeNo ?? "", body.Channel ?? "", body.Amount);
        if (!Request.Headers.TryGetValue("X-Pay-Sign", out var sign) || sign.ToString() != payload)
            return BadRequest(new OrderResult(false, "验签失败"));
        var result = await _orders.MarkPaidAsync(body.OrderNo, body.TradeNo, body.Channel, body.Amount);
        return result.Ok ? Ok(result) : BadRequest(result);
    }

    long AccountId() => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
