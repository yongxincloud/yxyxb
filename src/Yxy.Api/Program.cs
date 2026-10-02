using Microsoft.EntityFrameworkCore;
using Yxy.Api.Data;
using Yxy.Api.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddMemoryCache();
builder.Services.AddControllers();
builder.Services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddScoped<EmailCodeService>();
builder.Services.AddScoped<CaptchaService>();
builder.Services.AddSingleton<FakeEmailSender>();
builder.Services.AddSingleton<IEmailSender>(sp => sp.GetRequiredService<FakeEmailSender>());
builder.Services.AddDbContext<WebDb>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Web")));
var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<WebDb>().Database.EnsureCreated();
}
app.MapControllers();
app.Run();
public partial class Program { }
