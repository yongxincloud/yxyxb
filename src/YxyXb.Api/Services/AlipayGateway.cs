using System.Text;

namespace YxyXb.Api.Services;

public interface IAlipayGateway
{
    bool IsConfigured { get; }
    Task<OrderResult> RefundAsync(string orderNo, decimal amount);
}

public sealed class AlipayGateway : IAlipayGateway
{
    private readonly IConfiguration _config;
    private readonly HttpClient _http;

    public AlipayGateway(IConfiguration config, IHttpClientFactory http)
    {
        _config = config;
        _http = http.CreateClient("alipay");
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_config["Alipay:AppId"])
        && !string.IsNullOrWhiteSpace(_config["Alipay:PrivateKey"])
        && !string.IsNullOrWhiteSpace(_config["Alipay:AlipayPublicKey"]);

    public async Task<OrderResult> RefundAsync(string orderNo, decimal amount)
    {
        try
        {
            var appId = _config["Alipay:AppId"] ?? "";
            var privateKey = _config["Alipay:PrivateKey"] ?? "";
            var publicKey = _config["Alipay:AlipayPublicKey"] ?? "";
            var fields = AlipayPay.BuildRefundFields(appId, privateKey, orderNo, amount, ChinaNow());
            using var response = await _http.PostAsync(AlipayPay.RefundGateway, new FormUrlEncodedContent(fields));
            var bytes = await response.Content.ReadAsByteArrayAsync();
            var body = Encoding.UTF8.GetString(bytes);
            if (!response.IsSuccessStatusCode || string.IsNullOrWhiteSpace(body))
                return new OrderResult(false, "支付宝退款请求失败");
            return AlipayPay.ReadRefundResponse(body, publicKey);
        }
        catch (Exception)
        {
            return new OrderResult(false, "支付宝退款请求失败");
        }
    }

    static DateTime ChinaNow()
    {
        var zoneId = OperatingSystem.IsWindows() ? "China Standard Time" : "Asia/Shanghai";
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(zoneId));
    }
}
