using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace YxyXb.Database;

public sealed class PreflightOptions
{
    public string DatabasePath { get; set; } = "";
    public string? BackupDirectory { get; set; }
    public string? ReportPath { get; set; }
    public bool CreateBackup { get; set; } = true;
    public bool RequireMigratedSchema { get; set; }
    public Dictionary<string, long>? ExpectedTableCounts { get; set; }
    public string? ExpectedSemanticFingerprint { get; set; }
    public Func<string, long>? AvailableBytesProvider { get; set; }
}

public sealed class PreflightResult
{
    public bool Passed { get; set; }
    public string SourcePath { get; set; } = "";
    public string BackupPath { get; set; } = "";
    public string ReportPath { get; set; } = "";
    public DateTime GeneratedUtc { get; set; }
    public int? SchemaVersion { get; set; }
    public int PassedCount { get; set; }
    public int WarningCount { get; set; }
    public int FailureCount { get; set; }
    public string SemanticFingerprint { get; set; } = "";
    public Dictionary<string, long> TableCounts { get; set; } = new();
    public Dictionary<string, string> TableFingerprints { get; set; } = new();
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
        ["SchemaVersions"] = new[] { "Version", "AppliedAtUtc" },
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

    private static readonly Dictionary<string, string[][]> ExpectedUniqueIndexes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AdminUsers"] = new[] { new[] { "Username" }, new[] { "Email" } },
        ["Accounts"] = new[] { new[] { "Email" } },
        ["Customers"] = new[] { new[] { "AccountId" }, new[] { "CustomerNo" } },
        ["ProviderLevels"] = new[] { new[] { "Name" } },
        ["Providers"] = new[] { new[] { "ProviderNo" }, new[] { "AccountId" } },
        ["Products"] = new[] { new[] { "ProductNo" } },
        ["Orders"] = new[] { new[] { "OrderNo" }, new[] { "TradeNo" } },
        ["Payments"] = new[] { new[] { "TradeNo" } },
        ["AccountDevices"] = new[] { new[] { "AccountId", "DeviceId" } },
        ["AuthTokens"] = new[] { new[] { "TokenHash" } },
        ["Commissions"] = new[] { new[] { "OrderId", "ProviderId", "EntryType" } },
        ["Settlements"] = new[] { new[] { "SettlementNo" } },
        ["Configs"] = new[] { new[] { "CfgKey" } },
        ["InvoiceApplies"] = new[] { new[] { "ApplyNo" } },
        ["ExpiryNoticeLogs"] = new[] { new[] { "CustomerId", "SentDay" } }
    };

    private static readonly Dictionary<string, string[]> SemanticColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Accounts"] = new[] { "Id", "Email", "Phone", "Status", "LastLoginAt", "CreatedAt", "UpdatedAt" },
        ["Customers"] = new[] { "Id", "CustomerNo", "AccountId", "Name", "Nickname", "CompanyName", "InvoiceTitle", "InvoiceTaxNo", "InvoiceEmail", "InvoiceLicense", "DirectProviderId", "Status", "CreatedAt", "UpdatedAt" },
        ["Products"] = new[] { "Id", "ProductNo", "Name", "Description", "Price", "PromoPrice", "PromoStart", "PromoEnd", "MaxDevices", "SortOrder", "PeriodDays", "Status", "CreatedAt", "UpdatedAt" },
        ["Orders"] = new[] { "Id", "OrderNo", "CustomerId", "ProductId", "OrderType", "Amount", "ProviderId", "CommissionRateSnapshot", "PayStatus", "PayChannel", "TradeNo", "PayTime", "RefundTime", "CommissionCalculated", "ExpiresAt", "InvoiceApplyId", "Hidden", "CreatedAt", "UpdatedAt" },
        ["Payments"] = new[] { "Id", "OrderId", "Channel", "TradeNo", "Amount", "CreatedAt" },
        ["SubscriptionAuths"] = new[] { "Id", "CustomerId", "ProductId", "OrderId", "AuthStartTime", "AuthEndTime", "Status", "GrantType", "GrantBy", "GrantReason", "DeviceLimit", "CreatedAt", "UpdatedAt" },
        ["AccountDevices"] = new[] { "Id", "AccountId", "DeviceId", "DisplayName", "Note", "FirstSeenAt", "LastSeenAt", "WanIp", "TaskStatus", "Status", "KickedAt" },
        ["Providers"] = new[] { "Id", "ProviderNo", "AccountId", "Name", "LevelId", "Status", "EffectiveAt", "CreatedAt", "UpdatedAt" },
        ["Commissions"] = new[] { "Id", "OrderId", "ProviderId", "LevelId", "CommissionRateSnapshot", "EntryType", "Amount", "Settled", "SettlementId", "CreatedAt" },
        ["Settlements"] = new[] { "Id", "SettlementNo", "ProviderId", "PeriodStart", "PeriodEnd", "Amount", "Status", "InvoiceFile", "PayVoucherNo", "PaidAt", "CreatedAt", "UpdatedAt" },
        ["InvoiceApplies"] = new[] { "Id", "ApplyNo", "CustomerId", "LicenseFile", "Amount", "TitleName", "TaxNo", "Email", "Issuer", "Status", "CreatedAt", "UpdatedAt" }
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
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff", CultureInfo.InvariantCulture);
        var backupPath = options.CreateBackup
            ? Path.Combine(backupDirectory, Path.GetFileNameWithoutExtension(sourcePath) + "-" + stamp + ".db")
            : "";
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

        try
        {
            using var source = OpenReadOnly(sourcePath);
            result.SchemaVersion = SchemaVersion(source);
            AddIntegrity(result, source, "source.integrity");

            if (options.CreateBackup)
            {
                var requiredBytes = RequiredBackupBytes(source);
                var availableBytes = options.AvailableBytesProvider?.Invoke(backupDirectory)
                    ?? AvailableBytes(backupDirectory);
                var hasSpace = availableBytes >= requiredBytes;
                AddCheck(result, "backup.space", hasSpace ? "pass" : "fail",
                    hasSpace ? "备份空间充足。" : "备份空间不足，所需字节数: " + requiredBytes, requiredBytes);
                if (hasSpace)
                {
                    CreateBackup(source, backupPath);
                    AddCheck(result, "backup.created", "pass", "已创建一致备份。");
                }
            }

            var schema = ReadSchema(source);
            AddSchema(result, source, schema, options.RequireMigratedSchema);
            AddForeignKeyCheck(result, source);
            AddRowsAndRelations(result, source, schema);

            if (options.CreateBackup && File.Exists(backupPath))
            {
                using var backup = OpenReadOnly(backupPath);
                AddIntegrity(result, backup, "backup.integrity");
                AddBackupComparison(result, source, backup, result.TableCounts);
            }

            AddExpectedComparison(result, options);
        }
        catch (Exception ex)
        {
            AddCheck(result, "preflight.exception", "fail", ex.GetType().Name + ": " + ex.Message);
        }

        if (options.CreateBackup)
        {
            AddCheck(result, "backup.sha256", File.Exists(backupPath) ? "pass" : "fail",
                File.Exists(backupPath) ? Hash(backupPath) : "备份不存在。");
        }

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

    private static long RequiredBackupBytes(SqliteConnection source)
    {
        var pageCount = ScalarLong(source, "PRAGMA page_count;");
        var pageSize = ScalarLong(source, "PRAGMA page_size;");
        var databaseBytes = Math.Max(0, pageCount * pageSize);
        return Math.Max(databaseBytes * 2, 1024L * 1024L);
    }

    private static long AvailableBytes(string directory)
    {
        var root = Path.GetPathRoot(Path.GetFullPath(directory));
        if (string.IsNullOrWhiteSpace(root)) return 0;
        return new DriveInfo(root).AvailableFreeSpace;
    }

    private static void AddIntegrity(PreflightResult result, SqliteConnection connection, string code)
    {
        var messages = ScalarStrings(connection, "PRAGMA integrity_check;")
            .Where(x => !string.Equals(x, "ok", StringComparison.OrdinalIgnoreCase))
            .ToList();
        AddCheck(result, code, messages.Count == 0 ? "pass" : "fail",
            messages.Count == 0 ? "完整性检查通过。" : string.Join(" | ", messages));
    }

    private static SchemaSnapshot ReadSchema(SqliteConnection connection)
    {
        var tables = ScalarStrings(connection, "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%';")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var columns = tables.ToDictionary(
            table => table,
            table => TableColumns(connection, table),
            StringComparer.OrdinalIgnoreCase);
        return new SchemaSnapshot(tables, columns);
    }

    private static void AddSchema(
        PreflightResult result,
        SqliteConnection connection,
        SchemaSnapshot schema,
        bool requireMigratedSchema)
    {
        var missingTables = ExpectedTables.Where(x => !schema.Tables.Contains(x)).ToArray();
        AddCheck(result, "schema.tables", missingTables.Length == 0 ? "pass" : "fail",
            missingTables.Length == 0 ? "预期业务表齐全。" : "缺少表: " + string.Join(",", missingTables),
            missingTables.Length);

        foreach (var table in ExpectedColumns.Keys)
        {
            if (!schema.Tables.Contains(table)) continue;

            var expected = ExpectedColumns[table];
            var actual = schema.Columns[table];
            var missing = expected.Where(x => !actual.Contains(x)).ToArray();
            AddCheck(result, "schema.columns." + table.ToLowerInvariant(),
                missing.Length == 0 ? "pass" : "fail",
                missing.Length == 0 ? "关键列齐全。" : "缺少列: " + string.Join(",", missing),
                missing.Length);
        }

        var hasSchemaVersions = schema.Tables.Contains("SchemaVersions");
        AddCheck(result, "schema.migrations", hasSchemaVersions || !requireMigratedSchema ? "pass" : "fail",
            hasSchemaVersions ? "迁移记录表存在。" : (requireMigratedSchema ? "缺少迁移记录表。" : "待首次迁移。"));
        AddUniqueIndexChecks(result, connection, schema);
        AddSemanticFingerprints(result, connection, schema);
    }

    private static void AddUniqueIndexChecks(
        PreflightResult result,
        SqliteConnection connection,
        SchemaSnapshot schema)
    {
        foreach (var pair in ExpectedUniqueIndexes)
        {
            if (!schema.Tables.Contains(pair.Key)) continue;

            var actual = UniqueIndexes(connection, pair.Key);
            var missing = pair.Value
                .Where(expected => !actual.Any(columns => columns.SequenceEqual(expected, StringComparer.OrdinalIgnoreCase)))
                .Select(columns => string.Join("+", columns))
                .ToArray();
            AddCheck(result, "schema.unique_index." + pair.Key.ToLowerInvariant(),
                missing.Length == 0 ? "pass" : "fail",
                missing.Length == 0 ? "唯一索引齐全。" : "缺少唯一索引: " + string.Join(",", missing),
                missing.Length);
        }
    }

    private static List<string[]> UniqueIndexes(SqliteConnection connection, string table)
    {
        var indexes = new List<string[]>();
        using var list = connection.CreateCommand();
        list.CommandText = "PRAGMA index_list(" + Quote(table) + ");";
        using var reader = list.ExecuteReader();
        var uniqueNames = new List<string>();
        while (reader.Read())
        {
            if (Convert.ToInt32(reader.GetValue(2)) == 1)
                uniqueNames.Add(reader.GetString(1));
        }

        reader.Close();
        foreach (var name in uniqueNames)
        {
            using var info = connection.CreateCommand();
            info.CommandText = "PRAGMA index_info(" + Quote(name) + ");";
            using var columns = info.ExecuteReader();
            var names = new List<(int Order, string Name)>();
            while (columns.Read())
                names.Add((Convert.ToInt32(columns.GetValue(0)), columns.GetString(2)));
            indexes.Add(names.OrderBy(x => x.Order).Select(x => x.Name).ToArray());
        }

        return indexes;
    }

    private static void AddRowsAndRelations(
        PreflightResult result,
        SqliteConnection connection,
        SchemaSnapshot schema)
    {
        foreach (var table in ExpectedTables)
        {
            if (schema.Tables.Contains(table))
                result.TableCounts[table] = ScalarLong(connection, "SELECT COUNT(*) FROM " + Quote(table) + ";");
        }

        AddLogicalCheck(result, connection, schema, "relation.customer_account",
            "SELECT COUNT(*) FROM Customers c LEFT JOIN Accounts a ON a.Id=c.AccountId WHERE a.Id IS NULL;",
            "Customers", "Accounts");
        AddLogicalCheck(result, connection, schema, "relation.order_customer",
            "SELECT COUNT(*) FROM Orders o LEFT JOIN Customers c ON c.Id=o.CustomerId WHERE c.Id IS NULL;",
            "Orders", "Customers");
        AddLogicalCheck(result, connection, schema, "relation.order_product",
            "SELECT COUNT(*) FROM Orders o LEFT JOIN Products p ON p.Id=o.ProductId WHERE p.Id IS NULL;",
            "Orders", "Products");
        AddLogicalCheck(result, connection, schema, "relation.auth_customer",
            "SELECT COUNT(*) FROM SubscriptionAuths a LEFT JOIN Customers c ON c.Id=a.CustomerId WHERE c.Id IS NULL;",
            "SubscriptionAuths", "Customers");
        AddLogicalCheck(result, connection, schema, "relation.auth_product",
            "SELECT COUNT(*) FROM SubscriptionAuths a LEFT JOIN Products p ON p.Id=a.ProductId WHERE p.Id IS NULL;",
            "SubscriptionAuths", "Products");
        AddLogicalCheck(result, connection, schema, "relation.payment_order",
            "SELECT COUNT(*) FROM Payments p LEFT JOIN Orders o ON o.Id=p.OrderId WHERE o.Id IS NULL;",
            "Payments", "Orders");
        AddLogicalCheck(result, connection, schema, "relation.device_account",
            "SELECT COUNT(*) FROM AccountDevices d LEFT JOIN Accounts a ON a.Id=d.AccountId WHERE a.Id IS NULL;",
            "AccountDevices", "Accounts");
        AddLogicalCheck(result, connection, schema, "relation.commission_order",
            "SELECT COUNT(*) FROM Commissions c LEFT JOIN Orders o ON o.Id=c.OrderId WHERE o.Id IS NULL;",
            "Commissions", "Orders");
        AddLogicalCheck(result, connection, schema, "relation.commission_provider",
            "SELECT COUNT(*) FROM Commissions c LEFT JOIN Providers p ON p.Id=c.ProviderId WHERE p.Id IS NULL;",
            "Commissions", "Providers");
        AddLogicalCheck(result, connection, schema, "relation.commission_level",
            "SELECT COUNT(*) FROM Commissions c LEFT JOIN ProviderLevels l ON l.Id=c.LevelId WHERE l.Id IS NULL;",
            "Commissions", "ProviderLevels");
        AddLogicalCheck(result, connection, schema, "relation.settlement_provider",
            "SELECT COUNT(*) FROM Settlements s LEFT JOIN Providers p ON p.Id=s.ProviderId WHERE p.Id IS NULL;",
            "Settlements", "Providers");
        AddLogicalCheck(result, connection, schema, "relation.invoice_customer",
            "SELECT COUNT(*) FROM InvoiceApplies i LEFT JOIN Customers c ON c.Id=i.CustomerId WHERE c.Id IS NULL;",
            "InvoiceApplies", "Customers");
        AddLogicalCheck(result, connection, schema, "business.paid_order_amount",
            "SELECT COUNT(*) FROM Orders WHERE PayStatus=1 AND CAST(Amount AS REAL)<=0;",
            "Orders");
        AddLogicalCheck(result, connection, schema, "business.paid_order_paytime",
            "SELECT COUNT(*) FROM Orders WHERE PayStatus=1 AND PayTime IS NULL;",
            true, "Orders");
        AddLogicalCheck(result, connection, schema, "business.active_auth_product",
            "SELECT COUNT(*) FROM SubscriptionAuths a LEFT JOIN Products p ON p.Id=a.ProductId WHERE a.Status=1 AND p.Id IS NULL;",
            "SubscriptionAuths", "Products");
        AddLogicalCheck(result, connection, schema, "business.active_device_account",
            "SELECT COUNT(*) FROM AccountDevices d LEFT JOIN Accounts a ON a.Id=d.AccountId WHERE d.Status=1 AND a.Id IS NULL;",
            "AccountDevices", "Accounts");
    }

    private static void AddSemanticFingerprints(
        PreflightResult result,
        SqliteConnection connection,
        SchemaSnapshot schema)
    {
        foreach (var pair in SemanticColumns.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (!schema.Tables.Contains(pair.Key)
                || pair.Value.Any(column => !schema.Columns[pair.Key].Contains(column)))
            {
                continue;
            }

            result.TableFingerprints[pair.Key] = FingerprintTable(connection, pair.Key, pair.Value);
        }

        using var combined = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var pair in result.TableFingerprints.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            Append(combined, pair.Key);
            Append(combined, pair.Value);
        }

        result.SemanticFingerprint = Convert.ToHexString(combined.GetHashAndReset()).ToLowerInvariant();
        AddCheck(result, "business.semantics", result.TableFingerprints.Count > 0 ? "pass" : "fail",
            result.TableFingerprints.Count > 0 ? "已生成业务语义指纹。" : "无法生成业务语义指纹。");
    }

    private static string FingerprintTable(SqliteConnection connection, string table, string[] columns)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT " + string.Join(",", columns.Select(Quote))
            + " FROM " + Quote(table) + " ORDER BY " + Quote("Id") + ";";
        using var reader = command.ExecuteReader();
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        while (reader.Read())
        {
            for (var i = 0; i < reader.FieldCount; i++)
            {
                if (reader.IsDBNull(i))
                {
                    hash.AppendData(new byte[] { 0 });
                    continue;
                }

                var value = reader.GetValue(i);
                var bytes = value is byte[] blob
                    ? blob
                    : Encoding.UTF8.GetBytes(Convert.ToString(value, CultureInfo.InvariantCulture) ?? "");
                hash.AppendData(new byte[] { 1 });
                hash.AppendData(BitConverter.GetBytes(bytes.Length));
                hash.AppendData(bytes);
            }

            hash.AppendData(new byte[] { 255 });
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static void Append(IncrementalHash hash, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        hash.AppendData(BitConverter.GetBytes(bytes.Length));
        hash.AppendData(bytes);
    }

    private static void AddBackupComparison(
        PreflightResult result,
        SqliteConnection source,
        SqliteConnection backup,
        Dictionary<string, long> sourceCounts)
    {
        var differences = new List<string>();
        foreach (var pair in sourceCounts)
        {
            var backupCount = ScalarLong(backup, "SELECT COUNT(*) FROM " + Quote(pair.Key) + ";");
            if (backupCount != pair.Value)
                differences.Add(pair.Key + ":" + pair.Value + "->" + backupCount);
        }

        AddCheck(result, "backup.counts", differences.Count == 0 ? "pass" : "fail",
            differences.Count == 0 ? "备份行数与源快照一致。" : "备份行数不一致: " + string.Join(",", differences),
            differences.Count);

        var backupSchema = ReadSchema(backup);
        var backupResult = new PreflightResult();
        AddSemanticFingerprints(backupResult, backup, backupSchema);
        var same = string.Equals(result.SemanticFingerprint, backupResult.SemanticFingerprint, StringComparison.Ordinal);
        AddCheck(result, "backup.semantics", same ? "pass" : "fail",
            same ? "备份业务语义与源快照一致。" : "备份业务语义与源快照不一致。");
    }

    private static void AddExpectedComparison(PreflightResult result, PreflightOptions options)
    {
        if (options.ExpectedTableCounts != null)
        {
            var differences = new List<string>();
            foreach (var pair in options.ExpectedTableCounts)
            {
                if (!result.TableCounts.TryGetValue(pair.Key, out var count))
                {
                    differences.Add(pair.Key + ":" + pair.Value + "->missing");
                }
                else if (count != pair.Value)
                {
                    differences.Add(pair.Key + ":" + pair.Value + "->" + count);
                }
            }

            AddCheck(result, "migration.counts", differences.Count == 0 ? "pass" : "fail",
                differences.Count == 0 ? "迁移前后业务行数一致。" : "迁移前后行数不一致: " + string.Join(",", differences),
                differences.Count);
        }

        if (!string.IsNullOrWhiteSpace(options.ExpectedSemanticFingerprint))
        {
            var same = string.Equals(
                options.ExpectedSemanticFingerprint,
                result.SemanticFingerprint,
                StringComparison.Ordinal);
            AddCheck(result, "migration.semantics", same ? "pass" : "fail",
                same ? "迁移前后业务语义一致。" : "迁移前后业务语义不一致。");
        }
    }

    private static void AddLogicalCheck(
        PreflightResult result,
        SqliteConnection connection,
        SchemaSnapshot schema,
        string code,
        string sql,
        params string[] requiredTables)
    {
        AddLogicalCheck(result, connection, schema, code, sql, false, requiredTables);
    }

    private static void AddLogicalCheck(
        PreflightResult result,
        SqliteConnection connection,
        SchemaSnapshot schema,
        string code,
        string sql,
        bool warning,
        params string[] requiredTables)
    {
        if (requiredTables.Any(table => !schema.Tables.Contains(table))) return;

        try
        {
            var count = ScalarLong(connection, sql);
            AddCheck(result, code, count == 0 ? "pass" : (warning ? "warn" : "fail"),
                count == 0 ? "检查通过。" : "发现 " + count + " 条异常。", count);
        }
        catch (SqliteException)
        {
            AddCheck(result, code, "fail", "缺少检查所需列。");
        }
    }

    private static void AddForeignKeyCheck(PreflightResult result, SqliteConnection connection)
    {
        var count = 0L;
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_key_check;";
        using var reader = command.ExecuteReader();
        while (reader.Read()) count++;

        AddCheck(result, "schema.foreign_keys", count == 0 ? "pass" : "fail",
            count == 0 ? "SQLite 外键检查通过。" : "发现 " + count + " 条外键异常。", count);
    }

    private static int? SchemaVersion(SqliteConnection connection)
    {
        var hasTable = ScalarLong(connection,
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='SchemaVersions';") == 1;
        return hasTable ? Convert.ToInt32(ScalarLong(connection, "SELECT COALESCE(MAX(Version),0) FROM SchemaVersions;")) : null;
    }

    private static void AddCheck(
        PreflightResult result,
        string code,
        string status,
        string message,
        long? count = null)
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
            values.Add(reader.IsDBNull(0) ? "" : Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture) ?? "");
        return values;
    }

    private static HashSet<string> TableColumns(SqliteConnection connection, string table)
    {
        var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(" + Quote(table) + ");";
        using var reader = command.ExecuteReader();
        while (reader.Read())
            values.Add(reader.GetString(1));
        return values;
    }

    private static long ScalarLong(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static string Quote(string identifier) => "\"" + identifier.Replace("\"", "\"\"") + "\"";

    private static string Hash(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private sealed record SchemaSnapshot(
        HashSet<string> Tables,
        Dictionary<string, HashSet<string>> Columns);
}
