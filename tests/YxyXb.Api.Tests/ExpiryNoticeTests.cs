using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using YxyXb.Api.Data;
using YxyXb.Api.Services;

namespace YxyXb.Api.Tests;

public class ExpiryNoticeTests
{
    static readonly DateTime Now = new(2026, 11, 5, 2, 30, 0, DateTimeKind.Utc);
    static readonly DateTime End = new(2026, 11, 15, 2, 30, 0, DateTimeKind.Utc);

    [Test]
    public void InWindow_OnlyWhileWholeDaysRemainUnder15()
    {
        Assert.That(ExpiryNotice.InWindow(new DateTime(2026, 11, 16, 2, 0, 0, DateTimeKind.Utc), new DateTime(2026, 11, 1, 2, 0, 0, DateTimeKind.Utc)), Is.False);
        Assert.That(ExpiryNotice.InWindow(new DateTime(2026, 11, 15, 2, 0, 0, DateTimeKind.Utc), new DateTime(2026, 11, 1, 2, 0, 0, DateTimeKind.Utc)), Is.True);
        Assert.That(ExpiryNotice.InWindow(End, End), Is.False);
        Assert.That(ExpiryNotice.InWindow(End, End.AddHours(-1)), Is.True);
    }

    [Test]
    public void ShanghaiDay_UsesBeijingCalendar()
    {
        Assert.That(ExpiryNotice.ShanghaiDay(new DateTime(2026, 10, 31, 15, 59, 0, DateTimeKind.Utc)), Is.EqualTo("2026-10-31"));
        Assert.That(ExpiryNotice.ShanghaiDay(new DateTime(2026, 10, 31, 16, 0, 0, DateTimeKind.Utc)), Is.EqualTo("2026-11-01"));
    }

    [Test]
    public void Body_StatesExpiryGraceAndWhatStops()
    {
        var body = ExpiryNotice.Body("标准版", End, Now);
        Assert.That(body, Is.EqualTo(string.Join("\n", new[]
        {
            "您好，",
            "",
            "您的用心云·循备（标准版）授权将于 2026-11-15 10:30 到期，现在还剩 10 天。",
            "",
            "请尽快登录门户续费：https://backup.yongxincloud.cn/orders",
            "续费从原到期日往后顺延，不从付款当天重算。",
            "",
            "到期后有 7 天宽限，宽限到 2026-11-22 10:30 止。宽限期内，文件备份、数据库备份、云、UNC、FTP、邮件仍可使用。",
            "",
            "若宽限结束仍未续费，上述功能将停用。离线介质仍可使用。",
            "",
            "此邮件由系统发出，请勿直接回复。"
        })));
        Assert.That(ExpiryNotice.Body("  ", End, End.AddHours(-1)), Does.Contain("您的用心云·循备授权将于 2026-11-15 10:30 到期，现在不足 1 天。"));
    }

