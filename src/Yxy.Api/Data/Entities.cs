namespace Yxy.Api.Data;

public sealed class Account
{
    public long Id { get; set; }
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string? Phone { get; set; }
    public int Status { get; set; } = 1;
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class EmailCodeRow
{
    public long Id { get; set; }
    public string Email { get; set; } = "";
    public string Code { get; set; } = "";
    public string Scene { get; set; } = "";
    public int Used { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string? ReqIp { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class AccountLoginLog
{
    public long Id { get; set; }
    public long? AccountId { get; set; }
    public string? Email { get; set; }
    public string Way { get; set; } = "";
    public string? DeviceId { get; set; }
    public string? Ip { get; set; }
    public int Success { get; set; }
    public string? Remark { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class Customer
{
    public long Id { get; set; }
    public string CustomerNo { get; set; } = "";
    public long AccountId { get; set; }
    public string? Name { get; set; }
    public long? DirectProviderId { get; set; }
    public int Status { get; set; } = 1;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class ProviderLevel
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public decimal CommissionRate { get; set; }
    public int SortNo { get; set; }
    public int Status { get; set; } = 1;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class Provider
{
    public long Id { get; set; }
    public string ProviderNo { get; set; } = "";
    public long AccountId { get; set; }
    public string Name { get; set; } = "";
    public long LevelId { get; set; }
    public int Status { get; set; } = 1;
    public DateTime? EffectiveAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
