using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Yxy.Api.Data;

namespace Yxy.Api.Services;

public sealed record OrderResult(bool Ok, string? Error, string? OrderNo = null, decimal? Amount = null, DateTime? AuthEndTime = null);

public sealed class OrderService
{
    public const int FormalDays = 365;
    public const int TrialDays = 30;
    public const int PayMinutes = 30;
    public const string PersonalProductNo = "PERSONAL";
    public const string StandardProductNo = "STANDARD";
    public const string ProProductNo = "PRO";

    private readonly WebDb _db;
    private readonly TimeProvider _time;
    private readonly IAlipayGateway _alipay;

    public OrderService(WebDb db, TimeProvider time, IAlipayGateway alipay)
    {
        _db = db;
        _time = time;
        _alipay = alipay;
    }

    public async Task EnsureCatalogAsync()
    {
        var now = _time.GetUtcNow().UtcDateTime;
        await EnsureTierAsync(PersonalProductNo, "个人版", "最多 3 台设备", 36m, 3, 1, now);
        await EnsureTierAsync(StandardProductNo, "标准版", "最多 10 台设备", 98m, 10, 2, now);
        await EnsureTierAsync(ProProductNo, "专业版", "最多 50 台设备", 198m, 50, 3, now);
        var legacy = await _db.Products.SingleOrDefaultAsync(x => x.ProductNo == "STD");
        if (legacy != null)
        {
            legacy.Status = 0;
            legacy.UpdatedAt = now;
        }
        await PutConfigAsync("trial_price_1", "0.10");
        await PutConfigAsync("trial_price_2", "0.50");
        await _db.SaveChangesAsync();
    }

    async Task EnsureTierAsync(string productNo, string name, string description, decimal price, int maxDevices, int sortOrder, DateTime now)
    {
        if (await _db.Products.AnyAsync(x => x.ProductNo == productNo)) return;
        _db.Products.Add(new Product
        {
            ProductNo = productNo,
            Name = name,
            Description = description,
            Price = price,
            MaxDevices = maxDevices,
            SortOrder = sortOrder,
            PeriodDays = FormalDays,
            Status = 1,
            CreatedAt = now,
            UpdatedAt = now
        });
    }

