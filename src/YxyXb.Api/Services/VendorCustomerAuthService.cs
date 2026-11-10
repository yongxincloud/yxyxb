using Microsoft.EntityFrameworkCore;
using YxyXb.Api.Data;

namespace YxyXb.Api.Services;

public sealed record VendorCustomerRow(
    long Id,
    string CustomerNo,
    string? Name,
    string? Nickname,
    string? CompanyName,
    string? Phone,
    string Email,
    string? InvoiceTitle,
    string? InvoiceTaxNo,
    string? InvoiceEmail,
    string? InvoiceLicense,
    long? DirectProviderId,
    DateTime CreatedAt,
    int Status,
    DateTime? AuthEndTime,
    string? ProductNo,
    string? ProductName,
    int? MaxDevices);

public sealed record CustomerGrantCommand(
    string? Email,
    int? Days,
    DateTime? EndTime,
    string? Reason,
    string? ProductNo,
    int? MaxDevices);

public sealed record CustomerGrantResult(DateTime EndTime);

public sealed record CustomerEditionCommand(
    string? ProductNo,
    string? Reason,
    int? MaxDevices);

public sealed record CustomerEditionResult(string ProductNo, string ProductName, int MaxDevices);

public sealed class VendorCustomerAuthService
{
    private readonly WebDb _db;
    private readonly TimeProvider _time;

    public VendorCustomerAuthService(WebDb db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    public async Task<IReadOnlyList<VendorCustomerRow>> SearchAsync(string? query)
    {
        var needle = (query ?? "").Trim().ToLowerInvariant();
        var data =
            from customer in _db.Customers
            join account in _db.Accounts on customer.AccountId equals account.Id
            select new { Customer = customer, Account = account };

        if (needle.Length > 0)
        {
            data = data.Where(x =>
                (x.Customer.Name ?? "").ToLower().Contains(needle) ||
                (x.Customer.Nickname ?? "").ToLower().Contains(needle) ||
                (x.Customer.CompanyName ?? "").ToLower().Contains(needle) ||
                x.Customer.CustomerNo.ToLower().Contains(needle) ||
                (x.Customer.InvoiceTitle ?? "").ToLower().Contains(needle) ||
                (x.Customer.InvoiceTaxNo ?? "").ToLower().Contains(needle) ||
                (x.Customer.InvoiceEmail ?? "").ToLower().Contains(needle) ||
                x.Account.Email.ToLower().Contains(needle) ||
                (x.Account.Phone ?? "").ToLower().Contains(needle));
        }

        var take = needle.Length > 0 ? 100 : 50;
        var rows = await data.OrderByDescending(x => x.Customer.Id).Take(take)
            .Select(x => new
            {
                x.Customer.Id,
                x.Customer.CustomerNo,
                x.Customer.Name,
                x.Customer.Nickname,
                x.Customer.CompanyName,
                x.Account.Phone,
                x.Account.Email,
                x.Customer.InvoiceTitle,
                x.Customer.InvoiceTaxNo,
                x.Customer.InvoiceEmail,
                x.Customer.InvoiceLicense,
                x.Customer.DirectProviderId,
                x.Customer.CreatedAt,
                x.Customer.Status
            }).ToListAsync();

        var ids = rows.Select(x => x.Id).ToList();
        var auths = await _db.SubscriptionAuths.Where(x => ids.Contains(x.CustomerId))
            .Select(x => new { x.CustomerId, x.ProductId, x.AuthEndTime, x.DeviceLimit })
            .ToListAsync();
        var latest = auths.GroupBy(x => x.CustomerId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.AuthEndTime).First());
        var productIds = latest.Values.Select(x => x.ProductId).Distinct().ToList();
        var products = await _db.Products.Where(x => productIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id);

        var result = new List<VendorCustomerRow>(rows.Count);
        foreach (var row in rows)
        {
            latest.TryGetValue(row.Id, out var auth);
            products.TryGetValue(auth?.ProductId ?? 0, out var product);
            result.Add(new VendorCustomerRow(
                row.Id,
                row.CustomerNo,
                row.Name,
                row.Nickname,
                row.CompanyName,
                row.Phone,
                row.Email,
                row.InvoiceTitle,
                row.InvoiceTaxNo,
                row.InvoiceEmail,
                row.InvoiceLicense,
                row.DirectProviderId,
                row.CreatedAt,
                row.Status,
                auth?.AuthEndTime,
                product?.ProductNo,
                product?.Name,
                product == null ? null : OrderService.DeviceCap(product.ProductNo, product.MaxDevices, auth?.DeviceLimit)));
        }

        return result;
    }

