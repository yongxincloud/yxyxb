using System.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace YxyXb.Api.Data;

public sealed class DatabaseStartupService
{
    public bool NeedsMigration(WebDb db)
    {
        return TryReadVersion(db).GetValueOrDefault() < 1;
    }

    public int? TryReadVersion(WebDb db)
    {
        try
        {
            return CurrentVersionOrZero(db);
        }
        catch (SqliteException)
        {
            return null;
        }
    }

    public int Initialize(WebDb db, Action<string>? onStep = null)
    {
        db.Database.EnsureCreated();
        onStep?.Invoke("schema.ensure-created");
        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "SchemaVersions" (
              "Version" INTEGER NOT NULL CONSTRAINT "PK_SchemaVersions" PRIMARY KEY,
              "AppliedAtUtc" TEXT NOT NULL
            );
            """);
        onStep?.Invoke("schema.versions.ready");

        var current = CurrentVersion(db);
        if (current < 1)
        {
            onStep?.Invoke("schema.version.1.started");
            ApplyVersion1(db);
            onStep?.Invoke("schema.version.1.applied");
            db.Database.ExecuteSqlRaw(
                "INSERT INTO \"SchemaVersions\" (\"Version\", \"AppliedAtUtc\") VALUES (1, '" +
                DateTime.UtcNow.ToString("o") + "');");
            onStep?.Invoke("schema.version.1.recorded");
            current = 1;
        }

        return current;
    }

    private static int CurrentVersion(WebDb db)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open) connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COALESCE(MAX(\"Version\"), 0) FROM \"SchemaVersions\";";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static int CurrentVersionOrZero(WebDb db)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open) connection.Open();
        using var table = connection.CreateCommand();
        table.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='SchemaVersions';";
        if (Convert.ToInt32(table.ExecuteScalar()) == 0) return 0;
        return CurrentVersion(db);
    }

    private static void ApplyVersion1(WebDb db)
    {
        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "Accounts" (
              "Id" INTEGER NOT NULL CONSTRAINT "PK_Accounts" PRIMARY KEY AUTOINCREMENT,
              "Email" TEXT NOT NULL, "PasswordHash" TEXT NOT NULL, "Phone" TEXT NULL,
              "Status" INTEGER NOT NULL, "LastLoginAt" TEXT NULL, "CreatedAt" TEXT NOT NULL,
              "UpdatedAt" TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Accounts_Email" ON "Accounts" ("Email");
            CREATE TABLE IF NOT EXISTS "EmailCodes" (
              "Id" INTEGER NOT NULL CONSTRAINT "PK_EmailCodes" PRIMARY KEY AUTOINCREMENT,
              "Email" TEXT NOT NULL, "Code" TEXT NOT NULL, "Scene" TEXT NOT NULL,
              "Used" INTEGER NOT NULL, "ExpiresAt" TEXT NOT NULL, "ReqIp" TEXT NULL,
              "CreatedAt" TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS "LoginLogs" (
              "Id" INTEGER NOT NULL CONSTRAINT "PK_LoginLogs" PRIMARY KEY AUTOINCREMENT,
              "AccountId" INTEGER NULL, "Email" TEXT NULL, "Way" TEXT NOT NULL,
              "DeviceId" TEXT NULL, "Ip" TEXT NULL, "Success" INTEGER NOT NULL,
              "Remark" TEXT NULL, "CreatedAt" TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS "Customers" (
              "Id" INTEGER NOT NULL CONSTRAINT "PK_Customers" PRIMARY KEY AUTOINCREMENT,
              "CustomerNo" TEXT NOT NULL, "AccountId" INTEGER NOT NULL, "Name" TEXT NULL,
              "Nickname" TEXT NULL, "CompanyName" TEXT NULL, "InvoiceTitle" TEXT NULL,
              "InvoiceTaxNo" TEXT NULL, "InvoiceEmail" TEXT NULL, "InvoiceLicense" TEXT NULL,
              "DirectProviderId" INTEGER NULL, "Status" INTEGER NOT NULL,
              "CreatedAt" TEXT NOT NULL, "UpdatedAt" TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Customers_AccountId" ON "Customers" ("AccountId");
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Customers_CustomerNo" ON "Customers" ("CustomerNo");
            CREATE TABLE IF NOT EXISTS "Products" (
              "Id" INTEGER NOT NULL CONSTRAINT "PK_Products" PRIMARY KEY AUTOINCREMENT,
              "ProductNo" TEXT NOT NULL, "Name" TEXT NOT NULL, "Description" TEXT NULL,
              "Price" TEXT NOT NULL, "PeriodDays" INTEGER NOT NULL, "Status" INTEGER NOT NULL,
              "CreatedAt" TEXT NOT NULL, "UpdatedAt" TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Products_ProductNo" ON "Products" ("ProductNo");
            CREATE TABLE IF NOT EXISTS "Orders" (
              "Id" INTEGER NOT NULL CONSTRAINT "PK_SubscriptionOrders" PRIMARY KEY AUTOINCREMENT,
              "OrderNo" TEXT NOT NULL, "CustomerId" INTEGER NOT NULL, "ProductId" INTEGER NOT NULL,
              "OrderType" INTEGER NOT NULL, "Amount" TEXT NOT NULL, "ProviderId" INTEGER NULL,
              "CommissionRateSnapshot" TEXT NULL, "PayStatus" INTEGER NOT NULL, "PayChannel" TEXT NULL,
              "TradeNo" TEXT NULL, "PayTime" TEXT NULL, "RefundTime" TEXT NULL,
              "CommissionCalculated" INTEGER NOT NULL, "ExpiresAt" TEXT NULL,
              "CreatedAt" TEXT NOT NULL, "UpdatedAt" TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Orders_OrderNo" ON "Orders" ("OrderNo");
            CREATE TABLE IF NOT EXISTS "Payments" (
              "Id" INTEGER NOT NULL CONSTRAINT "PK_PaymentRecords" PRIMARY KEY AUTOINCREMENT,
              "OrderId" INTEGER NOT NULL, "Channel" TEXT NOT NULL, "TradeNo" TEXT NOT NULL,
              "Amount" TEXT NOT NULL, "CreatedAt" TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Payments_TradeNo" ON "Payments" ("TradeNo");
            CREATE TABLE IF NOT EXISTS "Configs" (
              "Id" INTEGER NOT NULL CONSTRAINT "PK_SystemConfigRows" PRIMARY KEY AUTOINCREMENT,
              "CfgKey" TEXT NOT NULL, "CfgValue" TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Configs_CfgKey" ON "Configs" ("CfgKey");
            CREATE TABLE IF NOT EXISTS "AccountDevices" (
              "Id" INTEGER NOT NULL CONSTRAINT "PK_AccountDevices" PRIMARY KEY AUTOINCREMENT,
              "AccountId" INTEGER NOT NULL, "DeviceId" TEXT NOT NULL, "DisplayName" TEXT NULL,
              "Note" TEXT NULL, "FirstSeenAt" TEXT NOT NULL, "LastSeenAt" TEXT NOT NULL,
              "Status" INTEGER NOT NULL, "KickedAt" TEXT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_AccountDevices_Account_Device" ON "AccountDevices" ("AccountId", "DeviceId");
            CREATE TABLE IF NOT EXISTS "AuthTokens" (
              "Id" INTEGER NOT NULL CONSTRAINT "PK_AuthTokens" PRIMARY KEY AUTOINCREMENT,
              "AccountId" INTEGER NOT NULL, "TokenHash" TEXT NOT NULL, "DeviceId" TEXT NULL,
              "IssuedAt" TEXT NOT NULL, "ExpiresAt" TEXT NOT NULL, "Revoked" INTEGER NOT NULL,
              "RevokedAt" TEXT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_AuthTokens_TokenHash" ON "AuthTokens" ("TokenHash");
            CREATE TABLE IF NOT EXISTS "SubscriptionAuths" (
              "Id" INTEGER NOT NULL CONSTRAINT "PK_SubscriptionAuths" PRIMARY KEY AUTOINCREMENT,
              "CustomerId" INTEGER NOT NULL, "ProductId" INTEGER NOT NULL, "OrderId" INTEGER NULL,
              "AuthStartTime" TEXT NOT NULL, "AuthEndTime" TEXT NOT NULL, "Status" INTEGER NOT NULL,
              "GrantType" INTEGER NOT NULL, "GrantBy" INTEGER NULL, "GrantReason" TEXT NULL,
              "DeviceLimit" INTEGER NULL, "CreatedAt" TEXT NOT NULL, "UpdatedAt" TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS "AdminUsers" (
              "Id" INTEGER NOT NULL CONSTRAINT "PK_AdminUsers" PRIMARY KEY AUTOINCREMENT,
              "Username" TEXT NOT NULL, "PasswordHash" TEXT NOT NULL, "Email" TEXT NULL,
              "CreatedAt" TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_AdminUsers_Username" ON "AdminUsers" ("Username");
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_AdminUsers_Email" ON "AdminUsers" ("Email");
            CREATE TABLE IF NOT EXISTS "Commissions" (
              "Id" INTEGER NOT NULL CONSTRAINT "PK_Commissions" PRIMARY KEY AUTOINCREMENT,
              "OrderId" INTEGER NOT NULL, "ProviderId" INTEGER NOT NULL, "LevelId" INTEGER NOT NULL,
              "CommissionRateSnapshot" TEXT NOT NULL, "EntryType" INTEGER NOT NULL, "Amount" TEXT NOT NULL,
              "Settled" INTEGER NOT NULL, "SettlementId" INTEGER NULL, "CreatedAt" TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Commissions_OrderId_ProviderId_EntryType" ON "Commissions" ("OrderId", "ProviderId", "EntryType");
            CREATE TABLE IF NOT EXISTS "Settlements" (
              "Id" INTEGER NOT NULL CONSTRAINT "PK_Settlements" PRIMARY KEY AUTOINCREMENT,
              "SettlementNo" TEXT NOT NULL, "ProviderId" INTEGER NOT NULL, "PeriodStart" TEXT NOT NULL,
              "PeriodEnd" TEXT NOT NULL, "Amount" TEXT NOT NULL, "Status" INTEGER NOT NULL,
              "InvoiceFile" TEXT NULL, "PayVoucherNo" TEXT NULL, "PaidAt" TEXT NULL,
              "CreatedAt" TEXT NOT NULL, "UpdatedAt" TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Settlements_SettlementNo" ON "Settlements" ("SettlementNo");
            CREATE TABLE IF NOT EXISTS "OperationLogs" (
              "Id" INTEGER NOT NULL CONSTRAINT "PK_OperationLogs" PRIMARY KEY AUTOINCREMENT,
              "OperatorType" TEXT NOT NULL, "Module" TEXT NOT NULL, "Action" TEXT NOT NULL,
              "BeforeValue" TEXT NULL, "AfterValue" TEXT NULL, "CreatedAt" TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS "Messages" (
              "Id" INTEGER NOT NULL CONSTRAINT "PK_Messages" PRIMARY KEY AUTOINCREMENT,
              "AccountId" INTEGER NOT NULL, "Title" TEXT NOT NULL, "Content" TEXT NULL,
              "IsRead" INTEGER NOT NULL, "CreatedAt" TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS "ProviderApplies" (
              "Id" INTEGER NOT NULL CONSTRAINT "PK_ProviderApplies" PRIMARY KEY AUTOINCREMENT,
              "AccountId" INTEGER NOT NULL, "CompanyName" TEXT NOT NULL, "TaxNo" TEXT NOT NULL,
              "LicenseFile" TEXT NOT NULL, "ContactName" TEXT NOT NULL, "ContactEmail" TEXT NOT NULL,
              "ContactPhone" TEXT NOT NULL, "Status" INTEGER NOT NULL, "CreatedAt" TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS "InvoiceApplies" (
              "Id" INTEGER NOT NULL CONSTRAINT "PK_InvoiceApplies" PRIMARY KEY AUTOINCREMENT,
              "ApplyNo" TEXT NOT NULL, "CustomerId" INTEGER NOT NULL, "LicenseFile" TEXT NULL,
              "Amount" TEXT NOT NULL, "TitleName" TEXT NULL, "TaxNo" TEXT NULL, "Email" TEXT NULL,
              "Issuer" INTEGER NOT NULL, "Status" INTEGER NOT NULL, "CreatedAt" TEXT NOT NULL,
              "UpdatedAt" TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_InvoiceApplies_ApplyNo" ON "InvoiceApplies" ("ApplyNo");
            CREATE TABLE IF NOT EXISTS "ProviderLevels" (
              "Id" INTEGER NOT NULL CONSTRAINT "PK_ProviderLevels" PRIMARY KEY AUTOINCREMENT,
              "Name" TEXT NOT NULL, "CommissionRate" TEXT NOT NULL, "SortNo" INTEGER NOT NULL,
              "Status" INTEGER NOT NULL, "CreatedAt" TEXT NOT NULL, "UpdatedAt" TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_ProviderLevels_Name" ON "ProviderLevels" ("Name");
            CREATE TABLE IF NOT EXISTS "Providers" (
              "Id" INTEGER NOT NULL CONSTRAINT "PK_Providers" PRIMARY KEY AUTOINCREMENT,
              "ProviderNo" TEXT NOT NULL, "AccountId" INTEGER NOT NULL, "Name" TEXT NOT NULL,
              "LevelId" INTEGER NOT NULL, "Status" INTEGER NOT NULL, "EffectiveAt" TEXT NULL,
              "CreatedAt" TEXT NOT NULL, "UpdatedAt" TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Providers_ProviderNo" ON "Providers" ("ProviderNo");
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Providers_AccountId" ON "Providers" ("AccountId");
            """);

