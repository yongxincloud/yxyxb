using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
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
    private readonly IConfiguration _config;
    public OrderController(OrderService orders, WebDb db, IConfiguration config)
    {
        _orders = orders;
        _db = db;
        _config = config;
    }

    [Authorize]
    [HttpGet("catalog")]
    public async Task<IActionResult> Catalog()
    {
        await _orders.EnsureCatalogAsync();
        var now = DateTime.UtcNow;
        var plans = await _db.Products.Where(x => x.Status == 1)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .ToListAsync();
        var trial1 = await _db.Configs.SingleAsync(x => x.CfgKey == "trial_price_1");
        var trial2 = await _db.Configs.SingleAsync(x => x.CfgKey == "trial_price_2");
        var standard = plans.FirstOrDefault(x => x.ProductNo == OrderService.StandardProductNo) ?? plans.First();
        return Ok(new
        {
            ok = true,
            productNo = standard.ProductNo,
            name = standard.Name,
            price = ProductPricing.SalePrice(standard, now),
            trialPrice1 = decimal.Parse(trial1.CfgValue),
            trialPrice2 = decimal.Parse(trial2.CfgValue),
            plans = plans.Select(p => new
            {
                productNo = p.ProductNo,
                name = p.Name,
                maxDevices = p.MaxDevices,
                listPrice = p.Price,
                salePrice = ProductPricing.SalePrice(p, now),
                promoActive = ProductPricing.IsPromoActive(p, now),
                promoEnd = p.PromoEnd,
                sortOrder = p.SortOrder
            })
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
        var rows = await _db.Orders.Where(x => x.CustomerId == customer.Id && x.Hidden == 0)
            .OrderByDescending(x => x.Id)
            .Select(x => new { x.OrderNo, x.OrderType, x.Amount, x.PayStatus, x.PayChannel })
            .ToListAsync();
        return Ok(new { ok = true, orders = rows });
    }

    [Authorize]
    [HttpPost("{orderNo}/delete")]
    public async Task<IActionResult> Delete(string orderNo)
    {
        var result = await _orders.HideAsync(AccountId(), orderNo);
        return result.Ok ? Ok(result) : BadRequest(result);
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
        var result = await _orders.MarkPaidAsync(order.OrderNo, trade, order.PayChannel ?? "alipay", order.Amount);
        return result.Ok ? Ok(result) : BadRequest(result);
    }

    [Authorize]
    [HttpPost("{orderNo}/alipay")]
    public async Task<IActionResult> StartAlipay(string orderNo)
    {
        var appId = _config["Alipay:AppId"];
        var privateKey = _config["Alipay:PrivateKey"];
        var notifyUrl = _config["Alipay:NotifyUrl"];
        if (string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(privateKey))
            return BadRequest(new { ok = false, error = "支付宝未配置" });
        if (string.IsNullOrWhiteSpace(notifyUrl) || !notifyUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { ok = false, error = "请先配置支付宝异步通知地址" });

        await _orders.CloseExpiredAsync();
        var order = await _db.Orders.SingleOrDefaultAsync(x => x.OrderNo == orderNo);
        if (order == null) return BadRequest(new { ok = false, error = "订单不存在" });
        var customer = await _db.Customers.SingleAsync(x => x.AccountId == AccountId());
        if (order.CustomerId != customer.Id) return BadRequest(new { ok = false, error = "订单不存在" });
        if (order.PayChannel != "alipay") return BadRequest(new { ok = false, error = "请选择支付宝" });
        if (order.PayStatus == 1) return BadRequest(new { ok = false, error = "订单已支付" });
        if (order.PayStatus != 0) return BadRequest(new { ok = false, error = "订单已关闭" });

        var subject = order.OrderType == 2 ? "用心云备份试用" : "用心云备份标准版";
        string payUrl;
        try
        {
            payUrl = AlipayPay.BuildPagePayUrl(appId, privateKey, notifyUrl, _config["Alipay:ReturnUrl"], order.OrderNo, subject, order.Amount, ChinaNow());
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            return BadRequest(new { ok = false, error = "支付宝密钥无效" });
        }
        return Ok(new { ok = true, payUrl });
    }

    [HttpPost("/api/payments/alipay/notify")]
    public async Task<IActionResult> AlipayNotify()
    {
        var form = Request.HasFormContentType
            ? Request.Form.ToDictionary(item => item.Key, item => item.Value.ToString())
            : new Dictionary<string, string>();
        var publicKey = _config["Alipay:AlipayPublicKey"] ?? "";
        var appId = _config["Alipay:AppId"] ?? "";
        if (!form.TryGetValue("sign", out var sign) || !AlipayPay.Verify(form, sign, publicKey, excludeSignType: true))
            return Content("failure", "text/plain");
        if (!form.TryGetValue("app_id", out var gotApp) || gotApp != appId)
            return Content("failure", "text/plain");
        var status = form.GetValueOrDefault("trade_status");
        if (status is not ("TRADE_SUCCESS" or "TRADE_FINISHED"))
            return Content("success", "text/plain");
        if (!decimal.TryParse(form.GetValueOrDefault("total_amount"), NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
            return Content("failure", "text/plain");
        var result = await _orders.MarkPaidAsync(form.GetValueOrDefault("out_trade_no"), form.GetValueOrDefault("trade_no"), "alipay", amount);
        return Content(result.Ok ? "success" : "failure", "text/plain");
    }

    static DateTime ChinaNow()
    {
        var zoneId = OperatingSystem.IsWindows() ? "China Standard Time" : "Asia/Shanghai";
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(zoneId));
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