    public async Task<OperationResult<CustomerGrantResult>> GrantAsync(CustomerGrantCommand command)
    {
        var email = (command.Email ?? "").Trim().ToLowerInvariant();
        var account = await _db.Accounts.SingleOrDefaultAsync(x => x.Email == email);
        if (account == null) return OperationResult<CustomerGrantResult>.Fail("账号不存在，后台不能新建账号");
        var customer = await _db.Customers.SingleOrDefaultAsync(x => x.AccountId == account.Id);
        if (customer == null) return OperationResult<CustomerGrantResult>.Fail("账号不存在，后台不能新建账号");

        var productNo = string.IsNullOrWhiteSpace(command.ProductNo)
            ? OrderService.StandardProductNo
            : command.ProductNo.Trim().ToUpperInvariant();
        var product = await _db.Products.SingleOrDefaultAsync(x => x.ProductNo == productNo);
        if (product == null || product.Status != 1) return OperationResult<CustomerGrantResult>.Fail("产品不存在或已下架");
        var limitError = CustomLimitError(product, command.MaxDevices);
        if (limitError != null) return OperationResult<CustomerGrantResult>.Fail(limitError);

        var now = _time.GetUtcNow().UtcDateTime;
        DateTime end;
        if (command.EndTime != null)
        {
            end = command.EndTime.Value;
        }
        else if (command.Days is > 0)
        {
            var latest = await _db.SubscriptionAuths.Where(x => x.CustomerId == customer.Id)
                .OrderByDescending(x => x.AuthEndTime).Select(x => (DateTime?)x.AuthEndTime).FirstOrDefaultAsync();
            var baseTime = latest != null && now <= latest.Value.AddDays(ClientAuthService.GraceDays) ? latest.Value : now;
            end = baseTime.AddDays(command.Days.Value);
        }
        else
        {
            return OperationResult<CustomerGrantResult>.Fail("请填写时长或到期日");
        }

        if (end <= now) return OperationResult<CustomerGrantResult>.Fail("到期日必须晚于现在");
        _db.SubscriptionAuths.Add(new SubscriptionAuth
        {
            CustomerId = customer.Id,
            ProductId = product.Id,
            AuthStartTime = now,
            AuthEndTime = end,
            Status = 1,
            GrantType = 2,
            GrantReason = command.Reason,
            DeviceLimit = product.ProductNo == OrderService.CustomProductNo ? command.MaxDevices : null,
            CreatedAt = now,
            UpdatedAt = now
        });
        _db.OperationLogs.Add(new OperationLog
        {
            Module = "auth",
            Action = "grant",
            AfterValue = email + " " + product.ProductNo + " " + end.ToString("yyyy-MM-dd"),
            CreatedAt = now
        });
        _db.Messages.Add(new SiteMessage
        {
            AccountId = account.Id,
            Title = "授权已发放",
            Content = "可用至 " + end.ToString("yyyy-MM-dd"),
            CreatedAt = now
        });
        await _db.SaveChangesAsync();
        return OperationResult<CustomerGrantResult>.Success(new CustomerGrantResult(end));
    }

    public async Task<OperationResult<CustomerEditionResult>> ChangeEditionAsync(long customerId, CustomerEditionCommand command)
    {
        var customer = await _db.Customers.SingleOrDefaultAsync(x => x.Id == customerId);
        if (customer == null) return OperationResult<CustomerEditionResult>.Fail("客户不存在");
        var productNo = (command.ProductNo ?? "").Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(productNo)) return OperationResult<CustomerEditionResult>.Fail("请选择账号类型");
        var product = await _db.Products.SingleOrDefaultAsync(x => x.ProductNo == productNo);
        if (product == null || product.Status != 1) return OperationResult<CustomerEditionResult>.Fail("产品不存在或已下架");
        var limitError = CustomLimitError(product, command.MaxDevices);
        if (limitError != null) return OperationResult<CustomerEditionResult>.Fail(limitError);

        var auth = await _db.SubscriptionAuths
            .Where(x => x.CustomerId == customer.Id)
            .OrderByDescending(x => x.AuthEndTime)
            .FirstOrDefaultAsync();
        if (auth == null) return OperationResult<CustomerEditionResult>.Fail("客户尚未开通授权，请先增加期限");
        var beforeProduct = await _db.Products.SingleOrDefaultAsync(x => x.Id == auth.ProductId);
        var nextLimit = product.ProductNo == OrderService.CustomProductNo ? command.MaxDevices : null;
        if (beforeProduct != null && beforeProduct.Id == product.Id && auth.DeviceLimit == nextLimit)
            return OperationResult<CustomerEditionResult>.Fail("账号类型未变化");

