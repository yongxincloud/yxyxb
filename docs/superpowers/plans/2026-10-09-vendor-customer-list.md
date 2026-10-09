# 厂商客户列表资料与查询 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 厂商「客户」页展示客户自填的个人信息和开票信息，打开时只列最近 50 条，有关键字时在全部客户中查询并最多返回 100 条。

**Architecture:** 扩展已有 `GET /api/vendor/customers`，按 `q` 在客户表和账号表上做不区分大小写的包含匹配，并附上最晚授权。客户页表格改调这个接口；账号类型下拉仍用 `/api/vendor/board` 的在售产品。不改看板响应。

**Tech Stack:** ASP.NET Core 8、EF Core、SQLite、NUnit、Vue 3、Vitest。

## Global Constraints

- 打开列表只返回最近 50 条，按客户 `Id` 降序。
- `q` 去空白后有内容时，在全部客户中包含匹配，最多 100 条，仍按 `Id` 降序。
- `q` 缺省、空、或只有空白时，与不带 `q` 相同。
- 不接受客户端 `take`。
- 匹配不区分大小写，命中任一字段即可：`Name`、`Nickname`、`CompanyName`、`CustomerNo`、`InvoiceTitle`、`InvoiceTaxNo`、`InvoiceEmail`、账号 `Email`、账号 `Phone`。
- 每行包含：`name`、`nickname`、`companyName`、`phone`、`email`、`invoiceTitle`、`invoiceTaxNo`、`invoiceEmail`、`invoiceLicense`、`id`、`customerNo`、`directProviderId`、`createdAt`、`status`、`authEndTime`、`productNo`、`productName`、`maxDevices`。
- `invoiceLicense` 只返回已保存的文件名，不返回文件内容。
- `authEndTime` / 产品取该客户 `AuthEndTime` 最晚的一条授权。
- 无匹配时 `{ ok: true, rows: [] }`，HTTP 200。
- 非厂商保持现有拒绝：`NotFound()`。
- 空资料在页面显示「—」。
- 增加期限、改账号类型、删除成功后，按当前搜索条件重新加载客户列表。
- 不修改 `GET /api/vendor/board`，不修改其它厂商页，不修改客户账号信息编辑页。
- 未经用户在对话中明确要求，不执行 `git commit`。下面的提交命令只作报文备忘。

## File Structure

| 文件 | 职责 |
| --- | --- |
| `src/YxyXb.Api/Controllers/VendorController.cs` | 替换 `Customers()`：条数、搜索、资料字段、最晚授权 |
| `tests/YxyXb.Api.Tests/VendorCustomerListTests.cs` | 50 条上限、搜索命中、空白 `q`、非厂商拒绝、搜索最多 100 条 |
| `src/YxyXb.Web/src/views/VendorCustomersView.vue` | 搜索框、资料列、改调列表接口 |
| `src/YxyXb.Web/src/vendor-customers.test.js` | 源码断言：搜索框与资料字段存在 |

---

### Task 1: 客户列表接口

**Files:**
- Create: `tests/YxyXb.Api.Tests/VendorCustomerListTests.cs`
- Modify: `src/YxyXb.Api/Controllers/VendorController.cs`（替换 `Customers()`，约 580–587 行）
- Test: `tests/YxyXb.Api.Tests/VendorCustomerListTests.cs`

**Interfaces:**
- Consumes: `VendorController.VendorKey`、`AccountApi` 不需要；直接用 `WebAppFactory` 和 `WebDb`
- Produces: `GET /api/vendor/customers` 与 `GET /api/vendor/customers?q={keyword}`
  - 200：`{ ok: true, rows: CustomerRow[] }`
  - `CustomerRow` JSON 字段：`id`、`customerNo`、`name`、`nickname`、`companyName`、`phone`、`email`、`invoiceTitle`、`invoiceTaxNo`、`invoiceEmail`、`invoiceLicense`、`directProviderId`、`createdAt`、`status`、`authEndTime`、`productNo`、`productName`、`maxDevices`
  - 非厂商：404

