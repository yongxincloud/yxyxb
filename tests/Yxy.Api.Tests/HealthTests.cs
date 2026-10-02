using NUnit.Framework;

namespace Yxy.Api.Tests;

public sealed class HealthTests
{
    [Test]
    public async Task Health_Returns_Ok()
    {
        await using var factory = new WebAppFactory();
        var json = await factory.CreateClient().GetStringAsync("/api/health");
        Assert.That(json, Does.Contain("\"ok\":true"));
    }
}
