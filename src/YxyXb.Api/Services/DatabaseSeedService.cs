using Microsoft.EntityFrameworkCore;
using YxyXb.Api.Data;

namespace YxyXb.Api.Services;

public sealed class DatabaseSeedService
{
    private readonly WebDb _db;
    private readonly TimeProvider _time;

    public DatabaseSeedService(WebDb db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    public async Task EnsureCatalogAsync()
    {
        var now = _time.GetUtcNow().UtcDateTime;
        await EnsureTierAsync("PERSONAL", "个人版", "最多 3 台设备", 36m, 3, 1, now);
        await EnsureTierAsync("STANDARD", "标准版", "最多 10 台设备", 98m, 10, 2, now);
        await EnsureTierAsync("PRO", "专业版", "最多 50 台设备", 198m, 50, 3, now);
        await EnsureTierAsync("CUSTOM", "定制版", "人数由后台填写", 0m, 1, 4, now);
        await PutConfigAsync("trial_price_1", "0.10");
        await PutConfigAsync("trial_price_2", "0.50");
        await _db.SaveChangesAsync();
    }

    public async Task EnsureAdminAsync(
        string? username,
        string? password,
        string? email,
        bool resetPassword,
        IPasswordHasher hasher)
    {
        if (hasher == null) throw new ArgumentNullException(nameof(hasher));
        var name = string.IsNullOrWhiteSpace(username) ? "admin" : username.Trim();
        var mail = AccountService.Norm(email);
        if (!await _db.AdminUsers.AnyAsync())
        {
            _db.AdminUsers.Add(new AdminUser
            {
                Username = name,
                PasswordHash = hasher.Hash(password ?? ""),
                Email = string.IsNullOrEmpty(mail) ? null : mail,
                CreatedAt = _time.GetUtcNow().UtcDateTime
            });
            await _db.SaveChangesAsync();
            return;
        }

        var admin = await _db.AdminUsers.SingleOrDefaultAsync(x => x.Username == name);
        if (admin == null) return;
        if (resetPassword)
        {
            admin.PasswordHash = hasher.Hash(password ?? "");
            if (!string.IsNullOrEmpty(mail)) admin.Email = mail;
        }
        else if (!string.IsNullOrEmpty(mail) && string.IsNullOrEmpty(admin.Email))
        {
            admin.Email = mail;
        }

        await _db.SaveChangesAsync();
    }

    private async Task EnsureTierAsync(
        string productNo,
        string name,
        string description,
        decimal price,
        int maxDevices,
        int sortOrder,
        DateTime now)
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
            PeriodDays = OrderService.FormalDays,
            Status = 1,
            CreatedAt = now,
            UpdatedAt = now
        });
    }

    private async Task PutConfigAsync(string key, string value)
    {
        if (await _db.Configs.AnyAsync(x => x.CfgKey == key)) return;
        _db.Configs.Add(new SystemConfigRow { CfgKey = key, CfgValue = value });
    }
}
