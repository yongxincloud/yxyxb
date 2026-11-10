using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace YxyXb.DbPreflight;

public sealed class PreflightOptions
{
    public string DatabasePath { get; set; } = "";
    public string? BackupDirectory { get; set; }
    public string? ReportPath { get; set; }
}

public sealed class PreflightResult
{
    public bool Passed { get; set; }
    public string SourcePath { get; set; } = "";
    public string BackupPath { get; set; } = "";
    public string ReportPath { get; set; } = "";
    public DateTime GeneratedUtc { get; set; }
    public int PassedCount { get; set; }
    public int WarningCount { get; set; }
    public int FailureCount { get; set; }
    public Dictionary<string, long> TableCounts { get; set; } = new();
    public List<PreflightCheck> Checks { get; set; } = new();
}

public sealed class PreflightCheck
{
    public string Code { get; set; } = "";
    public string Status { get; set; } = "pass";
    public string Message { get; set; } = "";
    public long? Count { get; set; }
}

public static class PreflightRunner
{
    private static readonly string[] ExpectedTables =
    {
        "AdminUsers", "Accounts", "EmailCodes", "LoginLogs", "Customers",
        "ProviderLevels", "Providers", "Commissions", "Settlements", "OperationLogs",
        "Messages", "AuthTokens", "AccountDevices", "SubscriptionAuths", "Products",
        "Orders", "Payments", "Configs", "ProviderApplies", "InvoiceApplies",
        "ExpiryNoticeLogs"
    };