- [ ] **Step 1: Write the failing test**

创建 `tests/YxyXb.Api.Tests/VendorCustomerListTests.cs`：

```csharp
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using YxyXb.Api.Controllers;
using YxyXb.Api.Data;

namespace YxyXb.Api.Tests;

public sealed class VendorCustomerListTests
{
    [Test]
    public async Task Default_List_Returns_Latest_50()
    {
        await using var factory = new WebAppFactory();
        for (var i = 0; i < 51; i++)
            await Seed(factory, "list" + i.ToString("00") + "@example.com");

        var body = await Get(factory, "/api/vendor/customers");
        Assert.That(body!.Rows, Has.Length.EqualTo(50));
        Assert.That(body.Rows.Any(x => x.Email == "list00@example.com"), Is.False);
        Assert.That(body.Rows[0].Email, Is.EqualTo("list50@example.com"));
    }

    [Test]
    public async Task Search_Matches_Profile_And_Account_Fields()
    {
        await using var factory = new WebAppFactory();
        await Seed(factory, "alice@example.com", (account, customer) =>
        {
            account.Phone = "13800001111";
            customer.Name = "ZhangSan";
            customer.Nickname = "Ali";
            customer.CompanyName = "Acme";
            customer.CustomerNo = "NO-ALICE";
            customer.InvoiceTitle = "抬头甲";
            customer.InvoiceTaxNo = "TAX-1";
            customer.InvoiceEmail = "finance@example.com";
            customer.InvoiceLicense = "license.png";
        });
        await Seed(factory, "bob@example.com", (account, customer) =>
        {
            customer.Name = "LiSi";
            customer.CustomerNo = "NO-BOB";
        });

        var byName = await Get(factory, "/api/vendor/customers?q=zhangsan");
        Assert.That(byName!.Rows.Select(x => x.Email), Is.EqualTo(new[] { "alice@example.com" }));
        Assert.That(byName.Rows[0].Name, Is.EqualTo("ZhangSan"));
        Assert.That(byName.Rows[0].InvoiceLicense, Is.EqualTo("license.png"));

        foreach (var q in new[] { "ali", "acme", "NO-ALICE", "抬头甲", "tax-1", "finance@example.com", "alice@example.com", "13800001111" })
        {
            var hit = await Get(factory, "/api/vendor/customers?q=" + Uri.EscapeDataString(q));
            Assert.That(hit!.Rows.Select(x => x.Email), Is.EqualTo(new[] { "alice@example.com" }), q);
        }

        var miss = await Get(factory, "/api/vendor/customers?q=nobody");
        Assert.That(miss!.Ok, Is.True);
        Assert.That(miss.Rows, Is.Empty);
    }

    [Test]
    public async Task Blank_Query_Matches_Default_List()
    {
        await using var factory = new WebAppFactory();
        await Seed(factory, "one@example.com");
        await Seed(factory, "two@example.com");
        var plain = await Get(factory, "/api/vendor/customers");
        var blank = await Get(factory, "/api/vendor/customers?q=%20%20");
        Assert.That(blank!.Rows.Select(x => x.Email), Is.EqualTo(plain!.Rows.Select(x => x.Email)));
    }

    [Test]
    public async Task Search_Returns_At_Most_100()
    {
        await using var factory = new WebAppFactory();
        for (var i = 0; i < 101; i++)
            await Seed(factory, "same" + i.ToString("000") + "@example.com", (_, customer) => customer.Name = "同名");

        var body = await Get(factory, "/api/vendor/customers?q=" + Uri.EscapeDataString("同名"));
        Assert.That(body!.Rows, Has.Length.EqualTo(100));
        Assert.That(body.Rows.Any(x => x.Email == "same000@example.com"), Is.False);
        Assert.That(body.Rows[0].Email, Is.EqualTo("same100@example.com"));
    }

    [Test]
    public async Task List_Uses_Latest_Auth_Product()
    {
        await using var factory = new WebAppFactory();
        await Seed(factory, "auth@example.com");
        DateTime end;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WebDb>();
            var customer = await db.Customers.SingleAsync();
            var older = await db.Products.SingleAsync(x => x.ProductNo == "PERSONAL");
            var newer = await db.Products.SingleAsync(x => x.ProductNo == "PRO");
            var now = DateTime.UtcNow;
            end = now.AddDays(30);
            db.SubscriptionAuths.Add(new SubscriptionAuth
            {
                CustomerId = customer.Id,
                ProductId = older.Id,
                AuthStartTime = now.AddDays(-10),
                AuthEndTime = now.AddDays(10),
                Status = 1,
                CreatedAt = now,
                UpdatedAt = now
            });
            db.SubscriptionAuths.Add(new SubscriptionAuth
            {
                CustomerId = customer.Id,
                ProductId = newer.Id,
                AuthStartTime = now.AddDays(-1),
                AuthEndTime = end,
                Status = 1,
                CreatedAt = now,
                UpdatedAt = now
            });
            await db.SaveChangesAsync();
        }

        var body = await Get(factory, "/api/vendor/customers");
        Assert.That(body!.Rows[0].ProductNo, Is.EqualTo("PRO"));
        Assert.That(body.Rows[0].AuthEndTime, Is.EqualTo(end).Within(TimeSpan.FromSeconds(1)));
    }

    [Test]
    public async Task Customers_Rejects_Non_Vendor()
    {
        await using var factory = new WebAppFactory();
        var response = await factory.CreateClient().GetAsync("/api/vendor/customers");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    static async Task<CustomerListDto?> Get(WebAppFactory factory, string path)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Vendor-Key", VendorController.VendorKey);
        return await client.GetFromJsonAsync<CustomerListDto>(path);
    }

    static async Task Seed(WebAppFactory factory, string email, Action<Account, Customer>? apply = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WebDb>();
        var now = DateTime.UtcNow;
        var account = new Account
        {
            Email = email,
            PasswordHash = "x",
            Status = 1,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        var customer = new Customer
        {
            CustomerNo = "C" + account.Id.ToString("D6"),
            AccountId = account.Id,
            Status = 1,
            CreatedAt = now,
            UpdatedAt = now
        };
        apply?.Invoke(account, customer);
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
    }

    sealed record CustomerListDto(bool Ok, CustomerRowDto[] Rows);
    sealed record CustomerRowDto(string? Email, string? Name, string? InvoiceLicense, string? ProductNo, DateTime? AuthEndTime);
}
```

