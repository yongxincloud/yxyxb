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
builder.Services.AddSingleton<FakeEmailSender>();
builder.Services.AddSingleton<IEmailSender>(sp => sp.GetRequiredService<FakeEmailSender>());
builder.Services.AddDbContext<WebDb>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Web")));
var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<WebDb>().Database.EnsureCreated();
}
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
public partial class Program { }
