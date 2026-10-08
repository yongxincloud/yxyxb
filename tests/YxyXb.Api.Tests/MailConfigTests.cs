using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using YxyXb.Api.Controllers;
using YxyXb.Api.Data;
using YxyXb.Api.Services;

namespace YxyXb.Api.Tests;

public sealed class MailConfigTests
{
    [Test]
    public async Task Vendor_Saves_Mail_Server_Without_Returning_Password()
    {
        await using var factory = new WebAppFactory();
        var vendor = factory.CreateClient();
        vendor.DefaultRequestHeaders.Add("X-Vendor-Key", VendorController.VendorKey);
        var saved = await AccountApi.Post(vendor, "/api/vendor/mail", new
        {
            host = "smtp.example.com",
            port = 465,
            user = "mailer",
            password = "secret-mail",
            from = "noreply@example.com",
            ssl = true
        });
        Assert.That(saved.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var body = await vendor.GetStringAsync("/api/vendor/mail");
        Assert.That(body, Does.Contain("smtp.example.com"));
        Assert.That(body, Does.Not.Contain("secret-mail"));
        Assert.That(body, Does.Contain("\"configured\":true"));

        var again = await AccountApi.Post(vendor, "/api/vendor/mail", new
        {
            host = "smtp.example.com",
            port = 465,
            user = "mailer",
            password = "",
            from = "noreply@example.com",
            ssl = true
        });
        Assert.That(again.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WebDb>();
        var password = await db.Configs.SingleAsync(x => x.CfgKey == MailSettingsService.PasswordKey);
        Assert.That(password.CfgValue, Is.EqualTo("secret-mail"));
    }
}