    public async Task<OrderResult> CreateAsync(long accountId, string? productNo, int orderType, string? channel)
    {
        if (channel is not "alipay") return new OrderResult(false, "请使用支付宝");
        if (orderType is not (1 or 2)) return new OrderResult(false, "订单类型不正确");
        await CloseExpiredAsync();
        var customer = await _db.Customers.SingleOrDefaultAsync(x => x.AccountId == accountId);
        if (customer == null) return new OrderResult(false, "请先注册");
        var product = await _db.Products.SingleOrDefaultAsync(x => x.ProductNo == (productNo ?? StandardProductNo));
        if (product == null || product.Status != 1) return new OrderResult(false, "产品不可购买");
        var now = _time.GetUtcNow().UtcDateTime;
        decimal amount;
        if (orderType == 2)
        {
            var paidTrials = await _db.Orders.CountAsync(x => x.CustomerId == customer.Id && x.OrderType == 2 && x.PayStatus == 1);
            if (paidTrials >= 2) return new OrderResult(false, "试用次数已用完");
            amount = decimal.Parse(await ConfigAsync(paidTrials == 0 ? "trial_price_1" : "trial_price_2"));
        }
        else amount = ProductPricing.SalePrice(product, now);

        var self = await _db.Providers.SingleOrDefaultAsync(x => x.AccountId == accountId && x.Status == 1);
        var providerId = self?.Id ?? customer.DirectProviderId;
        decimal? rate = null;
        if (orderType == 1 && providerId != null)
        {
            var provider = self ?? await _db.Providers.SingleOrDefaultAsync(x => x.Id == providerId);
            if (provider != null && provider.Status == 1)
            {
                var level = await _db.ProviderLevels.SingleOrDefaultAsync(x => x.Id == provider.LevelId);
                rate = level?.CommissionRate;
                providerId = provider.Id;
            }
        }
        var order = new SubscriptionOrder
        {
            OrderNo = "O" + now.ToString("yyyyMMddHHmmssfff") + Random.Shared.Next(1000, 9999),
            CustomerId = customer.Id,
            ProductId = product.Id,
            OrderType = orderType,
            Amount = amount,
            ProviderId = providerId,
            CommissionRateSnapshot = rate,
            PayStatus = 0,
            PayChannel = channel,
            ExpiresAt = now.AddMinutes(PayMinutes),
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();
        return new OrderResult(true, null, order.OrderNo, order.Amount);
    }

    public async Task<OrderResult> MarkPaidAsync(string? orderNo, string? tradeNo, string? channel, decimal amount)
    {
        if (string.IsNullOrWhiteSpace(orderNo) || string.IsNullOrWhiteSpace(tradeNo))
            return new OrderResult(false, "订单号或交易号为空");
        if (channel is not ("wechat" or "alipay")) return new OrderResult(false, "支付渠道不正确");
        await CloseExpiredAsync();
        var order = await _db.Orders.SingleOrDefaultAsync(x => x.OrderNo == orderNo);
        if (order == null) return new OrderResult(false, "订单不存在");
        if (order.PayStatus == 1)
        {
            if (order.TradeNo == tradeNo) return new OrderResult(true, null, order.OrderNo, order.Amount, await CurrentEndAsync(order.CustomerId));
            return new OrderResult(false, "订单已支付");
        }
        if (order.PayStatus != 0) return new OrderResult(false, "订单已关闭");
        if (order.Amount != amount) return new OrderResult(false, "金额不一致");
        if (await _db.Payments.AnyAsync(x => x.TradeNo == tradeNo)) return new OrderResult(false, "交易号已使用");

        var now = _time.GetUtcNow().UtcDateTime;
        order.PayStatus = 1;
        order.PayChannel = channel;
        order.TradeNo = tradeNo;
        order.PayTime = now;
        order.UpdatedAt = now;
        _db.Payments.Add(new PaymentRecord
        {
            OrderId = order.Id,
            Channel = channel,
            TradeNo = tradeNo,
            Amount = amount,
            CreatedAt = now
        });
        var end = await NextEndAsync(order, now);
        _db.SubscriptionAuths.Add(new SubscriptionAuth
        {
            CustomerId = order.CustomerId,
            ProductId = order.ProductId,
            OrderId = order.Id,
            AuthStartTime = now,
            AuthEndTime = end,
            Status = 1,
            GrantType = 1,
            CreatedAt = now,
            UpdatedAt = now
        });
        await WriteCommissionAsync(order, now);
        await _db.SaveChangesAsync();
        return new OrderResult(true, null, order.OrderNo, order.Amount, end);
    }

    async Task WriteCommissionAsync(SubscriptionOrder order, DateTime now)
    {
        if (order.OrderType != 1 || order.ProviderId == null || order.CommissionRateSnapshot == null) return;
        var provider = await _db.Providers.SingleOrDefaultAsync(x => x.Id == order.ProviderId);
        if (provider == null || provider.Status != 1) return;
        var exists = await _db.Commissions.AnyAsync(x => x.OrderId == order.Id && x.ProviderId == provider.Id && x.EntryType == 1);
        if (exists) return;
        var commission = decimal.Round(order.Amount * order.CommissionRateSnapshot.Value, 2, MidpointRounding.AwayFromZero);
        _db.Commissions.Add(new CommissionDetail
        {
            OrderId = order.Id,
            ProviderId = provider.Id,
            LevelId = provider.LevelId,
            CommissionRateSnapshot = order.CommissionRateSnapshot.Value,
            EntryType = 1,
            Amount = commission,
            Settled = 0,
            CreatedAt = now
        });
    }

    public async Task<OrderResult> RefundAsync(long accountId, string orderNo)
    {
        var customer = await _db.Customers.SingleOrDefaultAsync(x => x.AccountId == accountId);
        if (customer == null) return new OrderResult(false, "请先注册");
        var order = await _db.Orders.SingleOrDefaultAsync(x => x.OrderNo == orderNo && x.CustomerId == customer.Id);
        if (order == null) return new OrderResult(false, "订单不存在");
        if (order.PayStatus == 2) return new OrderResult(true, null, order.OrderNo, order.Amount);
        if (order.PayStatus != 1 || order.PayTime == null) return new OrderResult(false, "订单未支付");
        var now = _time.GetUtcNow().UtcDateTime;
        if (now > order.PayTime.Value.AddDays(7)) return new OrderResult(false, "已超过 7 天退款窗口");
        if (order.PayChannel == "alipay" && _alipay.IsConfigured)
        {
            OrderResult remote;
            try
            {
                remote = await _alipay.RefundAsync(order.OrderNo, order.Amount);
            }
            catch (Exception)
            {
                return new OrderResult(false, "支付宝退款请求失败");
            }
            if (!remote.Ok) return remote;
        }
        order.PayStatus = 2;
        order.RefundTime = now;
        order.UpdatedAt = now;
        var auth = await _db.SubscriptionAuths.SingleOrDefaultAsync(x => x.OrderId == order.Id);
        if (auth != null)
        {
            auth.Status = 0;
            auth.UpdatedAt = now;
        }
        if (order.OrderType == 1 && order.ProviderId != null)
        {
            var sale = await _db.Commissions.SingleOrDefaultAsync(x => x.OrderId == order.Id && x.ProviderId == order.ProviderId && x.EntryType == 1);
            var already = await _db.Commissions.AnyAsync(x => x.OrderId == order.Id && x.ProviderId == order.ProviderId && x.EntryType == 2);
            if (sale != null && !already)
            {
                _db.Commissions.Add(new CommissionDetail
                {
                    OrderId = order.Id,
                    ProviderId = sale.ProviderId,
                    LevelId = sale.LevelId,
                    CommissionRateSnapshot = sale.CommissionRateSnapshot,
                    EntryType = 2,
                    Amount = -sale.Amount,
                    Settled = 0,
                    CreatedAt = now
                });
            }
        }
        await _db.SaveChangesAsync();
        return new OrderResult(true, null, order.OrderNo, order.Amount);
    }

    public async Task<OrderResult> HideAsync(long accountId, string orderNo)
    {
        var customer = await _db.Customers.SingleOrDefaultAsync(x => x.AccountId == accountId);
        if (customer == null) return new OrderResult(false, "请先注册");
        var order = await _db.Orders.SingleOrDefaultAsync(x => x.OrderNo == orderNo && x.CustomerId == customer.Id && x.Hidden == 0);
        if (order == null) return new OrderResult(false, "订单不存在");
        if (order.PayStatus == 1) return new OrderResult(false, "已支付的订单请先退款后再删除");
        if (order.InvoiceApplyId != null) return new OrderResult(false, "已申请开票的记录不能删除");
        order.Hidden = 1;
        order.UpdatedAt = _time.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync();
        return new OrderResult(true, null, order.OrderNo, order.Amount);
    }

    public static string Sign(string secret, string orderNo, string tradeNo, string channel, decimal amount)
    {
        var payload = orderNo + "|" + tradeNo + "|" + channel + "|" + amount.ToString("0.00");
        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(mac).ToLowerInvariant();
    }

    async Task<DateTime> NextEndAsync(SubscriptionOrder order, DateTime payTime)
    {
        var latest = await _db.SubscriptionAuths
            .Where(x => x.CustomerId == order.CustomerId)
            .OrderByDescending(x => x.AuthEndTime)
            .Select(x => (DateTime?)x.AuthEndTime)
            .FirstOrDefaultAsync();
        if (order.OrderType == 1)
            return (latest ?? payTime).AddDays(latest == null ? FormalDays : FormalDays);
        if (latest != null && payTime <= latest.Value.AddDays(7))
            return latest.Value.AddDays(TrialDays);
        return payTime.AddDays(TrialDays);
    }

    async Task<DateTime?> CurrentEndAsync(long customerId)
    {
        return await _db.SubscriptionAuths
            .Where(x => x.CustomerId == customerId)
            .OrderByDescending(x => x.AuthEndTime)
            .Select(x => (DateTime?)x.AuthEndTime)
            .FirstOrDefaultAsync();
    }

    public async Task CloseExpiredAsync()
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var due = _db.Orders.Where(x => x.PayStatus == 0 && x.ExpiresAt != null && x.ExpiresAt < now).ToList();
        foreach (var order in due)
        {
            order.PayStatus = 3;
            order.UpdatedAt = now;
        }
        if (due.Count > 0) await _db.SaveChangesAsync();
    }

    async Task PutConfigAsync(string key, string value)
    {
        if (await _db.Configs.AnyAsync(x => x.CfgKey == key)) return;
        _db.Configs.Add(new SystemConfigRow { CfgKey = key, CfgValue = value });
    }

    async Task<string> ConfigAsync(string key)
    {
        var row = await _db.Configs.SingleAsync(x => x.CfgKey == key);
        return row.CfgValue;
    }
}
