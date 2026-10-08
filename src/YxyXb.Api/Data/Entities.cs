namespace YxyXb.Api.Data;

public sealed class AdminUser
{
    public long Id { get; set; }
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

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

public sealed class AuthToken
{
    public long Id { get; set; }
    public long AccountId { get; set; }
    public string TokenHash { get; set; } = "";
    public string? DeviceId { get; set; }
    public DateTime IssuedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public int Revoked { get; set; }
    public DateTime? RevokedAt { get; set; }
}

public sealed class AccountDevice
{
    public long Id { get; set; }
    public long AccountId { get; set; }
    public string DeviceId { get; set; } = "";
    public string? DisplayName { get; set; }
    public DateTime FirstSeenAt { get; set; }
    public DateTime LastSeenAt { get; set; }
    public int Status { get; set; } = 1;
    public DateTime? KickedAt { get; set; }
}

public sealed class ProviderApply
{
    public long Id { get; set; }
    public long AccountId { get; set; }
    public string CompanyName { get; set; } = "";
    public string TaxNo { get; set; } = "";
    public string LicenseFile { get; set; } = "";
    public string ContactName { get; set; } = "";
    public string ContactEmail { get; set; } = "";
    public string ContactPhone { get; set; } = "";
    public int Status { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class InvoiceApply
{
    public long Id { get; set; }
    public string ApplyNo { get; set; } = "";
    public long CustomerId { get; set; }
    public string? LicenseFile { get; set; }
    public decimal Amount { get; set; }
    public string? TitleName { get; set; }
    public string? TaxNo { get; set; }
    public string? Email { get; set; }
    public int Issuer { get; set; } = 1;
    public int Status { get; set; } = 1;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class Product
{
    public long Id { get; set; }
    public string ProductNo { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public decimal? PromoPrice { get; set; }
    public DateTime? PromoStart { get; set; }
    public DateTime? PromoEnd { get; set; }
    public int MaxDevices { get; set; } = 10;
    public int SortOrder { get; set; }
    public int PeriodDays { get; set; } = 365;
    public int Status { get; set; } = 1;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class SubscriptionOrder
{
    public long Id { get; set; }
    public string OrderNo { get; set; } = "";
    public long CustomerId { get; set; }
    public long ProductId { get; set; }
    public int OrderType { get; set; } = 1;
    public decimal Amount { get; set; }
    public long? ProviderId { get; set; }
    public decimal? CommissionRateSnapshot { get; set; }
    public int PayStatus { get; set; }
    public string? PayChannel { get; set; }
    public string? TradeNo { get; set; }
    public DateTime? PayTime { get; set; }
    public DateTime? RefundTime { get; set; }
    public int CommissionCalculated { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public long? InvoiceApplyId { get; set; }
    public int Hidden { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class PaymentRecord
{
    public long Id { get; set; }
    public long OrderId { get; set; }
    public string Channel { get; set; } = "";
    public string TradeNo { get; set; } = "";
    public decimal Amount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class SystemConfigRow
{
    public long Id { get; set; }
    public string CfgKey { get; set; } = "";
    public string CfgValue { get; set; } = "";
}

public sealed class SubscriptionAuth
{
    public long Id { get; set; }
    public long CustomerId { get; set; }
    public long ProductId { get; set; }
    public long? OrderId { get; set; }
    public DateTime AuthStartTime { get; set; }
    public DateTime AuthEndTime { get; set; }
    public int Status { get; set; } = 1;
    public int GrantType { get; set; } = 1;
    public long? GrantBy { get; set; }
    public string? GrantReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class Customer
{
    public long Id { get; set; }
    public string CustomerNo { get; set; } = "";
    public long AccountId { get; set; }
    public string? Name { get; set; }
    public string? Nickname { get; set; }
    public string? CompanyName { get; set; }
    public string? InvoiceTitle { get; set; }
    public string? InvoiceTaxNo { get; set; }
    public string? InvoiceEmail { get; set; }
    public string? InvoiceLicense { get; set; }
    public long? DirectProviderId { get; set; }
    public int Status { get; set; } = 1;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class Settlement
{
    public long Id { get; set; }
    public string SettlementNo { get; set; } = "";
    public long ProviderId { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public decimal Amount { get; set; }
    public int Status { get; set; }
    public string? InvoiceFile { get; set; }
    public string? PayVoucherNo { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class OperationLog
{
    public long Id { get; set; }
    public string OperatorType { get; set; } = "vendor";
    public string Module { get; set; } = "";
    public string Action { get; set; } = "";
    public string? BeforeValue { get; set; }
    public string? AfterValue { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class SiteMessage
{
    public long Id { get; set; }
    public long AccountId { get; set; }
    public string Title { get; set; } = "";
    public string? Content { get; set; }
    public int IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class CommissionDetail
{
    public long Id { get; set; }
    public long OrderId { get; set; }
    public long ProviderId { get; set; }
    public long LevelId { get; set; }
    public decimal CommissionRateSnapshot { get; set; }
    public int EntryType { get; set; } = 1;
    public decimal Amount { get; set; }
    public int Settled { get; set; }
    public long? SettlementId { get; set; }
    public DateTime CreatedAt { get; set; }
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
