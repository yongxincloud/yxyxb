using NUnit.Framework;

namespace Yxy.Web.Tests;

public sealed class HomePageTests
{
    [Test]
    public async Task Home_Shows_Brand_Token()
    {
        await using var factory = new WebAppFactory();
        var client = factory.CreateClient();
        var html = await client.GetStringAsync("/");
        Assert.That(html, Does.Contain("#3171FF"));
        Assert.That(html, Does.Contain("tokens.css"));
    }
}
