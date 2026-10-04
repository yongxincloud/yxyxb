namespace Yxy.Baidu
{
    internal interface IBaiduTransport
    {
        string Call(string method, string url, string accessToken, byte[] body, string contentType);
    }
}
