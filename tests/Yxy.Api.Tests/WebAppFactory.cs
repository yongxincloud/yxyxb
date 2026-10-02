using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace Yxy.Api.Tests;

public class WebAppFactory : WebApplicationFactory<Program>
{
    public string DbPath { get; } = Path.Combine(Path.GetTempPath(), "yxy-api-" + Guid.NewGuid().ToString("N") + ".db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Web"] = "Data Source=" + DbPath
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        SqliteConnection.ClearAllPools();
        if (File.Exists(DbPath)) File.Delete(DbPath);
    }
}
