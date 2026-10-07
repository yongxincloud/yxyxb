using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Yxy.Api.Data;

namespace Yxy.Api.Services;

public sealed record ClientSession(
    bool Ok,
    string? Error,
    bool Valid,
    string? Token,
    int? AccountStatus,
    string? AuthStatus,
    DateTime? AuthEndTime,
    int? NextCheckDays,
    string? Name = null,
    string? Nickname = null,
    string? CompanyName = null,
    string? ErrorCode = null,
    string? ProductNo = null,
    int? MaxDevices = null,
    int? DeviceCount = null);

public sealed class ClientAuthService
{
    public const int GraceDays = 7;
    public const int TokenBufferDays = 14;

    private readonly WebDb _db;
    private readonly AccountService _accounts;
    private readonly DeviceRegistryService _devices;
    private readonly TimeProvider _time;

    public ClientAuthService(WebDb db, AccountService accounts, DeviceRegistryService devices, TimeProvider time)
    {
        _db = db;
        _accounts = accounts;
        _devices = devices;
        _time = time;
    }

    public async Task<ClientSession> LoginPasswordAsync(string? email, string? password, string? deviceId, string? ip, string? displayName = null)
    {
        var auth = await _accounts.LoginPasswordAsync(email, password, ip);
        if (!auth.Ok || auth.AccountId == null) return Fail(auth.Error);
        return await IssueAsync(auth.AccountId.Value, deviceId, displayName);
    }

    public async Task<ClientSession> LoginCodeAsync(string? email, string? code, string? deviceId, string? ip, string? displayName = null)
    {
        var auth = await _accounts.LoginCodeAsync(email, code, ip);
        if (!auth.Ok || auth.AccountId == null) return Fail(auth.Error);
        return await IssueAsync(auth.AccountId.Value, deviceId, displayName);
    }

    public Task<ClientSession> VerifyAsync(string? token, string? deviceId, string? displayName = null) =>
        RefreshAsync(token, deviceId, displayName);

    public Task<ClientSession> RenewAsync(string? token) => RefreshAsync(token, null, null);

    public async Task<long?> ResolveAccountIdAsync(string? rawToken)
    {
        var row = await FindLiveAsync(rawToken);
        return row?.AccountId;
    }

