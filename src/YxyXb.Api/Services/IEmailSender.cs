namespace YxyXb.Api.Services;

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body);
}

public sealed class FakeEmailSender : IEmailSender
{
    public string? LastTo { get; private set; }
    public string? LastBody { get; private set; }
    public Task SendAsync(string to, string subject, string body)
    {
        LastTo = to;
        LastBody = body;
        return Task.CompletedTask;
    }
}
