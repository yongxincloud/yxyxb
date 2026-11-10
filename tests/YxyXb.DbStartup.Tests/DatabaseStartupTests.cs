using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using System.Security.Cryptography;
using YxyXb.Api.Data;
using YxyXb.Api.Services;

namespace YxyXb.DbStartup.Tests;

public class DatabaseStartupTests
{
    private string _root = "";
    private string _path = "";
    private string _backupDirectory = "";

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "yxy-startup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _path = Path.Combine(_root, "web.db");
        _backupDirectory = Path.Combine(_root, "backups");
    }

    [TearDown]
    public void TearDown()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    [Test]
    public void EmptyDatabase_InitializesOnceAndIsIdempotent()
    {
        using var db = Open();
        var service = new DatabaseStartupService();

        Assert.That(service.Initialize(db), Is.EqualTo(1));
        Assert.That(service.Initialize(db), Is.EqualTo(1));
        Assert.That(ScalarLong(db, "SELECT COUNT(*) FROM SchemaVersions;"), Is.EqualTo(1));
        Assert.That(ScalarLong(db, "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='ExpiryNoticeLogs';"), Is.EqualTo(1));
    }

    [Test]
    public void LegacyDatabase_PreservesExistingProductAndAddsMissingSchema()
    {
        using (var connection = new SqliteConnection("Data Source=" + _path + ";Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE "Products" (
                  "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                  "ProductNo" TEXT NOT NULL,
                  "Name" TEXT NOT NULL,
                  "Description" TEXT NULL,
                  "Price" TEXT NOT NULL,
                  "PeriodDays" INTEGER NOT NULL,
                  "Status" INTEGER NOT NULL,
                  "CreatedAt" TEXT NOT NULL,
                  "UpdatedAt" TEXT NOT NULL
                );
                INSERT INTO "Products" ("ProductNo","Name","Description","Price","PeriodDays","Status","CreatedAt","UpdatedAt")
                VALUES ('LEGACY','历史产品','legacy','98',365,0,'2030-01-01T00:00:00Z','2030-01-01T00:00:00Z');
                """;
            command.ExecuteNonQuery();
        }

        using var db = Open();
        var version = new DatabaseStartupService().Initialize(db);
        var product = db.Products.Single(x => x.ProductNo == "LEGACY");

        Assert.That(version, Is.EqualTo(1));
        Assert.That(product.Name, Is.EqualTo("历史产品"));
        Assert.That(product.MaxDevices, Is.EqualTo(10));
        Assert.That(ScalarLong(db, "SELECT COUNT(*) FROM SchemaVersions;"), Is.EqualTo(1));
    }

    [Test]
    public async Task Seed_IsIdempotentAndDoesNotOverwriteExistingProduct()
    {
        using var db = Open();
        new DatabaseStartupService().Initialize(db);
        db.Products.Add(new Product
        {
            ProductNo = "PERSONAL",
            Name = "客户自定义名称",
            Price = 1,
            MaxDevices = 1,
            PeriodDays = 365,
            Status = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var seed = new DatabaseSeedService(db, new FixedTimeProvider(new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero)));

        await seed.EnsureCatalogAsync();
        await seed.EnsureCatalogAsync();
        await seed.EnsureAdminAsync("admin", "password", "admin@example.invalid", false, new FakeHasher());
        await seed.EnsureAdminAsync("admin", "other-password", "admin@example.invalid", false, new FakeHasher());

        Assert.That(await db.Products.CountAsync(), Is.EqualTo(4));
        Assert.That((await db.Products.SingleAsync(x => x.ProductNo == "PERSONAL")).Name, Is.EqualTo("客户自定义名称"));
        Assert.That((await db.Products.SingleAsync(x => x.ProductNo == "PERSONAL")).Status, Is.Zero);
        Assert.That(await db.Configs.CountAsync(), Is.EqualTo(2));
        Assert.That(await db.AdminUsers.CountAsync(), Is.EqualTo(1));
        Assert.That((await db.AdminUsers.SingleAsync()).PasswordHash, Is.EqualTo("hash:password"));
    }

    [Test]
    public async Task StartupRunner_CreatesConsistentBackupAndPreservesBusinessSemantics()
    {
        using var db = Open();
        new DatabaseStartupService().Initialize(db);
        await AddProductionShapedData(db);
        db.Database.ExecuteSqlRaw("DROP TABLE \"SchemaVersions\";");

        var result = new DatabaseStartupRunner(db).Run(new DatabaseStartupOptions
        {
            BackupDirectory = _backupDirectory,
            ReportDirectory = _backupDirectory
        });

        Assert.That(result.Version, Is.EqualTo(1));
        Assert.That(result.BackupPath, Is.Not.Null);
        Assert.That(File.Exists(result.BackupPath), Is.True);
        Assert.That(File.Exists(result.PreflightReportPath), Is.True);
        Assert.That(File.Exists(result.VerificationReportPath), Is.True);
        Assert.That(result.TableCounts["Customers"], Is.EqualTo(1));
        Assert.That(result.TableCounts["Orders"], Is.EqualTo(1));

        var customer = db.Customers.Single();
        var order = db.Orders.Single();
        var auth = db.SubscriptionAuths.Single();
        Assert.That(customer.InvoiceTaxNo, Is.EqualTo("91310000PRIVATE"));
        Assert.That(customer.DirectProviderId, Is.Not.Null);
        Assert.That(order.CommissionRateSnapshot, Is.EqualTo(0.35m));
        Assert.That(auth.AuthEndTime, Is.EqualTo(new DateTime(2031, 2, 3, 4, 5, 6, DateTimeKind.Utc)));
        Assert.That(auth.DeviceLimit, Is.EqualTo(7));

        using var copy = new WebDb(new DbContextOptionsBuilder<WebDb>()
            .UseSqlite("Data Source=" + result.BackupPath + ";Pooling=False")
            .Options);
        Assert.That(await copy.Customers.CountAsync(), Is.EqualTo(1));
        Assert.That(await copy.Orders.CountAsync(), Is.EqualTo(1));
        Assert.That(File.ReadAllText(result.PreflightReportPath!), Does.Not.Contain("private@example.invalid"));
        Assert.That(File.ReadAllText(result.PreflightReportPath!), Does.Not.Contain("91310000PRIVATE"));

        copy.Dispose();
        db.Dispose();
        SqliteConnection.ClearAllPools();
        if (File.Exists(_path + "-wal")) File.Delete(_path + "-wal");
        if (File.Exists(_path + "-shm")) File.Delete(_path + "-shm");
        File.Copy(result.BackupPath!, _path, true);
        using var restored = Open();
        Assert.That(ScalarLong(restored, "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='SchemaVersions';"), Is.Zero);
        Assert.That(restored.Customers.Single().InvoiceTaxNo, Is.EqualTo("91310000PRIVATE"));
        Assert.That(restored.Orders.Single().CommissionRateSnapshot, Is.EqualTo(0.35m));
    }

    [Test]
    public void StartupRunner_RejectsInvalidDatabaseBeforeMigration()
    {
        File.WriteAllText(_path, "not a sqlite database");
        using var db = Open();
        var before = Hash(_path);

        var exception = Assert.Throws<DatabaseStartupException>(delegate
        {
            new DatabaseStartupRunner(db).Run(new DatabaseStartupOptions
            {
                BackupDirectory = _backupDirectory,
                ReportDirectory = _backupDirectory
            });
        });

        Assert.That(exception!.Stage, Is.EqualTo("预检"));
        Assert.That(exception.ReportPath, Is.Not.Null);
        Assert.That(File.Exists(exception.ReportPath), Is.True);
        Assert.That(Hash(_path), Is.EqualTo(before));
    }

    [Test]
    public void StartupRunner_RepeatedStartUsesVerificationWithoutAnotherBackup()
    {
        using var db = Open();
        new DatabaseStartupService().Initialize(db);

        var runner = new DatabaseStartupRunner(db);
        var first = runner.Run(new DatabaseStartupOptions
        {
            BackupDirectory = _backupDirectory,
            ReportDirectory = _backupDirectory
        });
        var second = runner.Run(new DatabaseStartupOptions
        {
            BackupDirectory = _backupDirectory,
            ReportDirectory = _backupDirectory
        });

        Assert.That(first.Version, Is.EqualTo(1));
        Assert.That(second.Version, Is.EqualTo(1));
        Assert.That(second.BackupPath, Is.Null.Or.Empty);
        Assert.That(second.PreflightReportPath, Is.Not.Null);
    }

    [Test]
    public void Migration_InterruptedAfterSchemaApplied_CanRetryWithoutDataLoss()
    {
        using var db = Open();
        var service = new DatabaseStartupService();
        var steps = new List<string>();

        Assert.Throws<InvalidOperationException>(delegate
        {
            service.Initialize(db, step =>
            {
                steps.Add(step);
                if (step == "schema.version.1.applied")
                    throw new InvalidOperationException("simulated interruption");
            });
        });

        Assert.That(steps, Contains.Item("schema.version.1.applied"));
        Assert.That(service.TryReadVersion(db), Is.EqualTo(0));
        Assert.That(service.Initialize(db), Is.EqualTo(1));
        Assert.That(service.Initialize(db), Is.EqualTo(1));
    }

    [Test]
    public async Task StartupRunner_MigrationFailureWritesReportAndKeepsBackup()
    {
        using var db = Open();
        new DatabaseStartupService().Initialize(db);
        await AddProductionShapedData(db);
        db.Database.ExecuteSqlRaw("DELETE FROM \"SchemaVersions\";");
        db.Database.ExecuteSqlRaw("""
            CREATE TRIGGER "FailSchemaVersion"
            BEFORE INSERT ON "SchemaVersions"
            BEGIN
              SELECT RAISE(ABORT, 'simulated migration failure');
            END;
            """);

        var exception = Assert.Throws<DatabaseStartupException>(delegate
        {
            new DatabaseStartupRunner(db).Run(new DatabaseStartupOptions
            {
                BackupDirectory = _backupDirectory,
                ReportDirectory = _backupDirectory
            });
        });

        Assert.That(exception!.Stage, Is.EqualTo("迁移"));
        Assert.That(exception.BackupPath, Is.Not.Null);
        Assert.That(File.Exists(exception.BackupPath), Is.True);
        Assert.That(exception.ReportPath, Is.Not.Null);
        Assert.That(File.Exists(exception.ReportPath), Is.True);
        var report = File.ReadAllText(exception.ReportPath!);
        Assert.That(report, Does.Contain("schema.version.1.applied"));
        Assert.That(report, Does.Contain("\"VersionBefore\": 0"));
        Assert.That(report, Does.Not.Contain("private@example.invalid"));
        Assert.That(report, Does.Not.Contain("91310000PRIVATE"));
    }

    [Test]
    public void ProductionCopy_DryRunPreservesBusinessSemantics()
    {
        var productionCopy = Environment.GetEnvironmentVariable("YXYXB_PRODUCTION_DB");
        if (string.IsNullOrWhiteSpace(productionCopy))
            Assert.Ignore("未设置 YXYXB_PRODUCTION_DB，跳过生产副本演练。");
        if (!File.Exists(productionCopy))
            Assert.Fail("YXYXB_PRODUCTION_DB 指向的文件不存在。");

        var sourceHash = Hash(productionCopy!);
        File.Copy(productionCopy!, _path);
        using var db = Open();

        var result = new DatabaseStartupRunner(db).Run(new DatabaseStartupOptions
        {
            BackupDirectory = _backupDirectory,
            ReportDirectory = _backupDirectory
        });

        Assert.That(result.Version, Is.EqualTo(1));
        Assert.That(result.VersionBefore.GetValueOrDefault(), Is.EqualTo(0));
        Assert.That(result.BackupPath, Is.Not.Null.And.Not.Empty);
        Assert.That(File.Exists(result.BackupPath), Is.True);
        Assert.That(File.Exists(result.PreflightReportPath), Is.True);
        Assert.That(File.Exists(result.VerificationReportPath), Is.True);
        Assert.That(result.TableCounts["Customers"], Is.GreaterThan(0));
        Assert.That(result.TableCounts["Orders"], Is.GreaterThan(0));
        Assert.That(result.TableCounts["SubscriptionAuths"], Is.GreaterThan(0));
        Assert.That(Hash(productionCopy!), Is.EqualTo(sourceHash));

        var preflight = File.ReadAllText(result.PreflightReportPath!);
        var verification = File.ReadAllText(result.VerificationReportPath!);
        Assert.That(preflight, Does.Contain("\"business.semantics\""));
        Assert.That(verification, Does.Contain("\"migration.semantics\""));
    }

    private WebDb Open()
    {
        var options = new DbContextOptionsBuilder<WebDb>()
            .UseSqlite("Data Source=" + _path + ";Pooling=False")
            .Options;
        return new WebDb(options);
    }

    private static long ScalarLong(WebDb db, string sql)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open) connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(command.ExecuteScalar());
    }

    private static string Hash(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static async Task AddProductionShapedData(WebDb db)
    {
        var now = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var level = new ProviderLevel
        {
            Name = "历史等级",
            CommissionRate = 0.35m,
            SortNo = 1,
            CreatedAt = now,
            UpdatedAt = now
        };
        var providerAccount = new Account
        {
            Email = "provider@example.invalid",
            PasswordHash = "not-exported",
            CreatedAt = now,
            UpdatedAt = now
        };
        var customerAccount = new Account
        {
            Email = "private@example.invalid",
            PasswordHash = "not-exported",
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Accounts.AddRange(providerAccount, customerAccount);
        db.ProviderLevels.Add(level);
        await db.SaveChangesAsync();

        var provider = new Provider
        {
            ProviderNo = "PV-LEGACY",
            AccountId = providerAccount.Id,
            Name = "历史服务商",
            LevelId = level.Id,
            Status = 1,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Providers.Add(provider);
        var product = new Product
        {
            ProductNo = "LEGACY-OFFLINE",
            Name = "已下架旧产品",
            Price = 88m,
            MaxDevices = 3,
            PeriodDays = 365,
            Status = 0,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var customer = new Customer
        {
            CustomerNo = "CU-LEGACY",
            AccountId = customerAccount.Id,
            Nickname = "保留昵称",
            CompanyName = "历史企业",
            InvoiceTitle = "历史开票抬头",
            InvoiceTaxNo = "91310000PRIVATE",
            InvoiceEmail = "invoice@example.invalid",
            DirectProviderId = provider.Id,
            Status = 1,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var invoice = new InvoiceApply
        {
            ApplyNo = "INV-LEGACY",
            CustomerId = customer.Id,
            Amount = 88m,
            TitleName = "历史开票抬头",
            TaxNo = "91310000PRIVATE",
            Email = "invoice@example.invalid",
            Status = 2,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.InvoiceApplies.Add(invoice);
        await db.SaveChangesAsync();

        var order = new SubscriptionOrder
        {
            OrderNo = "OR-LEGACY",
            CustomerId = customer.Id,
            ProductId = product.Id,
            Amount = 88m,
            ProviderId = provider.Id,
            CommissionRateSnapshot = 0.35m,
            PayStatus = 1,
            PayChannel = "history",
            TradeNo = "TRADE-LEGACY",
            PayTime = now.AddMinutes(1),
            CommissionCalculated = 1,
            ExpiresAt = now.AddYears(1),
            InvoiceApplyId = invoice.Id,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        db.Payments.Add(new PaymentRecord
        {
            OrderId = order.Id,
            Channel = "history",
            TradeNo = "PAY-LEGACY",
            Amount = 88m,
            CreatedAt = now.AddMinutes(1)
        });
        db.SubscriptionAuths.Add(new SubscriptionAuth
        {
            CustomerId = customer.Id,
            ProductId = product.Id,
            OrderId = order.Id,
            AuthStartTime = now.AddMinutes(1),
            AuthEndTime = new DateTime(2031, 2, 3, 4, 5, 6, DateTimeKind.Utc),
            Status = 1,
            GrantType = 1,
            GrantReason = "历史授权",
            DeviceLimit = 7,
            CreatedAt = now,
            UpdatedAt = now
        });
        db.AccountDevices.Add(new AccountDevice
        {
            AccountId = customerAccount.Id,
            DeviceId = "DEVICE-LEGACY",
            DisplayName = "历史设备",
            FirstSeenAt = now,
            LastSeenAt = now,
            Status = 0,
            KickedAt = now.AddDays(1)
        });

        var settlement = new Settlement
        {
            SettlementNo = "ST-LEGACY",
            ProviderId = provider.Id,
            PeriodStart = now.AddMonths(-1),
            PeriodEnd = now,
            Amount = 30.8m,
            Status = 2,
            PaidAt = now.AddDays(2),
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Settlements.Add(settlement);
        await db.SaveChangesAsync();
        db.Commissions.Add(new CommissionDetail
        {
            OrderId = order.Id,
            ProviderId = provider.Id,
            LevelId = level.Id,
            CommissionRateSnapshot = 0.35m,
            Amount = 30.8m,
            Settled = 1,
            SettlementId = settlement.Id,
            CreatedAt = now
        });
        await db.SaveChangesAsync();
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _value;
        public FixedTimeProvider(DateTimeOffset value) => _value = value;
        public override DateTimeOffset GetUtcNow() => _value;
    }

    private sealed class FakeHasher : IPasswordHasher
    {
        public string Hash(string password) => "hash:" + password;
        public bool Verify(string password, string hash) => hash == Hash(password);
    }
}