        AddColumn(db, "Products", "PromoPrice", "TEXT NULL");
        AddColumn(db, "Products", "PromoStart", "TEXT NULL");
        AddColumn(db, "Products", "PromoEnd", "TEXT NULL");
        AddColumn(db, "Products", "MaxDevices", "INTEGER NOT NULL DEFAULT 10");
        AddColumn(db, "Products", "SortOrder", "INTEGER NOT NULL DEFAULT 0");
        AddColumn(db, "AccountDevices", "Note", "TEXT NULL");
        AddColumn(db, "AccountDevices", "WanIp", "TEXT NULL");
        AddColumn(db, "AccountDevices", "TaskStatus", "TEXT NULL");
        AddColumn(db, "SubscriptionAuths", "DeviceLimit", "INTEGER NULL");
        AddColumn(db, "Orders", "InvoiceApplyId", "INTEGER NULL");
        AddColumn(db, "Orders", "Hidden", "INTEGER NOT NULL DEFAULT 0");
        AddColumn(db, "Customers", "Nickname", "TEXT NULL");
        AddColumn(db, "Customers", "CompanyName", "TEXT NULL");
        AddColumn(db, "Customers", "InvoiceTitle", "TEXT NULL");
        AddColumn(db, "Customers", "InvoiceTaxNo", "TEXT NULL");
        AddColumn(db, "Customers", "InvoiceEmail", "TEXT NULL");
        AddColumn(db, "Customers", "InvoiceLicense", "TEXT NULL");
        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "ExpiryNoticeLogs" (
              "Id" INTEGER NOT NULL CONSTRAINT "PK_ExpiryNoticeLogs" PRIMARY KEY AUTOINCREMENT,
              "CustomerId" INTEGER NOT NULL, "SentDay" TEXT NOT NULL, "CreatedAt" TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_ExpiryNoticeLogs_CustomerId_SentDay" ON "ExpiryNoticeLogs" ("CustomerId", "SentDay");
            """);
        AddColumn(db, "AdminUsers", "Email", "TEXT NULL");
        db.Database.ExecuteSqlRaw("""CREATE UNIQUE INDEX IF NOT EXISTS "IX_AdminUsers_Email" ON "AdminUsers" ("Email");""");
    }

    private static void AddColumn(WebDb db, string table, string column, string definition)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open) connection.Open();
        using var check = connection.CreateCommand();
        check.CommandText = "PRAGMA table_info(\"" + table + "\")";
        using var reader = check.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase)) return;
        }

        reader.Close();
        db.Database.ExecuteSqlRaw("ALTER TABLE \"" + table + "\" ADD COLUMN \"" + column + "\" " + definition);
    }
}