- [ ] **Step 2: Run test to verify it fails**

在 `D:\AICode\Cursor\yxyxb` 运行：

```powershell
dotnet test tests/YxyXb.Api.Tests/YxyXb.Api.Tests.csproj --filter "FullyQualifiedName~VendorCustomerListTests.Default_List_Returns_Latest_50" --nologo
```

Expected: FAIL。现有接口 `Take(100)`，51 个客户会返回 51 条，断言长度为 50 失败。

- [ ] **Step 3: Write minimal implementation**

把 `VendorController.Customers()` 整段换成：

```csharp
[HttpGet("customers")]
public async Task<IActionResult> Customers([FromQuery] string? q)
{
    if (!Vendor()) return NotFound();
    var needle = (q ?? "").Trim().ToLowerInvariant();
    var query = _db.Customers.Join(
        _db.Accounts,
        customer => customer.AccountId,
        account => account.Id,
        (customer, account) => new { Customer = customer, Account = account });
    if (needle.Length > 0)
    {
        query = query.Where(x =>
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
    var rows = await query.OrderByDescending(x => x.Customer.Id).Take(take)
        .Select(x => new
        {
            x.Customer.Id,
            x.Customer.CustomerNo,
            x.Customer.Name,
            x.Customer.Nickname,
            x.Customer.CompanyName,
            x.Account.Phone,
            Email = x.Account.Email,
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
        .Select(x => new { x.CustomerId, x.ProductId, x.AuthEndTime })
        .ToListAsync();
    var latest = auths.GroupBy(x => x.CustomerId)
        .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.AuthEndTime).First());
    var productIds = latest.Values.Select(x => x.ProductId).Distinct().ToList();
    var products = await _db.Products.Where(x => productIds.Contains(x.Id))
        .ToDictionaryAsync(x => x.Id);

    return Ok(new
    {
        ok = true,
        rows = rows.Select(x =>
        {
            latest.TryGetValue(x.Id, out var auth);
            products.TryGetValue(auth?.ProductId ?? 0, out var product);
            return new
            {
                x.Id,
                x.CustomerNo,
                x.Name,
                x.Nickname,
                x.CompanyName,
                x.Phone,
                email = x.Email,
                x.InvoiceTitle,
                x.InvoiceTaxNo,
                x.InvoiceEmail,
                x.InvoiceLicense,
                x.DirectProviderId,
                x.CreatedAt,
                x.Status,
                authEndTime = auth == null ? (DateTime?)null : auth.AuthEndTime,
                productNo = product?.ProductNo,
                productName = product?.Name,
                maxDevices = product?.MaxDevices
            };
        })
    });
}
```