    [Test]
    public async Task SendDue_MailsLoginAddressOncePerShanghaiDay()
    {
        await using var hold = new DbHold();
        var id = await Seed(hold.Db, End, accountStatus: 1, authStatus: 1, invoiceEmail: "invoice@example.com");
        var mail = ReadyMail();
        var sent = new List<(string To, string Subject, string Body)>();
        var service = new ExpiryNoticeService();

        await service.SendDueAsync(hold.Db, mail, Now, Capture(sent));
        await service.SendDueAsync(hold.Db, mail, Now.AddHours(6), Capture(sent));

        Assert.That(sent, Has.Count.EqualTo(1));
        Assert.That(sent[0].To, Is.EqualTo("login@example.com"));
        Assert.That(sent[0].Subject, Is.EqualTo("用心云·循备授权即将到期"));
        Assert.That(sent[0].Body, Does.Contain("标准版"));
        Assert.That(await hold.Db.ExpiryNoticeLogs.CountAsync(x => x.CustomerId == id), Is.EqualTo(1));

        await service.SendDueAsync(hold.Db, mail, new DateTime(2026, 11, 5, 16, 0, 0, DateTimeKind.Utc), Capture(sent));
        Assert.That(sent, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task SendDue_SkipsClosedCasesAndRetriesFailedSend()
    {
        await using var hold = new DbHold();
        await Seed(hold.Db, End, accountStatus: 0, authStatus: 1, email: "disabled@example.com");
        await Seed(hold.Db, End, accountStatus: 1, authStatus: 3, email: "paused@example.com");
        await Seed(hold.Db, Now.AddHours(-1), accountStatus: 1, authStatus: 1, email: "grace@example.com");
        await Seed(hold.Db, Now.AddDays(15), accountStatus: 1, authStatus: 1, email: "far@example.com");
        var retryId = await Seed(hold.Db, End, accountStatus: 1, authStatus: 1, email: "retry@example.com");
        var mail = ReadyMail();
        var sent = new List<string>();
        var service = new ExpiryNoticeService();

        await service.SendDueAsync(hold.Db, mail, Now, (_, _, _) => throw new InvalidOperationException("smtp down"));
        Assert.That(await hold.Db.ExpiryNoticeLogs.CountAsync(x => x.CustomerId == retryId), Is.EqualTo(0));

        await service.SendDueAsync(hold.Db, mail, Now, (to, _, _) =>
        {
            sent.Add(to);
            return Task.CompletedTask;
        });
        await service.SendDueAsync(hold.Db, new MailSettings("", 587, "", "", "", false), Now, (to, _, _) =>
        {
            sent.Add(to);
            return Task.CompletedTask;
        });

        Assert.That(sent, Is.EqualTo(new[] { "retry@example.com" }));
    }

    [Test]
    public async Task SendDue_PausedLatestAuthBlocksOlderActiveAuth()
    {
        await using var hold = new DbHold();
        var db = hold.Db;
        var account = Account("login@example.com", 1);
        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        var customer = Customer(account.Id, "invoice@example.com");
        var product = Product();
        db.Customers.Add(customer);
        db.Products.Add(product);
        await db.SaveChangesAsync();
        db.SubscriptionAuths.Add(Auth(customer.Id, product.Id, End, 1));
        db.SubscriptionAuths.Add(Auth(customer.Id, product.Id, End.AddDays(1), 3));
        await db.SaveChangesAsync();
        var sent = new List<string>();

        await new ExpiryNoticeService().SendDueAsync(db, ReadyMail(), Now, (to, _, _) =>
        {
            sent.Add(to);
            return Task.CompletedTask;
        });

        Assert.That(sent, Is.Empty);
    }

    static Func<string, string, string, Task> Capture(List<(string To, string Subject, string Body)> sent)
    {
        return (to, subject, body) =>
        {
            sent.Add((to, subject, body));
            return Task.CompletedTask;
        };
    }

    static MailSettings ReadyMail() => new("smtp.example", 587, "vendor", "secret", "vendor@example.com", true);

    static async Task<long> Seed(WebDb db, DateTime end, int accountStatus, int authStatus, string email = "login@example.com", string? invoiceEmail = "invoice@example.com")
    {
        var account = Account(email, accountStatus);
        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        var customer = Customer(account.Id, invoiceEmail);
        var product = Product();
        db.Customers.Add(customer);
        db.Products.Add(product);
        await db.SaveChangesAsync();
        db.SubscriptionAuths.Add(Auth(customer.Id, product.Id, end, authStatus));
        await db.SaveChangesAsync();
        return customer.Id;
    }

    static Account Account(string email, int status) => new()
    {
        Email = email,
        PasswordHash = "x",
        Status = status,
        CreatedAt = Now,
        UpdatedAt = Now
    };

    static Customer Customer(long accountId, string? invoiceEmail) => new()
    {
        AccountId = accountId,
        CustomerNo = "C" + Guid.NewGuid().ToString("N"),
        InvoiceEmail = invoiceEmail,
        Status = 1,
        CreatedAt = Now,
        UpdatedAt = Now
    };

    static Product Product() => new()
    {
        ProductNo = "P" + Guid.NewGuid().ToString("N"),
        Name = "标准版",
        Price = 98,
        PeriodDays = 365,
        Status = 1,
        CreatedAt = Now,
        UpdatedAt = Now
    };

    static SubscriptionAuth Auth(long customerId, long productId, DateTime end, int status) => new()
    {
        CustomerId = customerId,
        ProductId = productId,
        AuthStartTime = end.AddDays(-365),
        AuthEndTime = end,
        Status = status,
        GrantType = 1,
        CreatedAt = Now,
        UpdatedAt = Now
    };

    sealed class DbHold : IAsyncDisposable
    {
        readonly SqliteConnection _conn;
        public WebDb Db { get; }

        public DbHold()
        {
            _conn = new SqliteConnection("Data Source=:memory:");
            _conn.Open();
            Db = new WebDb(new DbContextOptionsBuilder<WebDb>().UseSqlite(_conn).Options);
            Db.Database.EnsureCreated();
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _conn.DisposeAsync();
        }
    }
}
