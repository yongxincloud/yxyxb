using Microsoft.EntityFrameworkCore;
using YxyXb.Api.Data;

namespace YxyXb.Api.Services;

public sealed class SiteMaintenance : BackgroundService
{
    private readonly IServiceProvider _services;
    public SiteMaintenance(IServiceProvider services) => _services = services;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await TickAsync(); }
            catch (OperationCanceledException) { break; }
            catch { }
            await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
        }
    }

    async Task TickAsync()
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WebDb>();
        var now = DateTime.UtcNow;
        var cutoff = now.AddDays(-7);
        var stale = db.SubscriptionAuths.Where(x => x.Status == 1 && x.AuthEndTime < cutoff).ToList();
        foreach (var row in stale) row.Status = 2;
        if (now.Day == 1)
        {
            var title = now.ToString("yyyy-MM") + " 结算提醒";
            var providers = db.Providers.Where(x => x.Status == 1).ToList();
            foreach (var provider in providers)
            {
                var exists = db.Messages.Any(x => x.AccountId == provider.AccountId && x.Title == title);
                if (exists) continue;
                db.Messages.Add(new SiteMessage
                {
                    AccountId = provider.AccountId,
                    Title = title,
                    Content = "可以发起上一个自然月的佣金结算。",
                    CreatedAt = now
                });
            }
        }
        try
        {
            var mailSvc = scope.ServiceProvider.GetRequiredService<MailSettingsService>();
            var notices = scope.ServiceProvider.GetRequiredService<ExpiryNoticeService>();
            var mail = await mailSvc.LoadAsync();
            await notices.SendDueAsync(db, mail, now, (to, subject, body) => MailSettingsService.SendAsync(mail, to, subject, body));
        }
        catch { }
        await db.SaveChangesAsync();
    }
}