    private static readonly Dictionary<string, string[]> ExpectedColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AdminUsers"] = new[] { "Id", "Username", "PasswordHash", "Email", "CreatedAt" },
        ["Accounts"] = new[] { "Id", "Email", "PasswordHash", "Phone", "Status", "LastLoginAt", "CreatedAt", "UpdatedAt" },
        ["EmailCodes"] = new[] { "Id", "Email", "Code", "Scene", "Used", "ExpiresAt", "ReqIp", "CreatedAt" },
        ["LoginLogs"] = new[] { "Id", "AccountId", "Email", "Way", "DeviceId", "Ip", "Success", "Remark", "CreatedAt" },
        ["Customers"] = new[] { "Id", "CustomerNo", "AccountId", "Name", "Nickname", "CompanyName", "InvoiceTitle", "InvoiceTaxNo", "InvoiceEmail", "InvoiceLicense", "DirectProviderId", "Status", "CreatedAt", "UpdatedAt" },
        ["ProviderLevels"] = new[] { "Id", "Name", "CommissionRate", "SortNo", "Status", "CreatedAt", "UpdatedAt" },
        ["Providers"] = new[] { "Id", "ProviderNo", "AccountId", "Name", "LevelId", "Status", "EffectiveAt", "CreatedAt", "UpdatedAt" },
        ["Products"] = new[] { "Id", "ProductNo", "Name", "Description", "Price", "PromoPrice", "PromoStart", "PromoEnd", "MaxDevices", "SortOrder", "PeriodDays", "Status", "CreatedAt", "UpdatedAt" },
        ["Orders"] = new[] { "Id", "OrderNo", "CustomerId", "ProductId", "OrderType", "Amount", "ProviderId", "CommissionRateSnapshot", "PayStatus", "PayChannel", "TradeNo", "PayTime", "RefundTime", "CommissionCalculated", "ExpiresAt", "InvoiceApplyId", "Hidden", "CreatedAt", "UpdatedAt" },
        ["Payments"] = new[] { "Id", "OrderId", "Channel", "TradeNo", "Amount", "CreatedAt" },
        ["SubscriptionAuths"] = new[] { "Id", "CustomerId", "ProductId", "OrderId", "AuthStartTime", "AuthEndTime", "Status", "GrantType", "GrantBy", "GrantReason", "DeviceLimit", "CreatedAt", "UpdatedAt" },
        ["AccountDevices"] = new[] { "Id", "AccountId", "DeviceId", "DisplayName", "Note", "FirstSeenAt", "LastSeenAt", "WanIp", "TaskStatus", "Status", "KickedAt" },
        ["AuthTokens"] = new[] { "Id", "AccountId", "TokenHash", "DeviceId", "IssuedAt", "ExpiresAt", "Revoked", "RevokedAt" },
        ["Commissions"] = new[] { "Id", "OrderId", "ProviderId", "LevelId", "CommissionRateSnapshot", "EntryType", "Amount", "Settled", "SettlementId", "CreatedAt" },
        ["Settlements"] = new[] { "Id", "SettlementNo", "ProviderId", "PeriodStart", "PeriodEnd", "Amount", "Status", "InvoiceFile", "PayVoucherNo", "PaidAt", "CreatedAt", "UpdatedAt" },
        ["OperationLogs"] = new[] { "Id", "OperatorType", "Module", "Action", "BeforeValue", "AfterValue", "CreatedAt" },
        ["Messages"] = new[] { "Id", "AccountId", "Title", "Content", "IsRead", "CreatedAt" },
        ["InvoiceApplies"] = new[] { "Id", "ApplyNo", "CustomerId", "LicenseFile", "Amount", "TitleName", "TaxNo", "Email", "Issuer", "Status", "CreatedAt", "UpdatedAt" },
        ["Configs"] = new[] { "Id", "CfgKey", "CfgValue" },
        ["ProviderApplies"] = new[] { "Id", "AccountId", "CompanyName", "TaxNo", "LicenseFile", "ContactName", "ContactEmail", "ContactPhone", "Status", "CreatedAt" },
        ["ExpiryNoticeLogs"] = new[] { "Id", "CustomerId", "SentDay", "CreatedAt" }
    };

    public static PreflightResult Run(PreflightOptions options)
    {
        var sourcePath = Path.GetFullPath(options.DatabasePath);
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("数据库不存在。", sourcePath);
        }

        var backupDirectory = string.IsNullOrWhiteSpace(options.BackupDirectory)
            ? Path.Combine(Path.GetDirectoryName(sourcePath) ?? ".", "preflight-backups")
            : Path.GetFullPath(options.BackupDirectory);
        Directory.CreateDirectory(backupDirectory);
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var backupPath = Path.Combine(backupDirectory, Path.GetFileNameWithoutExtension(sourcePath) + "-" + stamp + ".db");
        var reportPath = string.IsNullOrWhiteSpace(options.ReportPath)
            ? Path.Combine(backupDirectory, "preflight-" + stamp + ".json")
            : Path.GetFullPath(options.ReportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath) ?? ".");

        var result = new PreflightResult
        {
            SourcePath = sourcePath,
            BackupPath = backupPath,
            ReportPath = reportPath,
            GeneratedUtc = DateTime.UtcNow
        };

        var sourceCounts = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var source = OpenReadOnly(sourcePath);
            CreateBackup(source, backupPath);
            AddCheck(result, "backup.created", "pass", "已创建一致备份。");

            using var backup = OpenReadOnly(backupPath);
            AddIntegrity(result, source, "source.integrity");
            AddIntegrity(result, backup, "backup.integrity");
            AddForeignKeyCheck(result, source);
            AddSchema(result, source);
            AddRowsAndRelations(result, source, sourceCounts);
            AddBackupComparison(result, backup, sourceCounts);
        }
        catch (Exception ex)
        {
            AddCheck(result, "preflight.exception", "fail", ex.GetType().Name + ": " + ex.Message);
        }

        result.Checks.Add(new PreflightCheck
        {
            Code = "backup.sha256",
            Status = File.Exists(backupPath) ? "pass" : "fail",
            Message = File.Exists(backupPath) ? Hash(backupPath) : "备份不存在。"
        });
        result.PassedCount = result.Checks.Count(x => x.Status == "pass");
        result.WarningCount = result.Checks.Count(x => x.Status == "warn");
        result.FailureCount = result.Checks.Count(x => x.Status == "fail");
        result.Passed = result.FailureCount == 0;

        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(reportPath, json);
        return result;
    }

    private static SqliteConnection OpenReadOnly(string path)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Private,
            Pooling = false
        }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA query_only=ON;";
        command.ExecuteNonQuery();
        return connection;
    }

    private static void CreateBackup(SqliteConnection source, string backupPath)
    {
        using var destination = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = backupPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        }.ToString());
        destination.Open();
        source.BackupDatabase(destination);
    }

    private static void AddIntegrity(PreflightResult result, SqliteConnection connection, string code)
    {
        var messages = ScalarStrings(connection, "PRAGMA integrity_check;").Where(x => !string.Equals(x, "ok", StringComparison.OrdinalIgnoreCase)).ToList();
        AddCheck(result, code, messages.Count == 0 ? "pass" : "fail", messages.Count == 0 ? "完整性检查通过。" : string.Join(" | ", messages));
    }

    private static void AddSchema(PreflightResult result, SqliteConnection connection)
    {
        var actualTables = ScalarStrings(connection, "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%';")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missingTables = ExpectedTables.Where(x => !actualTables.Contains(x)).ToArray();
        AddCheck(result, "schema.tables", missingTables.Length == 0 ? "pass" : "fail",
            missingTables.Length == 0 ? "预期业务表齐全。" : "缺少表: " + string.Join(",", missingTables), missingTables.Length);

        foreach (var table in ExpectedColumns.Keys)
        {
            if (!actualTables.Contains(table))
            {
                continue;
            }

            var actualColumns = TableColumns(connection, table);
            var expected = ExpectedColumns[table];
            var missing = expected.Where(x => !actualColumns.Contains(x)).ToArray();
            AddCheck(result, "schema.columns." + table.ToLowerInvariant(), missing.Length == 0 ? "pass" : "fail",
                missing.Length == 0 ? "关键列齐全。" : "缺少列: " + string.Join(",", missing), missing.Length);
        }
    }

    private static void AddRowsAndRelations(
        PreflightResult result,
        SqliteConnection connection,
        Dictionary<string, long> counts)
    {
        var actualTables = ScalarStrings(connection, "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%';")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var table in ExpectedTables)
        {
            if (!actualTables.Contains(table))
            {
                continue;
            }

            counts[table] = ScalarLong(connection, "SELECT COUNT(*) FROM \"" + table + "\";");
        }
        result.TableCounts = counts;

        AddLogicalCheck(result, connection, "relation.customer_account", "SELECT COUNT(*) FROM Customers c LEFT JOIN Accounts a ON a.Id=c.AccountId WHERE a.Id IS NULL;");
        AddLogicalCheck(result, connection, "relation.order_customer", "SELECT COUNT(*) FROM Orders o LEFT JOIN Customers c ON c.Id=o.CustomerId WHERE c.Id IS NULL;");
        AddLogicalCheck(result, connection, "relation.order_product", "SELECT COUNT(*) FROM Orders o LEFT JOIN Products p ON p.Id=o.ProductId WHERE p.Id IS NULL;");
        AddLogicalCheck(result, connection, "relation.auth_customer", "SELECT COUNT(*) FROM SubscriptionAuths a LEFT JOIN Customers c ON c.Id=a.CustomerId WHERE c.Id IS NULL;");
        AddLogicalCheck(result, connection, "relation.auth_product", "SELECT COUNT(*) FROM SubscriptionAuths a LEFT JOIN Products p ON p.Id=a.ProductId WHERE p.Id IS NULL;");
        AddLogicalCheck(result, connection, "relation.payment_order", "SELECT COUNT(*) FROM Payments p LEFT JOIN Orders o ON o.Id=p.OrderId WHERE o.Id IS NULL;");
        AddLogicalCheck(result, connection, "relation.device_account", "SELECT COUNT(*) FROM AccountDevices d LEFT JOIN Accounts a ON a.Id=d.AccountId WHERE a.Id IS NULL;");
        AddLogicalCheck(result, connection, "relation.commission_order", "SELECT COUNT(*) FROM Commissions c LEFT JOIN Orders o ON o.Id=c.OrderId WHERE o.Id IS NULL;");
        AddLogicalCheck(result, connection, "relation.commission_provider", "SELECT COUNT(*) FROM Commissions c LEFT JOIN Providers p ON p.Id=c.ProviderId WHERE p.Id IS NULL;");
        AddLogicalCheck(result, connection, "relation.settlement_provider", "SELECT COUNT(*) FROM Settlements s LEFT JOIN Providers p ON p.Id=s.ProviderId WHERE p.Id IS NULL;");
        AddLogicalCheck(result, connection, "relation.invoice_customer", "SELECT COUNT(*) FROM InvoiceApplies i LEFT JOIN Customers c ON c.Id=i.CustomerId WHERE c.Id IS NULL;");
        AddLogicalCheck(result, connection, "business.paid_order_amount", "SELECT COUNT(*) FROM Orders WHERE PayStatus=1 AND CAST(Amount AS REAL)<=0;");
        AddLogicalCheck(result, connection, "business.paid_order_paytime", "SELECT COUNT(*) FROM Orders WHERE PayStatus=1 AND PayTime IS NULL;", warning: true);
        AddLogicalCheck(result, connection, "business.active_auth_product", "SELECT COUNT(*) FROM SubscriptionAuths a LEFT JOIN Products p ON p.Id=a.ProductId WHERE a.Status=1 AND p.Id IS NULL;");
        AddLogicalCheck(result, connection, "business.active_device_account", "SELECT COUNT(*) FROM AccountDevices d LEFT JOIN Accounts a ON a.Id=d.AccountId WHERE d.Status=1 AND a.Id IS NULL;");
    }

    private static void AddBackupComparison(
        PreflightResult result,
        SqliteConnection backup,
        Dictionary<string, long> sourceCounts)
    {
        var differences = new List<string>();
        foreach (var pair in sourceCounts)
        {
            var backupCount = ScalarLong(backup, "SELECT COUNT(*) FROM \"" + pair.Key + "\";");
            if (backupCount != pair.Value)
            {
                differences.Add(pair.Key + ":" + pair.Value + "->" + backupCount);
            }
        }

        AddCheck(result, "backup.counts", differences.Count == 0 ? "pass" : "fail",
            differences.Count == 0 ? "备份行数与源快照一致。" : "备份行数不一致: " + string.Join(",", differences), differences.Count);
    }

    private static void AddLogicalCheck(
        PreflightResult result,
        SqliteConnection connection,
        string code,
        string sql,
        bool warning = false)
    {
        var count = ScalarLong(connection, sql);
        AddCheck(result, code, count == 0 ? "pass" : (warning ? "warn" : "fail"),
            count == 0 ? "检查通过。" : "发现 " + count + " 条异常。", count);
    }

    private static void AddForeignKeyCheck(PreflightResult result, SqliteConnection connection)
    {
        var count = 0L;
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_key_check;";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            count++;
        }

        AddCheck(result, "schema.foreign_keys", count == 0 ? "pass" : "fail",
            count == 0 ? "SQLite 外键检查通过。" : "发现 " + count + " 条外键异常。", count);
    }

    private static void AddCheck(PreflightResult result, string code, string status, string message, long? count = null)
    {
        result.Checks.Add(new PreflightCheck { Code = code, Status = status, Message = message, Count = count });
    }

    private static List<string> ScalarStrings(SqliteConnection connection, string sql)
    {
        var values = new List<string>();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            values.Add(reader.IsDBNull(0) ? "" : reader.GetString(0));
        }
        return values;
    }

    private static List<string> TableColumns(SqliteConnection connection, string table)
    {
        var values = new List<string>();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(\"" + table + "\");";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            values.Add(reader.IsDBNull(1) ? "" : reader.GetString(1));
        }

        return values;
    }

    private static long ScalarLong(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(command.ExecuteScalar());
    }

    private static string Hash(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}
