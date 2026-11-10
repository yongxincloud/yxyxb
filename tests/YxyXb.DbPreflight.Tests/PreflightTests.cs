using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using NUnit.Framework;
using YxyXb.Database;

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

    [Test]
    public void Preflight_BackupSpaceFailure_DoesNotCreatePartialBackup()
    {
        var root = Path.Combine(Path.GetTempPath(), "yxy-preflight-space-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var dbPath = Path.Combine(root, "web.db");
        try
        {
            using (var db = new SqliteConnection("Data Source=" + dbPath + ";Pooling=False"))
            {
                db.Open();
                using var command = db.CreateCommand();
                command.CommandText = "CREATE TABLE Customers (Id INTEGER PRIMARY KEY);";
                command.ExecuteNonQuery();
            }

            var result = PreflightRunner.Run(new PreflightOptions
            {
                DatabasePath = dbPath,
                BackupDirectory = Path.Combine(root, "backups"),
                AvailableBytesProvider = _ => 0
            });

            Assert.That(result.Passed, Is.False);
            Assert.That(result.BackupPath, Is.Not.Empty);
            Assert.That(File.Exists(result.BackupPath), Is.False);
            Assert.That(result.Checks.Single(x => x.Code == "backup.space").Status, Is.EqualTo("fail"));
            Assert.That(File.Exists(result.ReportPath), Is.True);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Test]
    public void Preflight_BackupWriteFailure_StopsAndPreservesSource()
    {
        var root = Path.Combine(Path.GetTempPath(), "yxy-preflight-write-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var dbPath = Path.Combine(root, "web.db");
        var blockedDirectory = Path.Combine(root, "blocked");
        try
        {
            using (var db = new SqliteConnection("Data Source=" + dbPath + ";Pooling=False"))
            {
                db.Open();
                using var command = db.CreateCommand();
                command.CommandText = "CREATE TABLE Customers (Id INTEGER PRIMARY KEY);";
                command.ExecuteNonQuery();
            }
            File.WriteAllText(blockedDirectory, "not a directory");
            var before = Hash(dbPath);

            Assert.Throws<IOException>(delegate
            {
                PreflightRunner.Run(new PreflightOptions
                {
                    DatabasePath = dbPath,
                    BackupDirectory = blockedDirectory
                });
            });
            Assert.That(Hash(dbPath), Is.EqualTo(before));
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
