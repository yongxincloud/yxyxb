using NUnit.Framework;
using YxyXb.Api.Services;

namespace YxyXb.Api.Tests;

public sealed class PasswordHasherTests
{
    [Test]
    public void Hash_Is_Not_Plaintext_And_Verifies()
    {
        IPasswordHasher hasher = new BcryptPasswordHasher();
        var hash = hasher.Hash("abc12345");
        Assert.That(hash, Is.Not.EqualTo("abc12345"));
        Assert.That(hasher.Verify("abc12345", hash), Is.True);
        Assert.That(hasher.Verify("wrong123", hash), Is.False);
    }
}
