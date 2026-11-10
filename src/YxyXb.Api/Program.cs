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
builder.Services.AddScoped<VendorCustomerAuthService>();
builder.Services.AddScoped<VendorProviderService>();
builder.Services.AddScoped<VendorSettlementService>();
builder.Services.AddScoped<VendorProductService>();
builder.Services.AddScoped<DatabaseSeedService>();
builder.Services.AddHttpClient("alipay", client => client.Timeout = TimeSpan.FromSeconds(20));
builder.Services.AddScoped<IAlipayGateway, AlipayGateway>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddSingleton<FakeEmailSender>();
builder.Services.AddScoped<MailSettingsService>();
builder.Services.AddScoped<ExpiryNoticeService>();
builder.Services.AddScoped<IEmailSender, RoutingEmailSender>();
builder.Services.AddHostedService<SiteMaintenance>();
builder.Services.AddDbContext<WebDb>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Web")));
var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<WebDb>();
    new DatabaseStartupService().Initialize(db);
    var seed = scope.ServiceProvider.GetRequiredService<DatabaseSeedService>();
    await seed.EnsureCatalogAsync();

    var username = app.Configuration["Admin:Username"];
    var password = app.Configuration["Admin:Password"];
    var adminEmail = AccountService.Norm(app.Configuration["Admin:Email"]);
    var resetPassword = app.Configuration.GetValue("Admin:ResetPassword", false);
    if (string.IsNullOrWhiteSpace(username)) username = VendorController.DefaultAdminUser;
    if (string.IsNullOrWhiteSpace(password)) password = VendorController.DefaultAdminPassword;
    var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
    await seed.EnsureAdminAsync(username, password, adminEmail, resetPassword, hasher);
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