文件顶部已有 `using Microsoft.EntityFrameworkCore;`。不要改 `Board()`。

- [ ] **Step 4: Run test to verify it passes**

```powershell
dotnet test tests/YxyXb.Api.Tests/YxyXb.Api.Tests.csproj --filter "FullyQualifiedName~VendorCustomerListTests" --nologo
```

Expected: PASS，6 个测试全部通过。

- [ ] **Step 5: Commit**

仅当用户明确要求提交时运行。建议只纳入本任务文件：

```powershell
git add tests/YxyXb.Api.Tests/VendorCustomerListTests.cs src/YxyXb.Api/Controllers/VendorController.cs
git commit -m "feat: let vendor customer list search profile fields"
```

---

### Task 2: 客户页搜索与资料列

**Files:**
- Create: `src/YxyXb.Web/src/vendor-customers.test.js`
- Modify: `src/YxyXb.Web/src/views/VendorCustomersView.vue`
- Test: `src/YxyXb.Web/src/vendor-customers.test.js`

**Interfaces:**
- Consumes: Task 1 的 `GET /api/vendor/customers` 与 `?q=`。看板仍提供 `board.products`（`status === 1` 的产品，含 `productNo`、`name`、`maxDevices`）。行字段与 Task 1 的 `CustomerRow` 相同。`day`、`periodText` 来自 `src/YxyXb.Web/src/vendorBoard.js`。`get` / `post` 来自 `src/YxyXb.Web/src/api.js`。
- Produces: 客户页在表格上方提供搜索；空关键字请求 `/api/vendor/customers`；有关键字请求 `/api/vendor/customers?q=` + `encodeURIComponent`。无结果且关键字非空时显示「没有匹配的客户」。

- [ ] **Step 1: Write the failing test**

创建 `src/YxyXb.Web/src/vendor-customers.test.js`：

```javascript
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { expect, test } from 'vitest'

const root = dirname(fileURLToPath(import.meta.url))

test('vendor customer list shows profile fields and searches the api', () => {
  const page = readFileSync(join(root, 'views/VendorCustomersView.vue'), 'utf8')
  expect(page).toContain('/api/vendor/customers')
  expect(page).toContain('encodeURIComponent')
  expect(page).toContain('姓名、邮箱、手机、公司、抬头')
  expect(page).toContain('查询')
  expect(page).toContain('姓名')
  expect(page).toContain('昵称')
  expect(page).toContain('公司')
  expect(page).toContain('手机')
  expect(page).toContain('抬头')
  expect(page).toContain('税号')
  expect(page).toContain('收票邮箱')
  expect(page).toContain('执照文件名')
  expect(page).toContain('没有匹配的客户')
  expect(page).toContain('—')
})
```

- [ ] **Step 2: Run test to verify it fails**

在 `D:\AICode\Cursor\yxyxb\src\YxyXb.Web` 运行：

```powershell
npm test -- src/vendor-customers.test.js
```

Expected: FAIL。当前页面没有「没有匹配的客户」或搜索占位文案。