        var account = await _db.Accounts.SingleOrDefaultAsync(x => x.Id == customer.AccountId);
        var email = account?.Email ?? customer.CustomerNo;
        var before = (beforeProduct?.ProductNo ?? "?") + " " + (beforeProduct?.Name ?? "");
        var after = product.ProductNo + " " + product.Name;
        var now = _time.GetUtcNow().UtcDateTime;
        auth.ProductId = product.Id;
        auth.DeviceLimit = nextLimit;
        auth.UpdatedAt = now;
        if (!string.IsNullOrWhiteSpace(command.Reason)) auth.GrantReason = command.Reason.Trim();
        _db.OperationLogs.Add(new OperationLog
        {
            Module = "customer",
            Action = "edition",
            BeforeValue = email + " " + before,
            AfterValue = email + " " + after,
            CreatedAt = now
        });
        _db.Messages.Add(new SiteMessage
        {
            AccountId = customer.AccountId,
            Title = "账号类型已变更",
            Content = "已调整为 " + product.Name + "（最多 " + OrderService.DeviceCap(product.ProductNo, product.MaxDevices, nextLimit) + " 台设备）",
            CreatedAt = now
        });
        await _db.SaveChangesAsync();
        return OperationResult<CustomerEditionResult>.Success(
            new CustomerEditionResult(product.ProductNo, product.Name, OrderService.DeviceCap(product.ProductNo, product.MaxDevices, nextLimit)));
    }

    public async Task<OperationResult> AssignProviderAsync(long customerId, string? providerNo)
    {
        var customer = await _db.Customers.SingleOrDefaultAsync(x => x.Id == customerId);
        if (customer == null) return OperationResult.Fail("客户不存在");
        long? providerId = null;
        if (!string.IsNullOrWhiteSpace(providerNo))
        {
            var provider = await _db.Providers.SingleOrDefaultAsync(x => x.ProviderNo == providerNo);
            if (provider == null) return OperationResult.Fail("服务商不存在");
            providerId = provider.Id;
        }
        var before = customer.DirectProviderId?.ToString() ?? "";
        customer.DirectProviderId = providerId;
        customer.UpdatedAt = _time.GetUtcNow().UtcDateTime;
        _db.OperationLogs.Add(new OperationLog
        {
            Module = "customer",
            Action = "assign",
            BeforeValue = before,
            AfterValue = providerId?.ToString() ?? "",
            CreatedAt = customer.UpdatedAt
        });
        await _db.SaveChangesAsync();
        return OperationResult.Success();
    }

    public async Task<OperationResult> DeleteCustomerAsync(long customerId)
    {
        var customer = await _db.Customers.SingleOrDefaultAsync(x => x.Id == customerId);
        if (customer == null) return OperationResult.Fail("客户不存在");
        var account = await _db.Accounts.SingleOrDefaultAsync(x => x.Id == customer.AccountId);
        if (await _db.Providers.AnyAsync(x => x.AccountId == customer.AccountId))
            return OperationResult.Fail("该账号是服务商，不能直接删除");

        var orderIds = await _db.Orders.Where(x => x.CustomerId == customer.Id).Select(x => x.Id).ToListAsync();
        var payments = await _db.Payments.Where(x => orderIds.Contains(x.OrderId)).ToListAsync();
        var commissions = await _db.Commissions.Where(x => orderIds.Contains(x.OrderId)).ToListAsync();
        var orders = await _db.Orders.Where(x => x.CustomerId == customer.Id).ToListAsync();
        var auths = await _db.SubscriptionAuths.Where(x => x.CustomerId == customer.Id).ToListAsync();
        var invoices = await _db.InvoiceApplies.Where(x => x.CustomerId == customer.Id).ToListAsync();
        var messages = await _db.Messages.Where(x => x.AccountId == customer.AccountId).ToListAsync();
        var tokens = await _db.AuthTokens.Where(x => x.AccountId == customer.AccountId).ToListAsync();
        var loginLogs = await _db.LoginLogs.Where(x => x.AccountId == customer.AccountId).ToListAsync();
        var applies = await _db.ProviderApplies.Where(x => x.AccountId == customer.AccountId).ToListAsync();
        var email = account?.Email ?? "";
        var codes = string.IsNullOrEmpty(email)
            ? new List<EmailCodeRow>()
            : await _db.EmailCodes.Where(x => x.Email == email).ToListAsync();

        _db.Payments.RemoveRange(payments);
        _db.Commissions.RemoveRange(commissions);
        _db.Orders.RemoveRange(orders);
        _db.SubscriptionAuths.RemoveRange(auths);
        _db.InvoiceApplies.RemoveRange(invoices);
        _db.Messages.RemoveRange(messages);
        _db.AuthTokens.RemoveRange(tokens);
        _db.LoginLogs.RemoveRange(loginLogs);
        _db.ProviderApplies.RemoveRange(applies);
        _db.EmailCodes.RemoveRange(codes);
        _db.Customers.Remove(customer);
        if (account != null) _db.Accounts.Remove(account);
        var now = _time.GetUtcNow().UtcDateTime;
        _db.OperationLogs.Add(new OperationLog
        {
            Module = "customer",
            Action = "delete",
            BeforeValue = email,
            AfterValue = "",
            CreatedAt = now
        });
        await _db.SaveChangesAsync();
        return OperationResult.Success();
    }

    private static string? CustomLimitError(Product product, int? maxDevices)
    {
        if (product.ProductNo != OrderService.CustomProductNo) return null;
        if (maxDevices is not > 0) return "定制版人数要大于 0";
        if (maxDevices > 9999) return "定制版人数最多 9999";
        return null;
    }
}

public sealed record OperationResult(bool Ok, string? Error)
{
    public static OperationResult Success() => new(true, null);
    public static OperationResult Fail(string error) => new(false, error);
}

public sealed record OperationResult<T>(bool Ok, string? Error, T? Value)
{
    public static OperationResult<T> Success(T value) => new(true, null, value);
    public static OperationResult<T> Fail(string error) => new(false, error, default);
}
