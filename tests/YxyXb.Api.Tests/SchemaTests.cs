using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using YxyXb.Api.Data;

namespace YxyXb.Api.Tests;

public sealed class SchemaTests
{
    [Test]
    public async Task Email_Is_Unique()
    {
        await using var factory = new WebAppFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WebDb>();
        db.Accounts.Add(NewAccount("a@b.com"));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        db.Accounts.Add(NewAccount("a@b.com"));
        Assert.ThrowsAsync<DbUpdateException>(async () => await db.SaveChangesAsync());
    }

    static Account NewAccount(string email) => new()
    {
        Email = email,
        PasswordHash = "x",
        Status = 1,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
}
