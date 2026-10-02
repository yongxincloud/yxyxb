using Microsoft.EntityFrameworkCore;
using Yxy.Api.Data;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
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
