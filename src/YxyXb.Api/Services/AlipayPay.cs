using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace YxyXb.Api.Services;

public static class AlipayPay
{
    public const string Gateway = "https://openapi.alipay.com/gateway.do";
    public const string RefundGateway = Gateway + "?charset=utf-8";

    public static string BuildPagePayUrl(
        string appId,
        string privateKey,
        string notifyUrl,
        string? returnUrl,
        string orderNo,
        string subject,
        decimal amount,
        DateTime chinaTime)
    {
        var biz = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["out_trade_no"] = orderNo,
            ["product_code"] = "FAST_INSTANT_TRADE_PAY",
            ["total_amount"] = amount.ToString("0.00", CultureInfo.InvariantCulture),
            ["subject"] = subject,
            ["timeout_express"] = "30m"
        }, new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        var fields = new Dictionary<string, string>
        {
            ["app_id"] = appId,
            ["method"] = "alipay.trade.page.pay",
            ["format"] = "JSON",
            ["charset"] = "utf-8",
            ["sign_type"] = "RSA2",
            ["timestamp"] = chinaTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            ["version"] = "1.0",
            ["notify_url"] = notifyUrl,
            ["biz_content"] = biz
        };
        if (!string.IsNullOrWhiteSpace(returnUrl)) fields["return_url"] = returnUrl;
        fields["sign"] = Sign(fields, privateKey, excludeSignType: false);
        return Gateway + "?" + string.Join("&", fields.Select(pair => Encode(pair.Key) + "=" + Encode(pair.Value)));
    }

    public static Dictionary<string, string> BuildRefundFields(string appId, string privateKey, string orderNo, decimal amount, DateTime chinaTime)
    {
        var biz = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["out_trade_no"] = orderNo,
            ["refund_amount"] = amount.ToString("0.00", CultureInfo.InvariantCulture),
            ["refund_reason"] = "用户申请退款",
            ["out_request_no"] = "R" + orderNo
        }, new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        var fields = new Dictionary<string, string>
        {
            ["app_id"] = appId,
            ["method"] = "alipay.trade.refund",
            ["format"] = "JSON",
            ["charset"] = "utf-8",
            ["sign_type"] = "RSA2",
            ["timestamp"] = chinaTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            ["version"] = "1.0",
            ["biz_content"] = biz
        };
        fields["sign"] = Sign(fields, privateKey, excludeSignType: false);
        return fields;
    }

    public static OrderResult ReadRefundResponse(string body, string publicKey)
    {
        const string marker = "\"alipay_trade_refund_response\":";
        var markerAt = body.IndexOf(marker, StringComparison.Ordinal);
        if (markerAt < 0) return new OrderResult(false, "支付宝退款验签失败");
        var start = body.IndexOf('{', markerAt);
        if (start < 0) return new OrderResult(false, "支付宝退款验签失败");
        var end = MatchBrace(body, start);
        if (end < 0) return new OrderResult(false, "支付宝退款验签失败");
        var payload = body[start..(end + 1)];
        const string signKey = "\"sign\":\"";
        var signAt = body.IndexOf(signKey, end, StringComparison.Ordinal);
        if (signAt < 0) return new OrderResult(false, "支付宝退款验签失败");
        var signStart = signAt + signKey.Length;
        var signEnd = body.IndexOf('"', signStart);
        if (signEnd < 0) return new OrderResult(false, "支付宝退款验签失败");
        var sign = body[signStart..signEnd];
        if (!VerifyText(payload, sign, publicKey)) return new OrderResult(false, "支付宝退款验签失败");
        using var doc = JsonDocument.Parse(payload);
        var code = doc.RootElement.TryGetProperty("code", out var codeEl) ? codeEl.GetString() : "";
        if (code == "10000") return new OrderResult(true, null);
        var message = doc.RootElement.TryGetProperty("sub_msg", out var sub) ? sub.GetString() : null;
        return new OrderResult(false, string.IsNullOrWhiteSpace(message) ? "支付宝退款失败" : message);
    }

    public static string SignText(string content, string privateKey)
    {
        using var rsa = RSA.Create();
        rsa.ImportPkcs8PrivateKey(Decode(privateKey), out _);
        var data = rsa.SignData(Encoding.UTF8.GetBytes(content), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return Convert.ToBase64String(data);
    }

    public static string Sign(IEnumerable<KeyValuePair<string, string>> fields, string privateKey, bool excludeSignType)
        => SignText(Canonical(fields, excludeSignType), privateKey);

    static bool VerifyText(string content, string signature, string publicKey)
    {
        try
        {
            using var rsa = RSA.Create();
            rsa.ImportSubjectPublicKeyInfo(Decode(publicKey), out _);
            return rsa.VerifyData(Encoding.UTF8.GetBytes(content), Convert.FromBase64String(signature), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException or ArgumentException)
        {
            return false;
        }
    }

    static int MatchBrace(string text, int start)
    {
        var depth = 0;
        var inString = false;
        for (var i = start; i < text.Length; i++)
        {
            var ch = text[i];
            if (inString)
            {
                if (ch == '\\') { i++; continue; }
                if (ch == '"') inString = false;
                continue;
            }
            if (ch == '"') { inString = true; continue; }
            if (ch == '{') depth++;
            else if (ch == '}')
            {
                depth--;
                if (depth == 0) return i;
            }
        }
        return -1;
    }

    public static bool Verify(IEnumerable<KeyValuePair<string, string>> fields, string signature, string publicKey, bool excludeSignType)
        => VerifyText(Canonical(fields, excludeSignType), signature, publicKey);

    public static string Canonical(IEnumerable<KeyValuePair<string, string>> fields, bool excludeSignType)
    {
        return string.Join("&", fields
            .Where(pair => pair.Key != "sign")
            .Where(pair => !excludeSignType || pair.Key != "sign_type")
            .Where(pair => !string.IsNullOrEmpty(pair.Value))
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => pair.Key + "=" + pair.Value));
    }

    static string Encode(string value) => Uri.EscapeDataString(value);

    static byte[] Decode(string key)
    {
        var text = key
            .Replace("-----BEGIN PRIVATE KEY-----", "", StringComparison.Ordinal)
            .Replace("-----END PRIVATE KEY-----", "", StringComparison.Ordinal)
            .Replace("-----BEGIN PUBLIC KEY-----", "", StringComparison.Ordinal)
            .Replace("-----END PUBLIC KEY-----", "", StringComparison.Ordinal)
            .Replace("-----BEGIN RSA PRIVATE KEY-----", "", StringComparison.Ordinal)
            .Replace("-----END RSA PRIVATE KEY-----", "", StringComparison.Ordinal)
            .Replace("\r", "", StringComparison.Ordinal)
            .Replace("\n", "", StringComparison.Ordinal)
            .Replace(" ", "", StringComparison.Ordinal);
        return Convert.FromBase64String(text);
    }
}
