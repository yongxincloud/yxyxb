using Microsoft.EntityFrameworkCore;

namespace YxyXb.Api.Data;

public sealed class WebDb : DbContext
{
    public WebDb(DbContextOptions<WebDb> options) : base(options) { }
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<EmailCodeRow> EmailCodes => Set<EmailCodeRow>();
    public DbSet<AccountLoginLog> LoginLogs => Set<AccountLoginLog>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<ProviderLevel> ProviderLevels => Set<ProviderLevel>();
    public DbSet<Provider> Providers => Set<Provider>();
    public DbSet<CommissionDetail> Commissions => Set<CommissionDetail>();
    public DbSet<Settlement> Settlements => Set<Settlement>();
    public DbSet<OperationLog> OperationLogs => Set<OperationLog>();
    public DbSet<SiteMessage> Messages => Set<SiteMessage>();
    public DbSet<AuthToken> AuthTokens => Set<AuthToken>();
    public DbSet<AccountDevice> AccountDevices => Set<AccountDevice>();
    public DbSet<SubscriptionAuth> SubscriptionAuths => Set<SubscriptionAuth>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<SubscriptionOrder> Orders => Set<SubscriptionOrder>();
    public DbSet<PaymentRecord> Payments => Set<PaymentRecord>();
    public DbSet<SystemConfigRow> Configs => Set<SystemConfigRow>();
    public DbSet<ProviderApply> ProviderApplies => Set<ProviderApply>();
    public DbSet<InvoiceApply> InvoiceApplies => Set<InvoiceApply>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<AdminUser>().HasIndex(x => x.Username).IsUnique();
        b.Entity<Account>().HasIndex(x => x.Email).IsUnique();
        b.Entity<Customer>().HasIndex(x => x.AccountId).IsUnique();
        b.Entity<Customer>().HasIndex(x => x.CustomerNo).IsUnique();
        b.Entity<Provider>().HasIndex(x => x.ProviderNo).IsUnique();
        b.Entity<Provider>().HasIndex(x => x.AccountId).IsUnique();
        b.Entity<CommissionDetail>().HasIndex(x => new { x.OrderId, x.ProviderId, x.EntryType }).IsUnique();
        b.Entity<Settlement>().HasIndex(x => x.SettlementNo).IsUnique();
        b.Entity<ProviderLevel>().HasIndex(x => x.Name).IsUnique();
        b.Entity<AuthToken>().HasIndex(x => x.TokenHash).IsUnique();
        b.Entity<AccountDevice>().HasIndex(x => new { x.AccountId, x.DeviceId }).IsUnique();
        b.Entity<SubscriptionAuth>().HasIndex(x => new { x.CustomerId, x.AuthEndTime });
        b.Entity<Product>().HasIndex(x => x.ProductNo).IsUnique();
        b.Entity<SubscriptionOrder>().HasIndex(x => x.OrderNo).IsUnique();
        b.Entity<SubscriptionOrder>().HasIndex(x => x.TradeNo).IsUnique();
        b.Entity<PaymentRecord>().HasIndex(x => x.TradeNo).IsUnique();
        b.Entity<SystemConfigRow>().HasIndex(x => x.CfgKey).IsUnique();
        b.Entity<InvoiceApply>().HasIndex(x => x.ApplyNo).IsUnique();
    }
}
