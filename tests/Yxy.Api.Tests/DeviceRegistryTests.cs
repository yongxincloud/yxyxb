using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Yxy.Api.Data;
using Yxy.Api.Services;

namespace Yxy.Api.Tests;

public sealed class DeviceRegistryTests
{
    [Test]
    public async Task Occupy_Rejects_When_Full_And_Allows_After_Kick()
    {
        await using var factory = new WebAppFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WebDb>();
        var devices = scope.ServiceProvider.GetRequiredService<DeviceRegistryService>();
        var account = new Account
        {
            Email = "dev@example.com",
            PasswordHash = "x",
            Status = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Accounts.Add(account);
        await db.SaveChangesAsync();

        Assert.That((await devices.TryOccupyAsync(account.Id, "a", 2, true)).Ok, Is.True);
        Assert.That((await devices.TryOccupyAsync(account.Id, "b", 2, true)).Ok, Is.True);
        var full = await devices.TryOccupyAsync(account.Id, "c", 2, true);
        Assert.That(full.Ok, Is.False);
        Assert.That(full.ErrorCode, Is.EqualTo("DEVICE_LIMIT"));

        Assert.That(await devices.KickAsync(account.Id, "a"), Is.True);
        Assert.That((await devices.TryOccupyAsync(account.Id, "c", 2, true)).Ok, Is.True);
        Assert.That((await devices.ListActiveAsync(account.Id)).Count, Is.EqualTo(2));
    }

    [Test]
    public async Task Same_Device_Reoccupy_Does_Not_Increase_Count()
    {
        await using var factory = new WebAppFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WebDb>();
        var devices = scope.ServiceProvider.GetRequiredService<DeviceRegistryService>();
        var account = new Account
        {
            Email = "same@example.com",
            PasswordHash = "x",
            Status = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Accounts.Add(account);
        await db.SaveChangesAsync();

        var first = await devices.TryOccupyAsync(account.Id, "pc-1", 3, true);
        var again = await devices.TryOccupyAsync(account.Id, "pc-1", 3, true);
        Assert.That(first.DeviceCount, Is.EqualTo(1));
        Assert.That(again.DeviceCount, Is.EqualTo(1));
        Assert.That(await db.AccountDevices.CountAsync(x => x.AccountId == account.Id && x.Status == 1), Is.EqualTo(1));
    }

    [Test]
    public async Task Kick_Revokes_Device_Tokens()
    {
        await using var factory = new WebAppFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WebDb>();
        var devices = scope.ServiceProvider.GetRequiredService<DeviceRegistryService>();
        var account = new Account
        {
            Email = "kick@example.com",
            PasswordHash = "x",
            Status = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        await devices.TryOccupyAsync(account.Id, "pc-1", 3, true);
        db.AuthTokens.Add(new AuthToken
        {
            AccountId = account.Id,
            TokenHash = "hash1",
            DeviceId = "pc-1",
            IssuedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            Revoked = 0
        });
        await db.SaveChangesAsync();
        Assert.That(await devices.KickAsync(account.Id, "pc-1"), Is.True);
        Assert.That((await db.AuthTokens.SingleAsync()).Revoked, Is.EqualTo(1));
    }
}