- [ ] **Step 3: Write minimal implementation**

用下面内容覆盖 `src/YxyXb.Web/src/views/VendorCustomersView.vue`：

```vue
<script setup>
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { get, post } from '../api.js'
import { day, periodText, useVendorBoard } from '../vendorBoard.js'

const { board, loadError, load } = useVendorBoard()
const days = ref(365)
const reason = ref('手工开通')
const grantProductNo = ref('STANDARD')
const message = ref('')
const busyId = ref(0)
const editionDraft = reactive({})
const keyword = ref('')
const rows = ref([])
const listError = ref('')

const editionOptions = computed(() =>
  (board.value?.products || []).filter((p) => p.status === 1)
)

function text(value) {
  const shown = String(value ?? '').trim()
  return shown || '—'
}

onMounted(async () => {
  await load()
  if (board.value) await loadCustomers()
})

watch(rows, (list) => {
  for (const row of list || []) {
    editionDraft[row.id] = row.productNo || 'STANDARD'
  }
})

async function loadCustomers() {
  listError.value = ''
  const q = keyword.value.trim()
  const path = q ? '/api/vendor/customers?q=' + encodeURIComponent(q) : '/api/vendor/customers'
  try {
    const data = await get(path)
    if (!data.ok) {
      listError.value = data.error || '暂时读不到客户'
      rows.value = []
      return
    }
    rows.value = data.rows || []
  } catch {
    listError.value = '暂时读不到客户'
    rows.value = []
  }
}

async function remove(row) {
  message.value = ''
  if (!window.confirm('删除 ' + row.email + ' ？账号、订单和授权会一起去掉，不能恢复。')) return
  busyId.value = row.id
  const data = await post('/api/vendor/customers/' + row.id + '/delete', {})
  busyId.value = 0
  message.value = data.ok ? '已删除 ' + row.email : (data.error || '删除失败')
  if (data.ok) await loadCustomers()
}

async function extend(row) {
  message.value = ''
  const count = Number(days.value)
  if (!count || count < 1) {
    message.value = '天数要大于 0'
    return
  }
  busyId.value = row.id
  const data = await post('/api/vendor/grants', {
    email: row.email,
    days: count,
    reason: reason.value || '手工开通',
    productNo: grantProductNo.value || 'STANDARD'
  })
  busyId.value = 0
  message.value = data.ok
    ? row.email + ' 可用至 ' + day(data.authEndTime)
    : (data.error || '增加失败')
  if (data.ok) await loadCustomers()
}

async function changeEdition(row) {
  message.value = ''
  const productNo = editionDraft[row.id]
  if (!productNo) {
    message.value = '请选择账号类型'
    return
  }
  if (productNo === row.productNo) {
    message.value = '账号类型未变化'
    return
  }
  const label = editionOptions.value.find((p) => p.productNo === productNo)?.name || productNo
  if (!window.confirm('将 ' + row.email + ' 改为「' + label + '」？')) return
  busyId.value = row.id
  const data = await post('/api/vendor/customers/' + row.id + '/edition', {
    productNo,
    reason: reason.value || '后台改版'
  })
  busyId.value = 0
  message.value = data.ok
    ? row.email + ' 已改为 ' + (data.productName || productNo)
    : (data.error || '修改失败')
  if (data.ok) await loadCustomers()
}
</script>

<template>
  <p class="err" v-if="loadError">{{ loadError }}</p>
  <section v-if="board" class="card-yxy">
    <h1>客户</h1>
    <p class="lead">注册后的账号会出现在这里。支付未接通时，填写天数后点该行的「增加期限」。可单独修改账号类型（个人/标准/专业）。</p>
    <p v-if="message" :class="message.includes('失败') || message.includes('要大于') || message.includes('未变化') || message.includes('请选择') ? 'err' : 'ok'">{{ message }}</p>
    <div class="row-form">
      <label>增加天数<input class="field-yxy" v-model="days" type="number" min="1" /></label>
      <label>开通类型
        <select class="field-yxy" v-model="grantProductNo">
          <option v-for="p in editionOptions" :key="p.productNo" :value="p.productNo">
            {{ p.name }}（{{ p.maxDevices }} 台）
          </option>
        </select>
      </label>
      <label>事由<input class="field-yxy reason" v-model="reason" /></label>
    </div>
    <form class="row-form" @submit.prevent="loadCustomers">
      <label>查询<input class="field-yxy" v-model="keyword" placeholder="姓名、邮箱、手机、公司、抬头" /></label>
      <button class="btn-yxy" type="submit">查询</button>
    </form>
    <p class="err" v-if="listError">{{ listError }}</p>
    <table>
      <thead>
        <tr>
          <th>个人信息</th>
          <th>邮箱</th>
          <th>客户号</th>
          <th>开票信息</th>
          <th>账号类型</th>
          <th>归属</th>
          <th>注册日</th>
          <th>到期日</th>
          <th>状态</th>
          <th></th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="row in rows" :key="row.id">
          <td class="stack">
            <div>姓名 {{ text(row.name) }}</div>
            <div>昵称 {{ text(row.nickname) }}</div>
            <div>公司 {{ text(row.companyName) }}</div>
            <div>手机 {{ text(row.phone) }}</div>
          </td>
          <td>{{ row.email }}</td>
          <td>{{ row.customerNo }}</td>
          <td class="stack">
            <div>抬头 {{ text(row.invoiceTitle) }}</div>
            <div>税号 {{ text(row.invoiceTaxNo) }}</div>
            <div>收票邮箱 {{ text(row.invoiceEmail) }}</div>
            <div>执照文件名 {{ text(row.invoiceLicense) }}</div>
          </td>
          <td>
            <div class="edition">
              <select class="field-yxy" v-model="editionDraft[row.id]">
                <option v-for="p in editionOptions" :key="p.productNo" :value="p.productNo">
                  {{ p.name }}（{{ p.maxDevices }} 台）
                </option>
              </select>
              <button class="btn-yxy mini" type="button" :disabled="busyId === row.id" @click="changeEdition(row)">修改</button>
            </div>
            <span class="muted" v-if="!row.productNo">尚未开通</span>
          </td>
          <td>{{ row.directProviderId || '厂商' }}</td>
          <td>{{ day(row.createdAt) }}</td>
          <td>{{ day(row.authEndTime) }}</td>
          <td>{{ periodText(row.authEndTime) }}</td>
          <td>
            <button class="btn-yxy mini" type="button" :disabled="busyId === row.id" @click="extend(row)">增加期限</button>
            <button class="btn-yxy mini" type="button" :disabled="busyId === row.id" @click="remove(row)">删除</button>
          </td>
        </tr>
        <tr v-if="!rows.length"><td colspan="10">{{ keyword.trim() ? '没有匹配的客户' : '暂无客户。用户先在网站注册，再回到这里开通。' }}</td></tr>
      </tbody>
    </table>
  </section>
</template>

<style scoped>
.edition { display: flex; gap: 8px; align-items: center; flex-wrap: wrap; }
.edition .field-yxy { min-width: 140px; }
.muted { display: block; margin-top: 4px; color: var(--c-text-muted); font-size: 12px; }
.row-form { display: flex; flex-wrap: wrap; gap: 12px; margin-bottom: 12px; align-items: end; }
.row-form label { display: flex; flex-direction: column; gap: 4px; font-size: 13px; }
.reason { min-width: 180px; }
.stack { font-size: 13px; line-height: 1.5; white-space: nowrap; }
</style>
```

- [ ] **Step 4: Run test to verify it passes**

在 `D:\AICode\Cursor\yxyxb\src\YxyXb.Web` 运行：

```powershell
npm test -- src/vendor-customers.test.js
```

Expected: PASS。

- [ ] **Step 5: Commit**

仅当用户明确要求提交时运行：

```powershell
git add src/YxyXb.Web/src/views/VendorCustomersView.vue src/YxyXb.Web/src/vendor-customers.test.js
git commit -m "feat: show customer profile fields on the vendor list"
```
