using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using YxyXb.Api.Data;
using YxyXb.Api.Services;

namespace YxyXb.DbStartup.Tests;

public class DatabaseStartupTests
{
    private string _path = "";

    [SetUp]
    public void SetUp()
    {
        _path = Path.Combine(Path.GetTempPath(), "yxy-schema-" + Guid.NewGuid().ToString("N") + ".db");
    }

    [TearDown]
    public void TearDown()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_path)) File.Delete(_path);
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