    public async Task<(string Status, DateTime? End, int NextDays, bool Usable)> DescribeAsync(long accountId)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var view = await LoadAuthAsync(accountId, now);
        return (view.Status, view.End, NextCheckDays(view.End, now), view.Usable);
    }

    public static int NextCheckDays(DateTime? authEnd, DateTime now)
    {
        if (authEnd == null) return GraceDays;
        return (authEnd.Value - now).TotalDays > 30 ? 30 : GraceDays;
    }

    async Task<ClientSession> RefreshAsync(string? rawToken, string? deviceId, string? displayName)
    {
        var row = await FindLiveAsync(rawToken);
        if (row == null) return Fail("登录已失效");
        var account = await _db.Accounts.SingleOrDefaultAsync(x => x.Id == row.AccountId);
        if (account == null || account.Status != 1)
        {
            await RevokeAccountAsync(row.AccountId);
            return Fail("登录已失效");
        }
        return await IssueAsync(account.Id, deviceId ?? row.DeviceId, displayName);
    }

    async Task<ClientSession> IssueAsync(long accountId, string? deviceId, string? displayName = null)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            return Fail("缺少设备标识", "DEVICE_ID_REQUIRED");

        var account = await _db.Accounts.SingleAsync(x => x.Id == accountId);
        var customer = await _db.Customers.SingleOrDefaultAsync(x => x.AccountId == accountId);
        var now = _time.GetUtcNow().UtcDateTime;
        var view = await LoadAuthAsync(accountId, now);

        string? productNo = null;
        int? maxDevices = null;
        var enforceLimit = false;
        if (view.Usable && view.AuthRow != null)
        {
            var product = await _db.Products.SingleOrDefaultAsync(x => x.Id == view.AuthRow.ProductId);
            productNo = product?.ProductNo;
            maxDevices = product?.MaxDevices;
            var trial = false;
            if (view.AuthRow.OrderId is long orderId)
            {
                var order = await _db.Orders.SingleOrDefaultAsync(x => x.Id == orderId);
                trial = order?.OrderType == 2;
            }
            enforceLimit = !trial && maxDevices != null;
        }

        var occupy = await _devices.TryOccupyAsync(accountId, deviceId, maxDevices, enforceLimit, NormalizeDisplayName(displayName));
        if (!occupy.Ok)
            return Fail(occupy.Error, occupy.ErrorCode, productNo, occupy.MaxDevices, occupy.DeviceCount);

        var days = NextCheckDays(view.End, now);
        var raw = MakeToken();
        _db.AuthTokens.Add(new AuthToken
        {
            AccountId = accountId,
            TokenHash = Hash(raw),
            DeviceId = deviceId.Trim(),
            IssuedAt = now,
            ExpiresAt = now.AddDays(days + TokenBufferDays),
            Revoked = 0
        });
        await _db.SaveChangesAsync();
        return new ClientSession(
            true, null, view.Usable, raw, account.Status, view.Status, view.End, days,
            customer?.Name, customer?.Nickname, customer?.CompanyName,
            null, productNo, maxDevices, occupy.DeviceCount);
    }

    async Task<AuthView> LoadAuthAsync(long accountId, DateTime now)
    {
        var customer = await _db.Customers.SingleOrDefaultAsync(x => x.AccountId == accountId);
        if (customer == null) return AuthView.None;
        var row = await _db.SubscriptionAuths
            .Where(x => x.CustomerId == customer.Id && x.Status != 0)
            .OrderByDescending(x => x.AuthEndTime)
            .FirstOrDefaultAsync();
        if (row == null)
        {
            var cancelled = await _db.SubscriptionAuths.AnyAsync(x => x.CustomerId == customer.Id && x.Status == 0);
            return cancelled ? new AuthView(false, "cancelled", null, null) : AuthView.None;
        }
        if (row.Status == 3) return new AuthView(false, "paused", row.AuthEndTime, row);
        if (row.Status == 0) return new AuthView(false, "cancelled", row.AuthEndTime, row);
        if (row.Status == 2) return new AuthView(false, "expired", row.AuthEndTime, row);
        if (now <= row.AuthEndTime) return new AuthView(true, "active", row.AuthEndTime, row);
        if (now <= row.AuthEndTime.AddDays(GraceDays)) return new AuthView(true, "grace", row.AuthEndTime, row);
        return new AuthView(false, "expired", row.AuthEndTime, row);
    }

    async Task<AuthToken?> FindLiveAsync(string? rawToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken)) return null;
        var hash = Hash(rawToken);
        var now = _time.GetUtcNow().UtcDateTime;
        return await _db.AuthTokens.SingleOrDefaultAsync(x =>
            x.TokenHash == hash && x.Revoked == 0 && x.ExpiresAt >= now);
    }

    async Task RevokeAccountAsync(long accountId)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var rows = _db.AuthTokens.Where(x => x.AccountId == accountId && x.Revoked == 0).ToList();
        foreach (var row in rows)
        {
            row.Revoked = 1;
            row.RevokedAt = now;
        }
        await _db.SaveChangesAsync();
    }

    static string MakeToken()
    {
        var text = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        return text.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static string Hash(string raw)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    static string? NormalizeDisplayName(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName)) return null;
        var text = displayName.Trim();
        return text.Length <= 200 ? text : text.Substring(0, 200);
    }

    static ClientSession Fail(
        string? error,
        string? errorCode = null,
        string? productNo = null,
        int? maxDevices = null,
        int? deviceCount = null) =>
        new(false, error, false, null, null, null, null, null, null, null, null, errorCode, productNo, maxDevices, deviceCount);

    sealed record AuthView(bool Usable, string Status, DateTime? End, SubscriptionAuth? AuthRow)
    {
        public static readonly AuthView None = new(false, "none", null, null);
    }
}
