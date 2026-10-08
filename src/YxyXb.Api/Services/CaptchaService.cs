using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;

namespace YxyXb.Api.Services;

public sealed record CaptchaView(string Id, string Svg);

public sealed class CaptchaService
{
    private readonly IMemoryCache _cache;
    private readonly bool _testing;
    public CaptchaService(IMemoryCache cache, IHostEnvironment env)
    {
        _cache = cache;
        _testing = env.IsEnvironment("Testing");
    }

    public CaptchaView Create()
    {
        var answer = _testing ? "0000" : Random.Shared.Next(0, 10000).ToString("D4");
        var id = Guid.NewGuid().ToString("N");
        _cache.Set("captcha:" + id, answer, TimeSpan.FromMinutes(5));
        var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"120\" height=\"40\"><rect width=\"120\" height=\"40\" fill=\"#F6F6F6\"/>";
        svg += _testing
            ? "<text x=\"16\" y=\"28\" font-size=\"24\" fill=\"#3171FF\">****</text>"
            : "<text x=\"16\" y=\"28\" font-size=\"24\" fill=\"#3171FF\">" + answer + "</text>";
        svg += "</svg>";
        return new CaptchaView(id, svg);
    }

    public bool VerifyAndConsume(string? id, string? answer)
    {
        if (string.IsNullOrEmpty(id)) return false;
        var key = "captcha:" + id;
        var ok = _cache.TryGetValue(key, out string? stored) && stored == (answer ?? "");
        _cache.Remove(key);
        return ok;
    }
}
