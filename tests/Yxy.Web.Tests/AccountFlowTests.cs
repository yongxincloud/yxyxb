using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Yxy.Web.Data;
using Yxy.Web.Services;

namespace Yxy.Web.Tests;

public sealed class RegisterTests
{
    [Test]
    public async Task Register_Creates_Account_And_Customer()
    {
        await using var factory = new WebAppFactory();
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        var code = await SendRegisterCode(factory, client, "user@example.com");
        var response = await client.PostAsync("/Account/Register", Form(new Dictionary<string, string>
        {
            ["email"] = "user@example.com",
            ["password"] = "abc12345",
            ["confirm"] = "abc12345",
            ["emailCode"] = code
        }));
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WebDb>();
        var account = await db.Accounts.SingleAsync();
        Assert.That(account.PasswordHash, Is.Not.EqualTo("abc12345"));
        var customer = await db.Customers.SingleAsync();
        Assert.That(customer.DirectProviderId, Is.Null);
        Assert.That(customer.AccountId, Is.EqualTo(account.Id));
    }

    [Test]
    public async Task Duplicate_Email_Is_Rejected()
    {
        await using var factory = new WebAppFactory();
        var client = factory.CreateClient();
        await Register(factory, client, "user@example.com", "abc12345");
        var html = await (await client.PostAsync("/Account/Register", Form(new Dictionary<string, string>
        {
            ["email"] = "user@example.com",
            ["password"] = "abc12345",
            ["confirm"] = "abc12345",
            ["emailCode"] = "000000"
        }))).Content.ReadAsStringAsync();
        Assert.That(html, Does.Contain("该邮箱已注册"));
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WebDb>();
        Assert.That(await db.Accounts.CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task Weak_Password_Is_Rejected()
    {
        await using var factory = new WebAppFactory();
        var client = factory.CreateClient();
        var code = await SendRegisterCode(factory, client, "user@example.com");
        var html = await (await client.PostAsync("/Account/Register", Form(new Dictionary<string, string>
        {
            ["email"] = "user@example.com",
            ["password"] = "abcdefgh",
            ["confirm"] = "abcdefgh",
            ["emailCode"] = code
        }))).Content.ReadAsStringAsync();
        Assert.That(html, Does.Contain("密码至少 8 位且同时包含字母和数字"));
    }

    [Test]
    public async Task Provider_No_Binds_Enabled_Provider()
    {
        await using var factory = new WebAppFactory();
        var client = factory.CreateClient();
        var providerId = await SeedProvider(factory, "P001", 1);
        var code = await SendRegisterCode(factory, client, "user@example.com");
        await client.PostAsync("/Account/Register", Form(new Dictionary<string, string>
        {
            ["email"] = "user@example.com",
            ["password"] = "abc12345",
            ["confirm"] = "abc12345",
            ["emailCode"] = code,
            ["providerNo"] = "P001"
        }));
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WebDb>();
        var customer = await db.Customers.SingleAsync();
        Assert.That(customer.DirectProviderId, Is.EqualTo(providerId));
    }

    [Test]
    public async Task Unknown_Provider_No_Still_Registers()
    {
        await using var factory = new WebAppFactory();
        var client = factory.CreateClient();
        var code = await SendRegisterCode(factory, client, "user@example.com");
        var response = await client.PostAsync("/Account/Register", Form(new Dictionary<string, string>
        {
            ["email"] = "user@example.com",
            ["password"] = "abc12345",
            ["confirm"] = "abc12345",
            ["emailCode"] = code,
            ["providerNo"] = "NOPE"
        }));
        Assert.That((int)response.StatusCode, Is.EqualTo(302).Or.EqualTo(200));
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WebDb>();
        Assert.That(await db.Accounts.CountAsync(), Is.EqualTo(1));
        Assert.That((await db.Customers.SingleAsync()).DirectProviderId, Is.Null);
    }

    [Test]
    public async Task Disabled_Provider_Does_Not_Bind()
    {
        await using var factory = new WebAppFactory();
        var client = factory.CreateClient();
        await SeedProvider(factory, "P009", 0);
        var code = await SendRegisterCode(factory, client, "user@example.com");
        await client.PostAsync("/Account/Register", Form(new Dictionary<string, string>
        {
            ["email"] = "user@example.com",
            ["password"] = "abc12345",
            ["confirm"] = "abc12345",
            ["emailCode"] = code,
            ["providerNo"] = "P009"
        }));
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WebDb>();
        Assert.That((await db.Customers.SingleAsync()).DirectProviderId, Is.Null);
    }

    internal static async Task<string> SendRegisterCode(WebAppFactory factory, HttpClient client, string email)
    {
        var page = await client.GetStringAsync("/Account/Register");
        var id = Regex.Match(page, "name=\"captchaId\" value=\"([^\"]+)\"").Groups[1].Value;
        var send = new HttpRequestMessage(HttpMethod.Post, "/Account/SendCode")
        {
            Content = Form(new Dictionary<string, string>
            {
                ["email"] = email,
                ["scene"] = "register",
                ["captchaId"] = id,
                ["captchaAnswer"] = "0000"
            })
        };
        send.Headers.Add("X-Test-Ip", "10.0.0." + Random.Shared.Next(1, 200));
        await client.SendAsync(send);
        var sender = factory.Services.GetRequiredService<FakeEmailSender>();
        var code = Regex.Match(sender.LastBody ?? "", @"\d{6}").Value;
        Assert.That(code, Is.Not.Empty);
        return code;
    }

    internal static async Task Register(WebAppFactory factory, HttpClient client, string email, string password)
    {
        var code = await SendRegisterCode(factory, client, email);
        await client.PostAsync("/Account/Register", Form(new Dictionary<string, string>
        {
            ["email"] = email,
            ["password"] = password,
            ["confirm"] = password,
            ["emailCode"] = code
        }));
    }

    internal static async Task SeedAccount(WebAppFactory factory, string email, string password)
    {
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WebDb>();
        var now = DateTime.UtcNow;
        var account = new Account
        {
            Email = email,
            PasswordHash = new BcryptPasswordHasher().Hash(password),
            Status = 1,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        db.Customers.Add(new Customer
        {
            CustomerNo = "C" + Guid.NewGuid().ToString("N")[..12],
            AccountId = account.Id,
            Status = 1,
            CreatedAt = now,
            UpdatedAt = now
        });
        await db.SaveChangesAsync();
    }

    internal static FormUrlEncodedContent Form(Dictionary<string, string> fields) => new(fields);

    static async Task<long> SeedProvider(WebAppFactory factory, string providerNo, int status)
    {
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WebDb>();
        var now = DateTime.UtcNow;
        var level = new ProviderLevel
        {
            Name = "L-" + providerNo,
            CommissionRate = 0.5m,
            Status = 1,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.ProviderLevels.Add(level);
        await db.SaveChangesAsync();
        var provider = new Provider
        {
            ProviderNo = providerNo,
            AccountId = 900000 + providerNo.Length + status,
            Name = providerNo,
            LevelId = level.Id,
            Status = status,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Providers.Add(provider);
        await db.SaveChangesAsync();
        return provider.Id;
    }
}

public sealed class PasswordLoginTests
{
    [Test]
    public async Task Wrong_Password_And_Missing_Account_Share_Message()
    {
        await using var factory = new WebAppFactory();
        var client = factory.CreateClient();
        await RegisterTests.Register(factory, client, "user@example.com", "abc12345");
        var wrong = await PostLogin(client, "user@example.com", "badpass1");
        var missing = await PostLogin(client, "none@example.com", "abc12345");
        Assert.That(wrong, Does.Contain("邮箱或密码错误"));
        Assert.That(missing, Does.Contain("邮箱或密码错误"));
        Assert.That(wrong, Does.Not.Contain("不存在"));
        Assert.That(missing, Does.Not.Contain("不存在"));
    }

    [Test]
    public async Task Five_Failures_Lock_For_Fifteen_Minutes()
    {
        await using var factory = new WebAppFactory();
        var client = factory.CreateClient();
        await RegisterTests.Register(factory, client, "user@example.com", "abc12345");
        for (var i = 0; i < 5; i++)
            await PostLogin(client, "user@example.com", "badpass1");
        var locked = await PostLogin(client, "user@example.com", "abc12345");
        Assert.That(locked, Does.Contain("登录已锁定"));
    }

    [Test]
    public async Task Right_Password_Sets_Cookie_And_Home()
    {
        await using var factory = new WebAppFactory();
        var client = factory.CreateClient();
        await RegisterTests.Register(factory, client, "user@example.com", "abc12345");
        var html = await PostLogin(client, "user@example.com", "abc12345");
        Assert.That(html, Does.Contain("user@example.com"));
    }

    static async Task<string> PostLogin(HttpClient client, string email, string password)
    {
        var response = await client.PostAsync("/Account/Login", RegisterTests.Form(new Dictionary<string, string>
        {
            ["email"] = email,
            ["password"] = password
        }));
        return await response.Content.ReadAsStringAsync();
    }
}

public sealed class CodeLoginTests
{
    [Test]
    public async Task Code_Login_Succeeds()
    {
        await using var factory = new WebAppFactory();
        var client = factory.CreateClient();
        await RegisterTests.SeedAccount(factory, "user@example.com", "abc12345");
        await client.PostAsync("/Account/SendCode", RegisterTests.Form(new Dictionary<string, string>
        {
            ["email"] = "user@example.com",
            ["scene"] = "login"
        }));
        var code = Regex.Match(factory.Services.GetRequiredService<FakeEmailSender>().LastBody ?? "", @"\d{6}").Value;
        var html = await (await client.PostAsync("/Account/LoginCode", RegisterTests.Form(new Dictionary<string, string>
        {
            ["email"] = "user@example.com",
            ["code"] = code
        }))).Content.ReadAsStringAsync();
        Assert.That(html, Does.Contain("user@example.com"));
    }

    [Test]
    public async Task Code_Is_Rejected_After_Five_Minutes()
    {
        await using var factory = new WebAppFactory();
        var client = factory.CreateClient();
        await RegisterTests.SeedAccount(factory, "user@example.com", "abc12345");
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WebDb>();
        db.EmailCodes.Add(new EmailCodeRow
        {
            Email = "user@example.com",
            Code = "123456",
            Scene = "login",
            Used = 0,
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1),
            CreatedAt = DateTime.UtcNow.AddMinutes(-6)
        });
        await db.SaveChangesAsync();
        var html = await (await client.PostAsync("/Account/LoginCode", RegisterTests.Form(new Dictionary<string, string>
        {
            ["email"] = "user@example.com",
            ["code"] = "123456"
        }))).Content.ReadAsStringAsync();
        Assert.That(html, Does.Contain("邮箱或验证码错误"));
    }

    [Test]
    public async Task Missing_Account_Uses_Same_Message()
    {
        await using var factory = new WebAppFactory();
        var client = factory.CreateClient();
        var html = await (await client.PostAsync("/Account/LoginCode", RegisterTests.Form(new Dictionary<string, string>
        {
            ["email"] = "none@example.com",
            ["code"] = "000000"
        }))).Content.ReadAsStringAsync();
        Assert.That(html, Does.Contain("邮箱或验证码错误"));
        Assert.That(html, Does.Not.Contain("不存在"));
    }

    [Test]
    public async Task SendCode_Second_Time_Within_60_Seconds()
    {
        await using var factory = new WebAppFactory();
        var client = factory.CreateClient();
        await RegisterTests.SeedAccount(factory, "user@example.com", "abc12345");
        await client.PostAsync("/Account/SendCode", RegisterTests.Form(new Dictionary<string, string>
        {
            ["email"] = "user@example.com",
            ["scene"] = "login"
        }));
        var html = await (await client.PostAsync("/Account/SendCode", RegisterTests.Form(new Dictionary<string, string>
        {
            ["email"] = "user@example.com",
            ["scene"] = "login"
        }))).Content.ReadAsStringAsync();
        Assert.That(html, Does.Contain("发送过于频繁"));
    }
}

public sealed class ResetPasswordTests
{
    [Test]
    public async Task Reset_Changes_Password()
    {
        await using var factory = new WebAppFactory();
        var client = factory.CreateClient();
        await RegisterTests.SeedAccount(factory, "user@example.com", "abc12345");
        await client.PostAsync("/Account/SendCode", RegisterTests.Form(new Dictionary<string, string>
        {
            ["email"] = "user@example.com",
            ["scene"] = "reset"
        }));
        var code = Regex.Match(factory.Services.GetRequiredService<FakeEmailSender>().LastBody ?? "", @"\d{6}").Value;
        await client.PostAsync("/Account/Forgot", RegisterTests.Form(new Dictionary<string, string>
        {
            ["email"] = "user@example.com",
            ["code"] = code,
            ["newPassword"] = "newpass12",
            ["confirm"] = "newpass12"
        }));
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WebDb>();
        var hash = (await db.Accounts.SingleAsync()).PasswordHash;
        Assert.That(hash, Is.Not.EqualTo("newpass12"));
        var oldLogin = await (await client.PostAsync("/Account/Login", RegisterTests.Form(new Dictionary<string, string>
        {
            ["email"] = "user@example.com",
            ["password"] = "abc12345"
        }))).Content.ReadAsStringAsync();
        Assert.That(oldLogin, Does.Contain("邮箱或密码错误"));
        var newLogin = await (await client.PostAsync("/Account/Login", RegisterTests.Form(new Dictionary<string, string>
        {
            ["email"] = "user@example.com",
            ["password"] = "newpass12"
        }))).Content.ReadAsStringAsync();
        Assert.That(newLogin, Does.Contain("user@example.com"));
    }

    [Test]
    public async Task Reset_Unknown_Email_Same_Send_Response()
    {
        await using var factory = new WebAppFactory();
        var client = factory.CreateClient();
        await RegisterTests.SeedAccount(factory, "user@example.com", "abc12345");
        var known = await client.PostAsync("/Account/SendCode", RegisterTests.Form(new Dictionary<string, string>
        {
            ["email"] = "user@example.com",
            ["scene"] = "reset"
        }));
        var unknown = await client.PostAsync("/Account/SendCode", RegisterTests.Form(new Dictionary<string, string>
        {
            ["email"] = "none@example.com",
            ["scene"] = "reset"
        }));
        Assert.That(known.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(unknown.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await unknown.Content.ReadAsStringAsync();
        Assert.That(body, Does.Not.Contain("不存在"));
        var sender = factory.Services.GetRequiredService<FakeEmailSender>();
        Assert.That(sender.LastTo, Is.Not.EqualTo("none@example.com"));
    }

    [Test]
    public async Task Reset_Weak_Password_Rejected()
    {
        await using var factory = new WebAppFactory();
        var client = factory.CreateClient();
        await RegisterTests.SeedAccount(factory, "user@example.com", "abc12345");
        await client.PostAsync("/Account/SendCode", RegisterTests.Form(new Dictionary<string, string>
        {
            ["email"] = "user@example.com",
            ["scene"] = "reset"
        }));
        var code = Regex.Match(factory.Services.GetRequiredService<FakeEmailSender>().LastBody ?? "", @"\d{6}").Value;
        var html = await (await client.PostAsync("/Account/Forgot", RegisterTests.Form(new Dictionary<string, string>
        {
            ["email"] = "user@example.com",
            ["code"] = code,
            ["newPassword"] = "abcdefgh",
            ["confirm"] = "abcdefgh"
        }))).Content.ReadAsStringAsync();
        Assert.That(html, Does.Contain("密码至少 8 位且同时包含字母和数字"));
        var login = await (await client.PostAsync("/Account/Login", RegisterTests.Form(new Dictionary<string, string>
        {
            ["email"] = "user@example.com",
            ["password"] = "abc12345"
        }))).Content.ReadAsStringAsync();
        Assert.That(login, Does.Contain("user@example.com"));
    }
}

public sealed class NoSocialLoginTests
{
    [TestCase("/")]
    [TestCase("/Account/Login")]
    [TestCase("/Account/Register")]
    [TestCase("/Account/Forgot")]
    public async Task Page_Has_No_Social_Login(string path)
    {
        await using var factory = new WebAppFactory();
        var html = (await factory.CreateClient().GetStringAsync(path)).ToLowerInvariant();
        Assert.That(html, Does.Not.Contain("微信"));
        Assert.That(html, Does.Not.Contain("支付宝"));
        Assert.That(html, Does.Not.Contain("weixin"));
        Assert.That(html, Does.Not.Contain("alipay"));
        Assert.That(html, Does.Not.Contain("qq.com"));
    }
}
