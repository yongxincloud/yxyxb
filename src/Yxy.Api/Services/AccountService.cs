using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using Yxy.Api.Data;

namespace Yxy.Api.Services;

public sealed record RegisterInput(string? Email, string? Password, string? Confirm, string? EmailCode, string? ProviderNo);
public sealed record AuthResult(bool Ok, string? Error, long? AccountId = null, string? Email = null);

public sealed class AccountService
{
    private readonly WebDb _db;
    private readonly IPasswordHasher _hasher;
    private readonly EmailCodeService _codes;
    private readonly CaptchaService _captcha;
    private readonly IEmailSender _mail;
    private readonly TimeProvider _time;

    public AccountService(WebDb db, IPasswordHasher hasher, EmailCodeService codes, CaptchaService captcha, IEmailSender mail, TimeProvider time)
    {
        _db = db; _hasher = hasher; _codes = codes; _captcha = captcha; _mail = mail; _time = time;
    }

    public async Task<AuthResult> SendCodeAsync(string? emailRaw, string? scene, string? captchaId, string? captchaAnswer, string? ip)
    {
        var email = Norm(emailRaw);
        if (scene is not ("register" or "login" or "reset")) return Fail("场景不正确");
        if (scene == "register" && !_captcha.VerifyAndConsume(captchaId, captchaAnswer))
            return Fail("图形验证码错误");
        var exists = await _db.Accounts.AnyAsync(x => x.Email == email);
        if (scene == "register" && exists) return new AuthResult(true, null);
        if (scene is "login" or "reset" && !exists) return new AuthResult(true, null);
        var issued = await _codes.IssueAsync(email, scene, ip);
        if (!issued.Ok)
            return Fail(issued.Error == "too_soon" ? "发送过于频繁" : "今日发送次数已达上限");
        await _mail.SendAsync(email, "用心云备份验证码", "验证码 " + issued.Code + " ，5 分钟内有效。");
        return new AuthResult(true, null);
    }

    public async Task<AuthResult> RegisterAsync(RegisterInput input, string? ip)
    {
        var email = Norm(input.Email);
        if (!IsEmail(email)) return Fail("邮箱格式不正确");
        if (input.Password != input.Confirm) return Fail("两次密码不一致");
        if (!PasswordRules.IsStrong(input.Password)) return Fail("密码至少 8 位且同时包含字母和数字");
        if (await IpRegisterCapped(ip)) return Fail("注册过于频繁");
        if (await _db.Accounts.AnyAsync(x => x.Email == email)) return Fail("该邮箱已注册");
        if (!await _codes.ConsumeAsync(email, "register", input.EmailCode ?? ""))
            return Fail("邮箱或验证码错误");
        var now = _time.GetUtcNow().UtcDateTime;
        var account = new Account
        {
            Email = email,
            PasswordHash = _hasher.Hash(input.Password!),
            Status = 1,
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.Accounts.Add(account);
        await _db.SaveChangesAsync();
        long? providerId = null;
        if (!string.IsNullOrWhiteSpace(input.ProviderNo))
        {
            var provider = await _db.Providers.SingleOrDefaultAsync(x => x.ProviderNo == input.ProviderNo && x.Status == 1);
            providerId = provider?.Id;
        }
        _db.Customers.Add(new Customer
        {
            CustomerNo = "C" + now.ToString("yyyyMMddHHmmssfff"),
            AccountId = account.Id,
            DirectProviderId = providerId,
            Status = 1,
            CreatedAt = now,
            UpdatedAt = now
        });
        await _db.SaveChangesAsync();
        return new AuthResult(true, null, account.Id, account.Email);
    }

    public async Task<AuthResult> LoginPasswordAsync(string? emailRaw, string? password, string? ip)
    {
        var email = Norm(emailRaw);
        if (await IsLocked(email, "password"))
            return Fail("登录已锁定，请 15 分钟后再试");
        var account = await _db.Accounts.SingleOrDefaultAsync(x => x.Email == email);
        if (account == null || account.Status != 1 || !_hasher.Verify(password ?? "", account.PasswordHash))
        {
            await WriteLog(account?.Id, email, "password", ip, 0);
            return Fail("邮箱或密码错误");
        }
        await WriteLog(account.Id, email, "password", ip, 1);
        var now = _time.GetUtcNow().UtcDateTime;
        account.LastLoginAt = now;
        account.UpdatedAt = now;
        await _db.SaveChangesAsync();
        return new AuthResult(true, null, account.Id, account.Email);
    }

    public async Task<AuthResult> LoginCodeAsync(string? emailRaw, string? code, string? ip)
    {
        var email = Norm(emailRaw);
        if (!await _codes.ConsumeAsync(email, "login", code ?? ""))
            return Fail("邮箱或验证码错误");
        var account = await _db.Accounts.SingleOrDefaultAsync(x => x.Email == email);
        if (account == null || account.Status != 1)
            return Fail("邮箱或验证码错误");
        await WriteLog(account.Id, email, "code", ip, 1);
        var now = _time.GetUtcNow().UtcDateTime;
        account.LastLoginAt = now;
        account.UpdatedAt = now;
        await _db.SaveChangesAsync();
        return new AuthResult(true, null, account.Id, account.Email);
    }

    async Task<bool> IsLocked(string email, string way)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var recent = await _db.LoginLogs.Where(x => x.Email == email && x.Way == way)
            .OrderByDescending(x => x.Id).Take(5).ToListAsync();
        if (recent.Count < 5 || recent.Any(x => x.Success == 1)) return false;
        return recent[0].CreatedAt.AddMinutes(15) > now;
    }

    async Task WriteLog(long? accountId, string email, string way, string? ip, int success)
    {
        _db.LoginLogs.Add(new AccountLoginLog
        {
            AccountId = accountId,
            Email = email,
            Way = way,
            Ip = ip,
            Success = success,
            CreatedAt = _time.GetUtcNow().UtcDateTime
        });
        await _db.SaveChangesAsync();
    }

    async Task<bool> IpRegisterCapped(string? ip)
    {
        if (string.IsNullOrEmpty(ip)) return false;
        var hourAgo = _time.GetUtcNow().UtcDateTime.AddHours(-1);
        var count = await _db.EmailCodes.CountAsync(x => x.Scene == "register" && x.ReqIp == ip && x.Used == 1 && x.CreatedAt >= hourAgo);
        return count >= 10;
    }

    internal static string Norm(string? email) => (email ?? "").Trim().ToLowerInvariant();
    static bool IsEmail(string email)
    {
        try { return new MailAddress(email).Address == email && email.Contains('@'); }
        catch { return false; }
    }
    static AuthResult Fail(string error) => new(false, error);
}

public static class PasswordRules
{
    public static bool IsStrong(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < 8) return false;
        return password.Any(char.IsLetter) && password.Any(char.IsDigit);
    }
}
