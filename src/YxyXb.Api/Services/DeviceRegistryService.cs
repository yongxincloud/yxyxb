using Microsoft.EntityFrameworkCore;
using YxyXb.Api.Data;

namespace YxyXb.Api.Services;

public sealed record DeviceOccupyResult(bool Ok, string? ErrorCode, string? Error, int DeviceCount, int? MaxDevices);

public sealed record DeviceRow(string DeviceId, string? DisplayName, string? Note, DateTime FirstSeenAt, DateTime LastSeenAt);

public sealed class DeviceRegistryService
{
    private readonly WebDb _db;
    private readonly TimeProvider _time;

    public DeviceRegistryService(WebDb db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    public async Task<DeviceOccupyResult> TryOccupyAsync(
        long accountId,
        string? deviceId,
        int? maxDevices,
        bool enforceLimit,
        string? displayName = null,
        string? note = null)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            return new DeviceOccupyResult(false, "DEVICE_ID_REQUIRED", "缺少设备标识", 0, maxDevices);

        var id = deviceId.Trim();
        var now = _time.GetUtcNow().UtcDateTime;
        var active = await _db.AccountDevices
            .Where(x => x.AccountId == accountId && x.Status == 1)
            .ToListAsync();
        var existing = await _db.AccountDevices
            .SingleOrDefaultAsync(x => x.AccountId == accountId && x.DeviceId == id);

        if (existing != null && existing.Status == 1)
        {
            existing.LastSeenAt = now;
            if (!string.IsNullOrWhiteSpace(displayName)) existing.DisplayName = displayName.Trim();
            ApplyNote(existing, note);
            await _db.SaveChangesAsync();
            return new DeviceOccupyResult(true, null, null, active.Count, maxDevices);
        }

        if (enforceLimit && maxDevices is int cap && active.Count >= cap)
            return new DeviceOccupyResult(false, "DEVICE_LIMIT", $"已达 {cap} 台设备上限，请踢出设备或升级", active.Count, cap);

        if (existing != null)
        {
            existing.Status = 1;
            existing.KickedAt = null;
            existing.LastSeenAt = now;
            existing.FirstSeenAt = now;
            if (!string.IsNullOrWhiteSpace(displayName)) existing.DisplayName = displayName.Trim();
            ApplyNote(existing, note);
        }
        else
        {
            _db.AccountDevices.Add(new AccountDevice
            {
                AccountId = accountId,
                DeviceId = id,
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim(),
                Note = NormalizeNote(note),
                FirstSeenAt = now,
                LastSeenAt = now,
                Status = 1
            });
        }

        await _db.SaveChangesAsync();
        var count = await _db.AccountDevices.CountAsync(x => x.AccountId == accountId && x.Status == 1);
        return new DeviceOccupyResult(true, null, null, count, maxDevices);
    }

    public async Task<IReadOnlyList<DeviceRow>> ListActiveAsync(long accountId)
    {
        return await _db.AccountDevices
            .Where(x => x.AccountId == accountId && x.Status == 1)
            .OrderByDescending(x => x.LastSeenAt)
            .Select(x => new DeviceRow(x.DeviceId, x.DisplayName, x.Note, x.FirstSeenAt, x.LastSeenAt))
            .ToListAsync();
    }

    public async Task<bool> SetNoteAsync(long accountId, string deviceId, string? note)
    {
        if (string.IsNullOrWhiteSpace(deviceId)) return false;
        var id = deviceId.Trim();
        var row = await _db.AccountDevices.SingleOrDefaultAsync(x => x.AccountId == accountId && x.DeviceId == id && x.Status == 1);
        if (row == null) return false;
        row.Note = NormalizeNote(note);
        await _db.SaveChangesAsync();
        return true;
    }

    static void ApplyNote(AccountDevice row, string? note)
    {
        var text = NormalizeNote(note);
        if (text != null) row.Note = text;
    }

    static string? NormalizeNote(string? note)
    {
        if (string.IsNullOrWhiteSpace(note)) return null;
        var text = note.Trim().Replace("{", "").Replace("}", "");
        if (text.Length == 0) return null;
        return text.Length <= 40 ? text : text.Substring(0, 40);
    }

    public async Task<bool> KickAsync(long accountId, string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId)) return false;
        var id = deviceId.Trim();
        var row = await _db.AccountDevices.SingleOrDefaultAsync(x => x.AccountId == accountId && x.DeviceId == id && x.Status == 1);
        if (row == null) return false;
        var now = _time.GetUtcNow().UtcDateTime;
        row.Status = 0;
        row.KickedAt = now;
        var tokens = await _db.AuthTokens
            .Where(x => x.AccountId == accountId && x.DeviceId == id && x.Revoked == 0)
            .ToListAsync();
        foreach (var token in tokens)
        {
            token.Revoked = 1;
            token.RevokedAt = now;
        }
        await _db.SaveChangesAsync();
        return true;
    }
}
