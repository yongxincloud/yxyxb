using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using YxyXb.Api.Controllers;
using YxyXb.Api.Data;
using YxyXb.Api.Services;

var builder = WebApplication.CreateBuilder(args);
if (!builder.Environment.IsEnvironment("Testing"))
    builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
builder.Services.AddMemoryCache();
builder.Services.AddControllers();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = "yxy.auth";
        o.Cookie.HttpOnly = true;
        o.Events.OnRedirectToLogin = ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            ctx.Response.ContentType = "application/json";
            return ctx.Response.WriteAsync("{\"ok\":false,\"error\":\"未登录\"}");
        };
    })
    .AddCookie(VendorController.VendorScheme, o =>
    {
        o.Cookie.Name = "yxy.vendor";
        o.Cookie.HttpOnly = true;
        o.Events.OnRedirectToLogin = ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            ctx.Response.ContentType = "application/json";
            return ctx.Response.WriteAsync("{\"ok\":false,\"error\":\"未登录\"}");
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddScoped<EmailCodeService>();
builder.Services.AddScoped<CaptchaService>();
builder.Services.AddScoped<AccountService>();
builder.Services.AddScoped<DeviceRegistryService>();
builder.Services.AddScoped<ClientAuthService>();
builder.Services.AddHttpClient("alipay", client => client.Timeout = TimeSpan.FromSeconds(20));
builder.Services.AddScoped<IAlipayGateway, AlipayGateway>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddSingleton<FakeEmailSender>();
builder.Services.AddScoped<MailSettingsService>();
builder.Services.AddScoped<IEmailSender, RoutingEmailSender>();
builder.Services.AddHostedService<SiteMaintenance>();
builder.Services.AddDbContext<WebDb>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Web")));
var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<WebDb>();
    db.Database.EnsureCreated();
    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS "Products" (
          "Id" INTEGER NOT NULL CONSTRAINT "PK_Products" PRIMARY KEY AUTOINCREMENT,
          "ProductNo" TEXT NOT NULL,
          "Name" TEXT NOT NULL,
          "Description" TEXT NULL,
          "Price" TEXT NOT NULL,
          "PeriodDays" INTEGER NOT NULL,
          "Status" INTEGER NOT NULL,
          "CreatedAt" TEXT NOT NULL,
          "UpdatedAt" TEXT NOT NULL
        );
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_Products_ProductNo" ON "Products" ("ProductNo");
        CREATE TABLE IF NOT EXISTS "Orders" (
          "Id" INTEGER NOT NULL CONSTRAINT "PK_SubscriptionOrders" PRIMARY KEY AUTOINCREMENT,
          "OrderNo" TEXT NOT NULL,
          "CustomerId" INTEGER NOT NULL,
          "ProductId" INTEGER NOT NULL,
          "OrderType" INTEGER NOT NULL,
          "Amount" TEXT NOT NULL,
          "ProviderId" INTEGER NULL,
          "CommissionRateSnapshot" TEXT NULL,
          "PayStatus" INTEGER NOT NULL,
          "PayChannel" TEXT NULL,
          "TradeNo" TEXT NULL,
          "PayTime" TEXT NULL,
          "RefundTime" TEXT NULL,
          "CommissionCalculated" INTEGER NOT NULL,
          "ExpiresAt" TEXT NULL,
          "CreatedAt" TEXT NOT NULL,
          "UpdatedAt" TEXT NOT NULL
        );
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_Orders_OrderNo" ON "Orders" ("OrderNo");
        CREATE TABLE IF NOT EXISTS "Payments" (
          "Id" INTEGER NOT NULL CONSTRAINT "PK_PaymentRecords" PRIMARY KEY AUTOINCREMENT,
          "OrderId" INTEGER NOT NULL,
          "Channel" TEXT NOT NULL,
          "TradeNo" TEXT NOT NULL,
          "Amount" TEXT NOT NULL,
          "CreatedAt" TEXT NOT NULL
        );
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_Payments_TradeNo" ON "Payments" ("TradeNo");
        CREATE TABLE IF NOT EXISTS "Configs" (
          "Id" INTEGER NOT NULL CONSTRAINT "PK_SystemConfigRows" PRIMARY KEY AUTOINCREMENT,
          "CfgKey" TEXT NOT NULL,
          "CfgValue" TEXT NOT NULL
        );
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_Configs_CfgKey" ON "Configs" ("CfgKey");
        """);
    AddColumn(db, "Products", "PromoPrice", "TEXT NULL");
    AddColumn(db, "Products", "PromoStart", "TEXT NULL");
    AddColumn(db, "Products", "PromoEnd", "TEXT NULL");
    AddColumn(db, "Products", "MaxDevices", "INTEGER NOT NULL DEFAULT 10");
    AddColumn(db, "Products", "SortOrder", "INTEGER NOT NULL DEFAULT 0");
    await scope.ServiceProvider.GetRequiredService<OrderService>().EnsureCatalogAsync();
    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS "AccountDevices" (
          "Id" INTEGER NOT NULL CONSTRAINT "PK_AccountDevices" PRIMARY KEY AUTOINCREMENT,
          "AccountId" INTEGER NOT NULL,
          "DeviceId" TEXT NOT NULL,
          "DisplayName" TEXT NULL,
          "FirstSeenAt" TEXT NOT NULL,
          "LastSeenAt" TEXT NOT NULL,
          "Status" INTEGER NOT NULL,
          "KickedAt" TEXT NULL
        );
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_AccountDevices_Account_Device" ON "AccountDevices" ("AccountId", "DeviceId");
        CREATE TABLE IF NOT EXISTS "AuthTokens" (
          "Id" INTEGER NOT NULL CONSTRAINT "PK_AuthTokens" PRIMARY KEY AUTOINCREMENT,
          "AccountId" INTEGER NOT NULL,
          "TokenHash" TEXT NOT NULL,
          "DeviceId" TEXT NULL,
          "IssuedAt" TEXT NOT NULL,
          "ExpiresAt" TEXT NOT NULL,
          "Revoked" INTEGER NOT NULL,
          "RevokedAt" TEXT NULL
        );
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_AuthTokens_TokenHash" ON "AuthTokens" ("TokenHash");
        CREATE TABLE IF NOT EXISTS "SubscriptionAuths" (
          "Id" INTEGER NOT NULL CONSTRAINT "PK_SubscriptionAuths" PRIMARY KEY AUTOINCREMENT,
          "CustomerId" INTEGER NOT NULL,
          "ProductId" INTEGER NOT NULL,
          "OrderId" INTEGER NULL,
          "AuthStartTime" TEXT NOT NULL,
          "AuthEndTime" TEXT NOT NULL,
          "Status" INTEGER NOT NULL,
          "GrantType" INTEGER NOT NULL,
          "GrantBy" INTEGER NULL,
          "GrantReason" TEXT NULL,
          "CreatedAt" TEXT NOT NULL,
          "UpdatedAt" TEXT NOT NULL
        );
        """);
    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS "AdminUsers" (
          "Id" INTEGER NOT NULL CONSTRAINT "PK_AdminUsers" PRIMARY KEY AUTOINCREMENT,
          "Username" TEXT NOT NULL,
          "PasswordHash" TEXT NOT NULL,
          "CreatedAt" TEXT NOT NULL
        );
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_AdminUsers_Username" ON "AdminUsers" ("Username");
        CREATE TABLE IF NOT EXISTS "Commissions" (
          "Id" INTEGER NOT NULL CONSTRAINT "PK_Commissions" PRIMARY KEY AUTOINCREMENT,
          "OrderId" INTEGER NOT NULL,
          "ProviderId" INTEGER NOT NULL,
          "LevelId" INTEGER NOT NULL,
          "CommissionRateSnapshot" TEXT NOT NULL,
          "EntryType" INTEGER NOT NULL,
          "Amount" TEXT NOT NULL,
          "Settled" INTEGER NOT NULL,
          "SettlementId" INTEGER NULL,
          "CreatedAt" TEXT NOT NULL
        );
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_Commissions_OrderId_ProviderId_EntryType" ON "Commissions" ("OrderId", "ProviderId", "EntryType");
        CREATE TABLE IF NOT EXISTS "Settlements" (
          "Id" INTEGER NOT NULL CONSTRAINT "PK_Settlements" PRIMARY KEY AUTOINCREMENT,
          "SettlementNo" TEXT NOT NULL,
          "ProviderId" INTEGER NOT NULL,
          "PeriodStart" TEXT NOT NULL,
          "PeriodEnd" TEXT NOT NULL,
          "Amount" TEXT NOT NULL,
          "Status" INTEGER NOT NULL,
          "InvoiceFile" TEXT NULL,
          "PayVoucherNo" TEXT NULL,
          "PaidAt" TEXT NULL,
          "CreatedAt" TEXT NOT NULL,
          "UpdatedAt" TEXT NOT NULL
        );
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_Settlements_SettlementNo" ON "Settlements" ("SettlementNo");
        CREATE TABLE IF NOT EXISTS "OperationLogs" (
          "Id" INTEGER NOT NULL CONSTRAINT "PK_OperationLogs" PRIMARY KEY AUTOINCREMENT,
          "OperatorType" TEXT NOT NULL,
          "Module" TEXT NOT NULL,
          "Action" TEXT NOT NULL,
          "BeforeValue" TEXT NULL,
          "AfterValue" TEXT NULL,
          "CreatedAt" TEXT NOT NULL
        );
        CREATE TABLE IF NOT EXISTS "Messages" (
          "Id" INTEGER NOT NULL CONSTRAINT "PK_Messages" PRIMARY KEY AUTOINCREMENT,
          "AccountId" INTEGER NOT NULL,
          "Title" TEXT NOT NULL,
          "Content" TEXT NULL,
          "IsRead" INTEGER NOT NULL,
          "CreatedAt" TEXT NOT NULL
        );
        CREATE TABLE IF NOT EXISTS "ProviderApplies" (
          "Id" INTEGER NOT NULL CONSTRAINT "PK_ProviderApplies" PRIMARY KEY AUTOINCREMENT,
          "AccountId" INTEGER NOT NULL,
          "CompanyName" TEXT NOT NULL,
          "TaxNo" TEXT NOT NULL,
          "LicenseFile" TEXT NOT NULL,
          "ContactName" TEXT NOT NULL,
          "ContactEmail" TEXT NOT NULL,
          "ContactPhone" TEXT NOT NULL,
          "Status" INTEGER NOT NULL,
          "CreatedAt" TEXT NOT NULL
        );
        CREATE TABLE IF NOT EXISTS "InvoiceApplies" (
          "Id" INTEGER NOT NULL CONSTRAINT "PK_InvoiceApplies" PRIMARY KEY AUTOINCREMENT,
          "ApplyNo" TEXT NOT NULL,
          "CustomerId" INTEGER NOT NULL,
          "LicenseFile" TEXT NULL,
          "Amount" TEXT NOT NULL,
          "TitleName" TEXT NULL,
          "TaxNo" TEXT NULL,
          "Email" TEXT NULL,
          "Issuer" INTEGER NOT NULL,
          "Status" INTEGER NOT NULL,
          "CreatedAt" TEXT NOT NULL,
          "UpdatedAt" TEXT NOT NULL
        );
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_InvoiceApplies_ApplyNo" ON "InvoiceApplies" ("ApplyNo");
        CREATE TABLE IF NOT EXISTS "ProviderLevels" (
          "Id" INTEGER NOT NULL CONSTRAINT "PK_ProviderLevels" PRIMARY KEY AUTOINCREMENT,
          "Name" TEXT NOT NULL,
          "CommissionRate" TEXT NOT NULL,
          "SortNo" INTEGER NOT NULL,
          "Status" INTEGER NOT NULL,
          "CreatedAt" TEXT NOT NULL,
          "UpdatedAt" TEXT NOT NULL
        );
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_ProviderLevels_Name" ON "ProviderLevels" ("Name");
        CREATE TABLE IF NOT EXISTS "Providers" (
          "Id" INTEGER NOT NULL CONSTRAINT "PK_Providers" PRIMARY KEY AUTOINCREMENT,
          "ProviderNo" TEXT NOT NULL,
          "AccountId" INTEGER NOT NULL,
          "Name" TEXT NOT NULL,
          "LevelId" INTEGER NOT NULL,
          "Status" INTEGER NOT NULL,
          "EffectiveAt" TEXT NULL,
          "CreatedAt" TEXT NOT NULL,
          "UpdatedAt" TEXT NOT NULL
        );
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_Providers_ProviderNo" ON "Providers" ("ProviderNo");
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_Providers_AccountId" ON "Providers" ("AccountId");
        """);
    AddColumn(db, "Orders", "InvoiceApplyId", "INTEGER NULL");
    AddColumn(db, "Orders", "Hidden", "INTEGER NOT NULL DEFAULT 0");
    AddColumn(db, "Customers", "Nickname", "TEXT NULL");
    AddColumn(db, "Customers", "CompanyName", "TEXT NULL");
    AddColumn(db, "Customers", "InvoiceTitle", "TEXT NULL");
    AddColumn(db, "Customers", "InvoiceTaxNo", "TEXT NULL");
    AddColumn(db, "Customers", "InvoiceEmail", "TEXT NULL");
    AddColumn(db, "Customers", "InvoiceLicense", "TEXT NULL");
    if (!db.AdminUsers.Any())
    {
        var username = app.Configuration["Admin:Username"];
        var password = app.Configuration["Admin:Password"];
        if (string.IsNullOrWhiteSpace(username)) username = VendorController.DefaultAdminUser;
        if (string.IsNullOrWhiteSpace(password)) password = VendorController.DefaultAdminPassword;
        db.AdminUsers.Add(new AdminUser
        {
            Username = username.Trim(),
            PasswordHash = scope.ServiceProvider.GetRequiredService<IPasswordHasher>().Hash(password),
            CreatedAt = DateTime.UtcNow
        });
        db.SaveChanges();
    }
}
static void AddColumn(WebDb db, string table, string column, string definition)
{
    var connection = db.Database.GetDbConnection();
    if (connection.State != System.Data.ConnectionState.Open) connection.Open();
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

app.UseAuthentication();
app.Use(async (ctx, next) =>
{
    if (ctx.Request.Path.StartsWithSegments("/api/vendor"))
    {
        var vendor = await ctx.AuthenticateAsync(VendorController.VendorScheme);
        if (vendor.Succeeded && vendor.Principal != null)
            ctx.User = vendor.Principal;
    }
    await next();
});
app.UseAuthorization();
app.MapControllers();
app.Run();
public partial class Program { }
