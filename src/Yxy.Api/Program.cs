using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Yxy.Api.Data;
using Yxy.Api.Services;

var builder = WebApplication.CreateBuilder(args);
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
    });
builder.Services.AddAuthorization();
builder.Services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddScoped<EmailCodeService>();
builder.Services.AddScoped<CaptchaService>();
builder.Services.AddScoped<AccountService>();
builder.Services.AddScoped<ClientAuthService>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddSingleton<FakeEmailSender>();
builder.Services.AddSingleton<IEmailSender>(sp => sp.GetRequiredService<FakeEmailSender>());
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
    await scope.ServiceProvider.GetRequiredService<OrderService>().EnsureCatalogAsync();
    db.Database.ExecuteSqlRaw("""
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
}
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
public partial class Program { }
