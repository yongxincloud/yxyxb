using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using YxyXb.Api.Controllers;
using YxyXb.Api.Data;
using YxyXb.Api.Services;

namespace YxyXb.Api.Tests;

public sealed class AlipayPayTests
{
    [Test]
    public async Task Refund_Request_Asks_Alipay_For_Utf8()
    {
        using var merchant = RSA.Create(2048);
        using var alipay = RSA.Create(2048);
        var privateKey = Convert.ToBase64String(alipay.ExportPkcs8PrivateKey());
        var publicKey = Convert.ToBase64String(alipay.ExportSubjectPublicKeyInfo());
        var payload = """{"code":"10000","msg":"Success","fund_change":"Y"}""";
        var body = RefundBody(payload, AlipayPay.SignText(payload, privateKey));
        var handler = new CaptureHandler(body);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Alipay:AppId"] = "2021000000000001",
            ["Alipay:PrivateKey"] = Convert.ToBase64String(merchant.ExportPkcs8PrivateKey()),
            ["Alipay:AlipayPublicKey"] = publicKey
        }).Build();
        var gateway = new AlipayGateway(config, new SingleClientFactory(new HttpClient(handler)));
        var result = await gateway.RefundAsync("O1", 0.01m);
        Assert.That(result.Ok, Is.True);
        Assert.That(handler.Url!.Query, Does.Contain("charset=utf-8"));
    }

    sealed class CaptureHandler : HttpMessageHandler
    {
        readonly string _body;
        public Uri? Url { get; private set; }
        public CaptureHandler(string body) => _body = body;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Url = request.RequestUri;
            var content = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(_body));
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/html") { CharSet = "GBK" };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }

    sealed class SingleClientFactory : IHttpClientFactory
    {
        readonly HttpClient _client;
        public SingleClientFactory(HttpClient client) => _client = client;
        public HttpClient CreateClient(string name) => _client;
    }

    [Test]
    public void Page_Pay_Url_Signature_Matches_Order()
    {
        using var merchant = RSA.Create(2048);
        var privateKey = Convert.ToBase64String(merchant.ExportPkcs8PrivateKey());
        var publicKey = Convert.ToBase64String(merchant.ExportSubjectPublicKeyInfo());
        var when = new DateTime(2026, 10, 4, 19, 40, 0);
        var url = AlipayPay.BuildPagePayUrl(
            "2021007105641746",
            privateKey,
            "https://pay.example.com/api/payments/alipay/notify",
            "https://pay.example.com/orders",
            "O202610040001",
            "用心云·循备标准版",
            12m,
            when);

        var fields = ParseQuery(url);
        Assert.That(new Uri(url).GetLeftPart(UriPartial.Path), Is.EqualTo("https://openapi.alipay.com/gateway.do"));
        Assert.That(fields["method"], Is.EqualTo("alipay.trade.page.pay"));
        Assert.That(fields["app_id"], Is.EqualTo("2021007105641746"));
        Assert.That(fields["biz_content"], Does.Contain("\"out_trade_no\":\"O202610040001\""));
        Assert.That(fields["biz_content"], Does.Contain("\"total_amount\":\"12.00\""));
        Assert.That(fields["biz_content"], Does.Contain("FAST_INSTANT_TRADE_PAY"));
        Assert.That(AlipayPay.Verify(fields, fields["sign"], publicKey, excludeSignType: false), Is.True);

        fields["biz_content"] = fields["biz_content"].Replace("12.00", "1.00");
        Assert.That(AlipayPay.Verify(fields, fields["sign"], publicKey, excludeSignType: false), Is.False);
    }

    [Test]
    public void Refund_Response_Requires_Alipay_Signature()
    {
        using var alipay = RSA.Create(2048);
        var privateKey = Convert.ToBase64String(alipay.ExportPkcs8PrivateKey());
        var publicKey = Convert.ToBase64String(alipay.ExportSubjectPublicKeyInfo());
        var payload = """{"code":"10000","msg":"Success","out_trade_no":"O1","fund_change":"Y","refund_fee":"0.10"}""";
        var body = RefundBody(payload, AlipayPay.SignText(payload, privateKey));
        var ok = AlipayPay.ReadRefundResponse(body, publicKey);
        Assert.That(ok.Ok, Is.True);

        var rejected = AlipayPay.ReadRefundResponse(RefundBody(payload, "bad-sign"), publicKey);
        Assert.That(rejected.Ok, Is.False);
        Assert.That(rejected.Error, Is.EqualTo("支付宝退款验签失败"));

        var failed = """{"code":"40004","msg":"Business Failed","sub_msg":"卖家余额不足"}""";
        var remote = AlipayPay.ReadRefundResponse(RefundBody(failed, AlipayPay.SignText(failed, privateKey)), publicKey);
        Assert.That(remote.Ok, Is.False);
        Assert.That(remote.Error, Is.EqualTo("卖家余额不足"));
    }

    static string RefundBody(string payload, string sign) =>
        "{\"alipay_trade_refund_response\":" + payload + ",\"sign\":\"" + sign + "\"}";

    [Test]
    public async Task Alipay_Refund_Timeout_Stays_Paid_And_Returns_Message()
    {
        using var merchant = RSA.Create(2048);
        using var alipay = RSA.Create(2048);
        await using var factory = new WebAppFactory();
        factory.ExtraConfig["Alipay:AppId"] = "2021000000000001";
        factory.ExtraConfig["Alipay:PrivateKey"] = Convert.ToBase64String(merchant.ExportPkcs8PrivateKey());
        factory.ExtraConfig["Alipay:AlipayPublicKey"] = Convert.ToBase64String(alipay.ExportSubjectPublicKeyInfo());
        factory.ExtraConfig["Alipay:NotifyUrl"] = "https://pay.example.com/api/payments/alipay/notify";
        factory.GatewayOverride = new ThrowingAlipayGateway();
        var client = await LoginAsync(factory);
        var created = await client.PostAsJsonAsync("/api/orders", new { productNo = "STANDARD", orderType = 2, channel = "alipay" });
        var order = await created.Content.ReadFromJsonAsync<OrderResult>();
        var notify = new HttpRequestMessage(HttpMethod.Post, "/api/payments/notify")
        {
            Content = JsonContent.Create(new { orderNo = order!.OrderNo, tradeNo = "RF-TIMEOUT", channel = "alipay", amount = 0.10m })
        };
        notify.Headers.Add("X-Pay-Sign", OrderService.Sign(OrderController.DevPaySecret, order.OrderNo!, "RF-TIMEOUT", "alipay", 0.10m));
        Assert.That((await client.SendAsync(notify)).StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var refund = await AccountApi.Post(client, "/api/orders/" + order.OrderNo + "/refund", new { });
        var body = await refund.Content.ReadAsStringAsync();
        Assert.That(refund.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest), body);
        Assert.That(body, Does.Contain("支付宝退款请求失败"));
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WebDb>();
        Assert.That((await db.Orders.SingleAsync()).PayStatus, Is.EqualTo(1));
    }

    sealed class ThrowingAlipayGateway : IAlipayGateway
    {
        public bool IsConfigured => true;
        public Task<OrderResult> RefundAsync(string orderNo, decimal amount) => throw new TaskCanceledException();
    }

    [Test]
    public async Task Alipay_Refund_Keeps_Paid_Until_Channel_Accepts()
    {
        using var merchant = RSA.Create(2048);
        using var alipay = RSA.Create(2048);
        await using var factory = new WebAppFactory();
        factory.ExtraConfig["Alipay:AppId"] = "2021000000000001";
        factory.ExtraConfig["Alipay:PrivateKey"] = Convert.ToBase64String(merchant.ExportPkcs8PrivateKey());
        factory.ExtraConfig["Alipay:AlipayPublicKey"] = Convert.ToBase64String(alipay.ExportSubjectPublicKeyInfo());
        factory.ExtraConfig["Alipay:NotifyUrl"] = "https://pay.example.com/api/payments/alipay/notify";
        var fake = new FakeAlipayGateway { Next = new OrderResult(false, "卖家余额不足") };
        factory.GatewayOverride = fake;
        var client = await LoginAsync(factory);
        var created = await client.PostAsJsonAsync("/api/orders", new { productNo = "STANDARD", orderType = 2, channel = "alipay" });
        var order = await created.Content.ReadFromJsonAsync<OrderResult>();
        Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var notify = new HttpRequestMessage(HttpMethod.Post, "/api/payments/notify")
        {
            Content = JsonContent.Create(new { orderNo = order!.OrderNo, tradeNo = "RF-ALI", channel = "alipay", amount = 0.10m })
        };
        notify.Headers.Add("X-Pay-Sign", OrderService.Sign(OrderController.DevPaySecret, order.OrderNo!, "RF-ALI", "alipay", 0.10m));
        Assert.That((await client.SendAsync(notify)).StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var rejected = await AccountApi.Post(client, "/api/orders/" + order.OrderNo + "/refund", new { });
        Assert.That(await rejected.Content.ReadAsStringAsync(), Does.Contain("卖家余额不足"));
        fake.Next = new OrderResult(true, null);
        var accepted = await AccountApi.Post(client, "/api/orders/" + order.OrderNo + "/refund", new { });
        Assert.That(accepted.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WebDb>();
        Assert.That((await db.Orders.SingleAsync()).PayStatus, Is.EqualTo(2));
        Assert.That((await db.SubscriptionAuths.SingleAsync()).Status, Is.EqualTo(0));
        Assert.That(fake.Calls, Is.EqualTo(2));
        Assert.That(fake.Amounts[0], Is.EqualTo(0.10m));
    }

    sealed class FakeAlipayGateway : IAlipayGateway
    {
        public OrderResult Next { get; set; } = new(false, "卖家余额不足");
        public int Calls { get; private set; }
        public List<decimal> Amounts { get; } = new();
        public bool IsConfigured => true;
        public Task<OrderResult> RefundAsync(string orderNo, decimal amount)
        {
            Calls++;
            Amounts.Add(amount);
            return Task.FromResult(Next);
        }
    }

    [Test]
    public async Task Notify_Grants_Once_And_Rejects_Bad_Signature()
    {
        using var merchant = RSA.Create(2048);
        using var alipay = RSA.Create(2048);
        await using var factory = new WebAppFactory();
        factory.ExtraConfig["Alipay:AppId"] = "2021000000000001";
        factory.ExtraConfig["Alipay:PrivateKey"] = Convert.ToBase64String(merchant.ExportPkcs8PrivateKey());
        factory.ExtraConfig["Alipay:AlipayPublicKey"] = Convert.ToBase64String(alipay.ExportSubjectPublicKeyInfo());
        factory.ExtraConfig["Alipay:NotifyUrl"] = "https://pay.example.com/api/payments/alipay/notify";
        factory.ExtraConfig["Alipay:ReturnUrl"] = "https://pay.example.com/orders";
        var client = await LoginAsync(factory);
        var created = await client.PostAsJsonAsync("/api/orders", new { productNo = "STANDARD", orderType = 1, channel = "alipay" });
        var order = await created.Content.ReadFromJsonAsync<OrderResult>();
        Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var pay = await client.PostAsJsonAsync("/api/orders/" + order!.OrderNo + "/alipay", new { });
        var payBody = await pay.Content.ReadFromJsonAsync<PayBody>();
        Assert.That(pay.StatusCode, Is.EqualTo(HttpStatusCode.OK), await pay.Content.ReadAsStringAsync());
        Assert.That(payBody!.PayUrl, Does.Contain("app_id=2021000000000001"));

        var fields = NotifyFields(order.OrderNo!, "T100", "98.00");
        fields["sign"] = AlipayPay.Sign(fields, Convert.ToBase64String(alipay.ExportPkcs8PrivateKey()), excludeSignType: true);
        var first = await client.PostAsync("/api/payments/alipay/notify", new FormUrlEncodedContent(fields));
        var second = await client.PostAsync("/api/payments/alipay/notify", new FormUrlEncodedContent(fields));
        Assert.That(await first.Content.ReadAsStringAsync(), Is.EqualTo("success"));
        Assert.That(await second.Content.ReadAsStringAsync(), Is.EqualTo("success"));

        fields["sign"] = "bad-sign";
        var rejected = await client.PostAsync("/api/payments/alipay/notify", new FormUrlEncodedContent(fields));
        Assert.That(await rejected.Content.ReadAsStringAsync(), Is.EqualTo("failure"));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<YxyXb.Api.Data.WebDb>();
        Assert.That(await db.SubscriptionAuths.CountAsync(), Is.EqualTo(1));
        Assert.That(await db.Payments.CountAsync(), Is.EqualTo(1));
    }

    static Dictionary<string, string> NotifyFields(string orderNo, string tradeNo, string amount) => new()
    {
        ["app_id"] = "2021000000000001",
        ["out_trade_no"] = orderNo,
        ["trade_no"] = tradeNo,
        ["trade_status"] = "TRADE_SUCCESS",
        ["total_amount"] = amount,
        ["charset"] = "utf-8",
        ["sign_type"] = "RSA2"
    };

    static Dictionary<string, string> ParseQuery(string url)
    {
        var query = new Uri(url).Query.TrimStart('?');
        var map = new Dictionary<string, string>();
        foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var index = part.IndexOf('=');
            map[Uri.UnescapeDataString(part[..index])] = Uri.UnescapeDataString(part[(index + 1)..]);
        }
        return map;
    }

    static async Task<HttpClient> LoginAsync(WebAppFactory factory)
    {
        var client = factory.CreateClient();
        var email = "ali" + Guid.NewGuid().ToString("N")[..8] + "@example.com";
        await AccountApi.Register(factory, client, email, "abc12345");
        var login = await AccountApi.Post(client, "/api/account/login", new { email, password = "abc12345" });
        Assert.That(login.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        return client;
    }

    sealed record PayBody(bool Ok, string? Error, string? PayUrl);
}
