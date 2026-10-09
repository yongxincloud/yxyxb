using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using YxyXb.Api.Controllers;
using YxyXb.Api.Data;

namespace YxyXb.Api.Tests;

public sealed class VendorCustomerListTests
{
    [Test]
    public async Task Default_List_Returns_Latest_50()
    {
        await using var factory = new WebAppFactory();
        for (var i = 0; i < 51; i++)
            await Seed(factory, "list" + i.ToString("00") + "@example.com");

        var body = await Get(factory, "/api/vendor/customers");
        Assert.That(body!.Rows, Has.Length.EqualTo(50));
        Assert.That(body.Rows.Any(x => x.Email == "list00@example.com"), Is.False);
        Assert.That(body.Rows[0].Email, Is.EqualTo("list50@example.com"));
    }

    [Test]
    public async Task Search_Matches_Profile_And_Account_Fields()
    {
        await using var factory = new WebAppFactory();
        await Seed(factory, "alice@example.com", (account, customer) =>
        {
            account.Phone = "13800001111";
            customer.Name = "ZhangSan";
            customer.Nickname = "Ali";
            customer.CompanyName = "Acme";
            customer.CustomerNo = "NO-ALICE";
            customer.InvoiceTitle = "抬头甲";
            customer.InvoiceTaxNo = "TAX-1";
            customer.InvoiceEmail = "finance@example.com";
            customer.InvoiceLicense = "license.png";
        });
        await Seed(factory, "bob@example.com", (account, customer) =>
        {
            customer.Name = "LiSi";
            customer.CustomerNo = "NO-BOB";
        });

        var byName = await Get(factory, "/api/vendor/customers?q=zhangsan");
        Assert.That(byName!.Rows.Select(x => x.Email), Is.EqualTo(new[] { "alice@example.com" }));
        Assert.That(byName.Rows[0].Name, Is.EqualTo("ZhangSan"));
        Assert.That(byName.Rows[0].InvoiceLicense, Is.EqualTo("license.png"));

        foreach (var q in new[] { "ali", "acme", "NO-ALICE", "抬头甲", "tax-1", "finance@example.com", "alice@example.com", "13800001111" })
        {
            var hit = await Get(factory, "/api/vendor/customers?q=" + Uri.EscapeDataString(q));
            Assert.That(hit!.Rows.Select(x => x.Email), Is.EqualTo(new[] { "alice@example.com" }), q);
        }

        var miss = await Get(factory, "/api/vendor/customers?q=nobody");
        Assert.That(miss!.Ok, Is.True);
        Assert.That(miss.Rows, Is.Empty);
    }

    [Test]
    public async Task Blank_Query_Matches_Default_List()
    {
        await using var factory = new WebAppFactory();
        await Seed(factory, "one@example.com");
        await Seed(factory, "two@example.com");
        var plain = await Get(factory, "/api/vendor/customers");
        var blank = await Get(factory, "/api/vendor/customers?q=%20%20");
        Assert.That(blank!.Rows.Select(x => x.Email), Is.EqualTo(plain!.Rows.Select(x => x.Email)));
    }

    [Test]
    public async Task Search_Returns_At_Most_100()
    {
        await using var factory = new WebAppFactory();
        for (var i = 0; i < 101; i++)
            await Seed(factory, "same" + i.ToString("000") + "@example.com", (_, customer) => customer.Name = "同名");

        var body = await Get(factory, "/api/vendor/customers?q=" + Uri.EscapeDataString("同名"));
        Assert.That(body!.Rows, Has.Length.EqualTo(100));
        Assert.That(body.Rows.Any(x => x.Email == "same000@example.com"), Is.False);
        Assert.That(body.Rows[0].Email, Is.EqualTo("same100@example.com"));
    }

    [Test]
    public async Task List_Uses_Latest_Auth_Product()
    {
        await using var factory = new WebAppFactory();
        await Seed(factory, "auth@example.com");
        DateTime end;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WebDb>();
            var customer = await db.Customers.SingleAsync();
            var older = await db.Products.SingleAsync(x => x.ProductNo == "PERSONAL");
            var newer = await db.Products.SingleAsync(x => x.ProductNo == "PRO");
            var now = DateTime.UtcNow;
            end = now.AddDays(30);
            db.SubscriptionAuths.Add(new SubscriptionAuth
            {
                CustomerId = customer.Id,
                ProductId = older.Id,
                AuthStartTime = now.AddDays(-10),
                AuthEndTime = now.AddDays(10),
                Status = 1,
                CreatedAt = now,
                UpdatedAt = now
            });
            db.SubscriptionAuths.Add(new SubscriptionAuth
            {
                CustomerId = customer.Id,
                ProductId = newer.Id,
                AuthStartTime = now.AddDays(-1),
                AuthEndTime = end,
                Status = 1,
                CreatedAt = now,
                UpdatedAt = now
            });
            await db.SaveChangesAsync();
        }

        var body = await Get(factory, "/api/vendor/customers");
        Assert.That(body!.Rows[0].ProductNo, Is.EqualTo("PRO"));
        Assert.That(body.Rows[0].AuthEndTime, Is.EqualTo(end).Within(TimeSpan.FromSeconds(1)));
    }

    [Test]
    public async Task Customers_Rejects_Non_Vendor()
    {
        await using var factory = new WebAppFactory();
        var response = await factory.CreateClient().GetAsync("/api/vendor/customers");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    static async Task<CustomerListDto?> Get(WebAppFactory factory, string path)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Vendor-Key", VendorController.VendorKey);
        return await client.GetFromJsonAsync<CustomerListDto>(path);
    }

    static async Task Seed(WebAppFactory factory, string email, Action<Account, Customer>? apply = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WebDb>();
        var now = DateTime.UtcNow;
        var account = new Account
        {
            Email = email,
            PasswordHash = "x",
            Status = 1,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        var customer = new Customer
        {
            CustomerNo = "C" + account.Id.ToString("D6"),
            AccountId = account.Id,
            Status = 1,
            CreatedAt = now,
            UpdatedAt = now
        };
        apply?.Invoke(account, customer);
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
    }

    sealed record CustomerListDto(bool Ok, CustomerRowDto[] Rows);
    sealed record CustomerRowDto(string? Email, string? Name, string? InvoiceLicense, string? ProductNo, DateTime? AuthEndTime);
}
