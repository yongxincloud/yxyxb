using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Yxy.Api.Controllers;
using Yxy.Api.Services;

namespace Yxy.Api.Tests;

public class WebAppFactory : WebApplicationFactory<Program>
{
    public string DbPath { get; } = Path.Combine(Path.GetTempPath(), "yxy-api-" + Guid.NewGuid().ToString("N") + ".db");
    public Dictionary<string, string?> ExtraConfig { get; } = new();
    public IAlipayGateway? GatewayOverride { get; set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            var data = new Dictionary<string, string?>
            {
                ["ConnectionStrings:Web"] = "Data Source=" + DbPath,
                ["Admin:Username"] = VendorController.DefaultAdminUser,
                ["Admin:Password"] = VendorController.DefaultAdminPassword
            };
            foreach (var pair in ExtraConfig) data[pair.Key] = pair.Value;
            config.AddInMemoryCollection(data);
        });
        builder.ConfigureServices(services =>
        {
            if (GatewayOverride == null) return;
            services.RemoveAll<IAlipayGateway>();
            services.AddSingleton(GatewayOverride);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        SqliteConnection.ClearAllPools();
        if (File.Exists(DbPath)) File.Delete(DbPath);
    }
}
