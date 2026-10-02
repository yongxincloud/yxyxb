using Microsoft.EntityFrameworkCore;

namespace Yxy.Api.Data;

public sealed class WebDb : DbContext
{
    public WebDb(DbContextOptions<WebDb> options) : base(options) { }
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<EmailCodeRow> EmailCodes => Set<EmailCodeRow>();
    public DbSet<AccountLoginLog> LoginLogs => Set<AccountLoginLog>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<ProviderLevel> ProviderLevels => Set<ProviderLevel>();
    public DbSet<Provider> Providers => Set<Provider>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Account>().HasIndex(x => x.Email).IsUnique();
        b.Entity<Customer>().HasIndex(x => x.AccountId).IsUnique();
        b.Entity<Customer>().HasIndex(x => x.CustomerNo).IsUnique();
        b.Entity<Provider>().HasIndex(x => x.ProviderNo).IsUnique();
        b.Entity<Provider>().HasIndex(x => x.AccountId).IsUnique();
        b.Entity<ProviderLevel>().HasIndex(x => x.Name).IsUnique();
    }
}
