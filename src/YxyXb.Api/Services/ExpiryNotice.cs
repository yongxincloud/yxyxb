using Microsoft.EntityFrameworkCore;

namespace YxyXb.Api.Services;

public static class ExpiryNotice
{
    public const string Subject = "用心云·循备授权即将到期";
    public const string RenewUrl = "https://backup.yongxincloud.cn/orders";

    static readonly TimeZoneInfo Shanghai = LoadShanghai();

    public static bool InWindow(DateTime authEndUtc, DateTime nowUtc)
    {
        if (nowUtc >= authEndUtc) return false;
        var days = (int)Math.Floor((authEndUtc - nowUtc).TotalDays);
        return days < 15;
    }

    public static string ShanghaiDay(DateTime utc)
    {
        return ToShanghai(utc).ToString("yyyy-MM-dd");
    }

    public static string Body(string? productName, DateTime authEndUtc, DateTime nowUtc)
    {
        var days = (int)Math.Floor((authEndUtc - nowUtc).TotalDays);
        var remain = days >= 1 ? "现在还剩 " + days + " 天" : "现在不足 1 天";
        var name = (productName ?? "").Trim();
        var who = name.Length == 0
            ? "您的用心云·循备授权"
            : "您的用心云·循备（" + name + "）授权";
        var end = ToShanghai(authEndUtc).ToString("yyyy-MM-dd HH:mm");
        var grace = ToShanghai(authEndUtc.AddDays(ClientAuthService.GraceDays)).ToString("yyyy-MM-dd HH:mm");
        return string.Join("\n", new[]
        {
            "您好，",
            "",
            who + "将于 " + end + " 到期，" + remain + "。",
            "",
            "请尽快登录门户续费：" + RenewUrl,
            "续费从原到期日往后顺延，不从付款当天重算。",
            "",
            "到期后有 " + ClientAuthService.GraceDays + " 天宽限，宽限到 " + grace + " 止。宽限期内，文件备份、数据库备份、云、UNC、FTP、邮件仍可使用。",
            "",
            "若宽限结束仍未续费，上述功能将停用。离线介质仍可使用。",
            "",
            "此邮件由系统发出，请勿直接回复。"
        });
    }

    static DateTime ToShanghai(DateTime utc)
    {
        var stamp = utc.Kind == DateTimeKind.Utc ? utc : DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(stamp, Shanghai);
    }

    static TimeZoneInfo LoadShanghai()
    {
        foreach (var id in new[] { "Asia/Shanghai", "China Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        throw new TimeZoneNotFoundException("Asia/Shanghai");
    }
}

public sealed class ExpiryNoticeService
{
    public async Task SendDueAsync(Data.WebDb db, MailSettings mail, DateTime nowUtc, Func<string, string, string, Task> send)
    {
        if (!mail.Configured) return;
        var now = nowUtc.Kind == DateTimeKind.Utc ? nowUtc : DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);
        var day = ExpiryNotice.ShanghaiDay(now);
        var auths = await db.SubscriptionAuths.Where(x => x.Status != 0).ToListAsync();
        var chosen = auths
            .GroupBy(x => x.CustomerId)
            .Select(g => g.OrderByDescending(x => x.AuthEndTime).First())
            .Where(x => x.Status == 1 && ExpiryNotice.InWindow(x.AuthEndTime, now))
            .ToList();
        if (chosen.Count == 0) return;

        var customerIds = chosen.Select(x => x.CustomerId).ToList();
        var customers = await db.Customers.Where(x => customerIds.Contains(x.Id)).ToListAsync();
        var accountIds = customers.Select(x => x.AccountId).ToList();
        var accounts = await db.Accounts.Where(x => accountIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
        var productIds = chosen.Select(x => x.ProductId).Distinct().ToList();
        var products = await db.Products.Where(x => productIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
        var already = await db.ExpiryNoticeLogs
            .Where(x => x.SentDay == day && customerIds.Contains(x.CustomerId))
            .Select(x => x.CustomerId)
            .ToListAsync();
        var sent = already.ToHashSet();

        foreach (var auth in chosen)
        {
            if (!sent.Add(auth.CustomerId)) continue;
            var customer = customers.FirstOrDefault(x => x.Id == auth.CustomerId);
            if (customer == null) continue;
            if (!accounts.TryGetValue(customer.AccountId, out var account) || account.Status != 1) continue;
            var email = (account.Email ?? "").Trim();
            if (!email.Contains('@')) continue;
            products.TryGetValue(auth.ProductId, out var product);
            var body = ExpiryNotice.Body(product?.Name, auth.AuthEndTime, now);
            try
            {
                await send(email, ExpiryNotice.Subject, body);
            }
            catch
            {
                sent.Remove(auth.CustomerId);
                continue;
            }
            db.ExpiryNoticeLogs.Add(new Data.ExpiryNoticeLog
            {
                CustomerId = auth.CustomerId,
                SentDay = day,
                CreatedAt = now
            });
            await db.SaveChangesAsync();
        }
    }
}
