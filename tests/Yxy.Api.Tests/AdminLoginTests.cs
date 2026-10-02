using System.Net;
using System.Net.Http.Json;
using NUnit.Framework;
using Yxy.Api.Controllers;

namespace Yxy.Api.Tests;

public sealed class AdminLoginTests
{
    [Test]
    public async Task Admin_Login_Sees_Every_Account()
    {
        await using var factory = new WebAppFactory();
        var client = factory.CreateClient();
        var bad = await AccountApi.Post(client, "/api/vendor/login", new { username = "admin", password = "wrong-pass" });
        Assert.That(bad.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var anon = await client.GetAsync("/api/vendor/board");
        Assert.That(anon.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        await AccountApi.SeedAccount(factory, "seen@example.com", "abc12345");
        var ok = await AccountApi.Post(client, "/api/vendor/login", new
        {
            username = VendorController.DefaultAdminUser,
            password = VendorController.DefaultAdminPassword
        });
        Assert.That(ok.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var board = await client.GetFromJsonAsync<BoardDto>("/api/vendor/board");
        Assert.That(board!.Ok, Is.True);
        Assert.That(board.Accounts.Any(x => x.Email == "seen@example.com"), Is.True);
        Assert.That(board.Customers.Any(x => x.Email == "seen@example.com"), Is.True);
    }

    sealed record BoardDto(bool Ok, AccountRow[] Accounts, CustomerRow[] Customers);
    sealed record AccountRow(string Email);
    sealed record CustomerRow(string Email);
}
