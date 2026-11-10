using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using YxyXb.Api.Data;

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
}
