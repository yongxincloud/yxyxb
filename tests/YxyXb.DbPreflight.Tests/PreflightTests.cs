using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using NUnit.Framework;
using YxyXb.DbPreflight;

namespace YxyXb.DbPreflight.Tests;

public class PreflightTests
{
    [Test]
    public void Preflight_DoesNotModifySource_AndDoesNotLeakValues()
    {
        var root = Path.Combine(Path.GetTempPath(), "yxy-preflight-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var dbPath = Path.Combine(root, "web.db");
        try
        {
            using (var db = new SqliteConnection("Data Source=" + dbPath + ";Pooling=False"))
            {
                db.Open();
                using var command = db.CreateCommand();
                command.CommandText = "CREATE TABLE Customers (Id INTEGER PRIMARY KEY, Email TEXT); INSERT INTO Customers (Email) VALUES ('private@example.invalid');";
                command.ExecuteNonQuery();
            }

            var before = Hash(dbPath);
            var result = PreflightRunner.Run(new PreflightOptions
            {
                DatabasePath = dbPath,
                BackupDirectory = Path.Combine(root, "backups")
            });

            Assert.That(result.Passed, Is.False);
            Assert.That(File.Exists(result.BackupPath), Is.True);
            Assert.That(File.Exists(result.ReportPath), Is.True);
            Assert.That(Hash(dbPath), Is.EqualTo(before));
            Assert.That(File.ReadAllText(result.ReportPath), Does.Not.Contain("private@example.invalid"));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}
