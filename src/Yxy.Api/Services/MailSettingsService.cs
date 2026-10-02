using System.Net;
using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using Yxy.Api.Data;

namespace Yxy.Api.Services;

public sealed record MailSettings(string Host, int Port, string User, string Password, string From, bool Ssl)
{
    public bool Configured => !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(From);
}

public sealed class MailSettingsService
{
    public const string HostKey = "mail.host";
    public const string PortKey = "mail.port";
    public const string UserKey = "mail.user";
    public const string PasswordKey = "mail.password";
    public const string FromKey = "mail.from";
    public const string SslKey = "mail.ssl";

    private readonly WebDb _db;
    private readonly IConfiguration _config;

    public MailSettingsService(WebDb db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public async Task<bool> IsConfiguredAsync() => (await LoadAsync()).Configured;

    public async Task<MailSettings> LoadAsync()
    {
        var rows = await _db.Configs.Where(x => x.CfgKey.StartsWith("mail.")).ToListAsync();
        if (rows.Count == 0)
        {
            var port = int.TryParse(_config["Smtp:Port"], out var parsed) ? parsed : 587;
            return new MailSettings(
                _config["Smtp:Host"] ?? "",
                port,
                _config["Smtp:User"] ?? "",
                _config["Smtp:Password"] ?? "",
                _config["Smtp:From"] ?? "",
                _config["Smtp:Ssl"] != "false");
        }

        string Value(string key) => rows.FirstOrDefault(x => x.CfgKey == key)?.CfgValue ?? "";
        var savedPort = int.TryParse(Value(PortKey), out var number) ? number : 587;
        return new MailSettings(Value(HostKey), savedPort, Value(UserKey), Value(PasswordKey), Value(FromKey), Value(SslKey) != "false");
    }

    public async Task<string?> SaveAsync(string? host, int? port, string? user, string? password, string? from, bool ssl)
    {
        var cleanHost = (host ?? "").Trim();
        var cleanFrom = (from ?? "").Trim();
        var cleanPort = port is > 0 and <= 65535 ? port.Value : 587;
        if (!string.IsNullOrEmpty(cleanHost) && !cleanFrom.Contains('@'))
            return "请填写发件人邮箱";
        var current = await LoadAsync();
        var cleanPassword = string.IsNullOrEmpty(password) ? current.Password : password;
        await Upsert(HostKey, cleanHost);
        await Upsert(PortKey, cleanPort.ToString());
        await Upsert(UserKey, (user ?? "").Trim());
        await Upsert(PasswordKey, cleanPassword ?? "");
        await Upsert(FromKey, cleanFrom);
        await Upsert(SslKey, ssl ? "true" : "false");
        _db.OperationLogs.Add(new OperationLog
        {
            Module = "mail",
            Action = "save",
            AfterValue = string.IsNullOrEmpty(cleanHost) ? "未配置" : cleanHost,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        return null;
    }

    public static async Task SendAsync(MailSettings settings, string to, string subject, string body)
    {
        if (!settings.Configured) throw new InvalidOperationException("还没有配置发信服务器");
        try
        {
            using var message = new MailMessage(settings.From, to, subject, body);
            using var client = new SmtpClient(settings.Host, settings.Port);
            if (!string.IsNullOrWhiteSpace(settings.User))
                client.Credentials = new NetworkCredential(settings.User, settings.Password);
            client.EnableSsl = settings.Ssl;
            await client.SendMailAsync(message);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(ex.Message);
        }
    }

    async Task Upsert(string key, string value)
    {
        var row = await _db.Configs.SingleOrDefaultAsync(x => x.CfgKey == key);
        if (row == null)
            _db.Configs.Add(new SystemConfigRow { CfgKey = key, CfgValue = value });
        else
            row.CfgValue = value;
    }
}

public sealed class RoutingEmailSender : IEmailSender
{
    private readonly MailSettingsService _settings;
    private readonly FakeEmailSender _fake;

    public RoutingEmailSender(MailSettingsService settings, FakeEmailSender fake)
    {
        _settings = settings;
        _fake = fake;
    }

    public async Task SendAsync(string to, string subject, string body)
    {
        var mail = await _settings.LoadAsync();
        if (!mail.Configured)
        {
            await _fake.SendAsync(to, subject, body);
            return;
        }

        await MailSettingsService.SendAsync(mail, to, subject, body);
    }
}
