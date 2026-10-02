using Yxy.Api.Data;

namespace Yxy.Api.Services;

public sealed record IssueCodeResult(bool Ok, string? Code, string? Error);

public sealed class EmailCodeService
{
    public const int LifetimeMinutes = 5;
    public const int MinIntervalSeconds = 60;
    public const int DailyCap = 10;
    public const int IpHourlyCap = 30;

    private readonly WebDb _db;
    private readonly TimeProvider _time;
    public EmailCodeService(WebDb db, TimeProvider time) { _db = db; _time = time; }

    public async Task<IssueCodeResult> IssueAsync(string email, string scene, string? ip)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var last = _db.EmailCodes.Where(x => x.Email == email).OrderByDescending(x => x.CreatedAt).FirstOrDefault();
        if (last != null && (now - last.CreatedAt).TotalSeconds < MinIntervalSeconds)
            return new IssueCodeResult(false, null, "too_soon");
        var dayStart = now.Date;
        var sentToday = _db.EmailCodes.Count(x => x.Email == email && x.CreatedAt >= dayStart);
        if (sentToday >= DailyCap) return new IssueCodeResult(false, null, "daily_cap");
        if (!string.IsNullOrEmpty(ip))
        {
            var hourAgo = now.AddHours(-1);
            var ipCount = _db.EmailCodes.Count(x => x.ReqIp == ip && x.CreatedAt >= hourAgo);
            if (ipCount >= IpHourlyCap) return new IssueCodeResult(false, null, "ip_cap");
        }
        var code = Random.Shared.Next(0, 1000000).ToString("D6");
        _db.EmailCodes.Add(new EmailCodeRow
        {
            Email = email, Code = code, Scene = scene, Used = 0,
            ExpiresAt = now.AddMinutes(LifetimeMinutes), ReqIp = ip, CreatedAt = now
        });
        await _db.SaveChangesAsync();
        return new IssueCodeResult(true, code, null);
    }

    public async Task<bool> ConsumeAsync(string email, string scene, string code)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var row = _db.EmailCodes
            .Where(x => x.Email == email && x.Scene == scene && x.Code == code && x.Used == 0 && x.ExpiresAt >= now)
            .OrderByDescending(x => x.Id).FirstOrDefault();
        if (row == null) return false;
        row.Used = 1;
        await _db.SaveChangesAsync();
        return true;
    }
}
