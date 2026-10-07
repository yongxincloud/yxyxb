# 三档版本与设备上限 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将用心云备份授权改为个人/标准/专业三档（默认设备上限 3/10/50、原价 36/98/198），支持后台改价与限期促销、登录占坑与踢出、周期内补差升级，以及首页三列营销价。

**Architecture:** 在现有 `Yxy.Api` 单体上扩展 `Products`、新增 `AccountDevices` 与 `ProductPricing` / `DeviceRegistryService`；`ClientAuthService` 正式授权时占坑；`OrderService` 支持现价下单与 `orderType=3` 升级补差；`Yxy.Web` 首页三列 + 门户设备/升级 + 厂商产品编辑；`Yxy.Client` 处理 `DEVICE_LIMIT` 并提供设备列表踢出。

**Tech Stack:** ASP.NET Core 8、EF Core SQLite、NUnit（`tests/Yxy.Api.Tests`）、Vue 3 + Vitest（`src/Yxy.Web`）、.NET Framework 4.8 客户端（`Yxy.Client`）。

## Global Constraints

- 功能三档相同；只差 `MaxDevices` 与价格（见 spec §1）。
- 正式订单周期仍 365 天；试用仍每客户最多 2 次、30 天、0.10/0.50 元且**不限台**。
- 登录占坑、退出不释放、主动踢出才释放；踢出立刻废该 `deviceId` 的 token。
- 升级：剩余整天数 × (新现价 − 旧现价) / 365，到期日不变；降级仅续费时。
- 改价/促销只影响新订单；订单金额快照不变。
- 公开价：`GET /api/public/price` → `{ ok, plans: [...] }`（破坏性变更，无存量前端依赖）。
- 产品号种子：`PERSONAL` / `STANDARD` / `PRO`；废弃 `STD`。
- 超员（含后台调小 MaxDevices）：不自动踢；禁新坑。
- 宽限期与正式期同样限台。
- Spec：`docs/superpowers/specs/2026-10-07-edition-device-limits-design.md`

## File Structure

| 文件 | 职责 |
| --- | --- |
| `src/Yxy.Api/Data/Entities.cs` | `Product` 促销/MaxDevices；新实体 `AccountDevice` |
| `src/Yxy.Api/Data/WebDb.cs` | `DbSet<AccountDevice>` |
| `src/Yxy.Api/Program.cs` | SQLite 迁移 ALTER / CREATE `AccountDevices` |
| `src/Yxy.Api/Services/ProductPricing.cs` | 现价与促销判定（纯函数优先） |
| `src/Yxy.Api/Services/DeviceRegistryService.cs` | 占坑 / 列表 / 踢出 / 废 token |
| `src/Yxy.Api/Services/OrderService.cs` | 三档种子、现价、升级单、支付改档 |
| `src/Yxy.Api/Services/ClientAuthService.cs` | 空 deviceId 拒绝；正式限台；会话带回档信息 |
| `src/Yxy.Api/Controllers/PortalController.cs` | `plans` 公开价；门户设备 API；订阅带回档 |
| `src/Yxy.Api/Controllers/OrderController.cs` | 升级预览/下单；价目 |
| `src/Yxy.Api/Controllers/VendorController.cs` | 产品改价/促销/MaxDevices |
| `src/Yxy.Api/Controllers/ClientController.cs` | 客户端设备列表/踢出；登录错误码 |
| `tests/Yxy.Api.Tests/*` | 定价、设备、升级、改价快照；`STD`→`STANDARD` |
| `src/Yxy.Web/src/views/HomeView.vue` | 三列定价 |
| `src/Yxy.Web/src/views/OrdersView.vue` | 选档购买/升级 |
| `src/Yxy.Web/src/views/VendorProductsView.vue` | 促销与 MaxDevices |
| `src/Yxy.Web/src/views/PortalDevicesView.vue`（或并入 Portal） | 我的设备 |
| `src/Yxy.Client/SiteLogin.cs` + 设备页/对话框 | `DEVICE_LIMIT` 与踢设备 |

---

### Task 1: Product 字段、种子与 ProductPricing

**Files:**
- Create: `src/Yxy.Api/Services/ProductPricing.cs`
- Create: `tests/Yxy.Api.Tests/ProductPricingTests.cs`
- Modify: `src/Yxy.Api/Data/Entities.cs`（`Product`）
- Modify: `src/Yxy.Api/Program.cs`（ALTER Products 列）
- Modify: `src/Yxy.Api/Services/OrderService.cs`（`EnsureCatalogAsync`、常量）
- Modify: `tests/Yxy.Api.Tests/OrderTests.cs`（及所有仍写 `STD` / `12m` 的 API 测试，本任务内全部改为 `STANDARD` / `98m`，避免红灯）

**Interfaces:**
- Consumes: `Product`、`DateTime utcNow`
- Produces:
  - `OrderService.PersonalProductNo = "PERSONAL"`、`StandardProductNo = "STANDARD"`、`ProProductNo = "PRO"`
  - `ProductPricing.SalePrice(Product p, DateTime utcNow) => decimal`
  - `ProductPricing.IsPromoActive(Product p, DateTime utcNow) => bool`
  - `EnsureCatalogAsync` 保证三档存在：MaxDevices 3/10/50，Price 36/98/198，SortOrder 1/2/3；若存在旧 `STD` 则 `Status=0` 下架

- [ ] **Step 1: Write the failing test**

```csharp
// tests/Yxy.Api.Tests/ProductPricingTests.cs
[Test]
public void SalePrice_Uses_Promo_Inside_Window()
{
    var now = new DateTime(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);
    var p = new Product
    {
        Price = 98m,
        PromoPrice = 49m,
        PromoStart = now.AddDays(-1),
        PromoEnd = now.AddDays(1)
    };
    Assert.That(ProductPricing.SalePrice(p, now), Is.EqualTo(49m));
    Assert.That(ProductPricing.IsPromoActive(p, now), Is.True);
}

[Test]
public void SalePrice_Falls_Back_Outside_Window()
{
    var now = new DateTime(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);
    var p = new Product
    {
        Price = 98m,
        PromoPrice = 49m,
        PromoStart = now.AddDays(-10),
        PromoEnd = now.AddDays(-1)
    };
    Assert.That(ProductPricing.SalePrice(p, now), Is.EqualTo(98m));
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Yxy.Api.Tests/Yxy.Api.Tests.csproj --filter FullyQualifiedName~ProductPricingTests`  
Working directory: repo root `yongxinyunbak`  
Expected: FAIL（类型/方法不存在）

- [ ] **Step 3: Write minimal implementation**

在 `Product` 增加：`decimal? PromoPrice`、`DateTime? PromoStart`、`DateTime? PromoEnd`、`int MaxDevices`、`int SortOrder`。

`ProductPricing.cs`：

```csharp
namespace Yxy.Api.Services;

public static class ProductPricing
{
    public static bool IsPromoActive(Product p, DateTime utcNow) =>
        p.PromoPrice is > 0
        && p.PromoStart is DateTime s
        && p.PromoEnd is DateTime e
        && utcNow >= s && utcNow <= e;

    public static decimal SalePrice(Product p, DateTime utcNow) =>
        IsPromoActive(p, utcNow) ? p.PromoPrice!.Value : p.Price;
}
```

`Program.cs` 在 EnsureCreated 后执行（SQLite 可重复）：

```sql
ALTER TABLE "Products" ADD COLUMN "PromoPrice" TEXT NULL;
ALTER TABLE "Products" ADD COLUMN "PromoStart" TEXT NULL;
ALTER TABLE "Products" ADD COLUMN "PromoEnd" TEXT NULL;
ALTER TABLE "Products" ADD COLUMN "MaxDevices" INTEGER NOT NULL DEFAULT 10;
ALTER TABLE "Products" ADD COLUMN "SortOrder" INTEGER NOT NULL DEFAULT 0;
```

（用 try/catch 或查询 `pragma table_info` 跳过已存在列，与项目现有迁移风格一致。）

`EnsureCatalogAsync`：为三档 upsert；下架 `STD`。

全局替换测试里的 `productNo = "STD"` → `"STANDARD"`，金额 `12m` → `98m`（凡依赖默认标准价的用例）。

- [ ] **Step 4: Run tests**

Run: `dotnet test tests/Yxy.Api.Tests/Yxy.Api.Tests.csproj`  
Expected: PASS（含 ProductPricing 与改写后的订单测试）

- [ ] **Step 5: Commit**

```bash
git add src/Yxy.Api/Services/ProductPricing.cs src/Yxy.Api/Data/Entities.cs src/Yxy.Api/Program.cs src/Yxy.Api/Services/OrderService.cs tests/Yxy.Api.Tests/
git commit -m "feat: add three product tiers and promo sale price helper"
```

---

### Task 2: 公开价目 `plans` 与正式单按现价

**Files:**
- Modify: `src/Yxy.Api/Controllers/PortalController.cs`（`Price`）
- Modify: `src/Yxy.Api/Controllers/OrderController.cs`（catalog 响应）
- Modify: `src/Yxy.Api/Services/OrderService.cs`（`CreateAsync` 正式单 `amount = ProductPricing.SalePrice`）
- Create: `tests/Yxy.Api.Tests/PublicPriceTests.cs`
- Modify: `tests/Yxy.Api.Tests/OrderTests.cs`（促销价下单断言）

**Interfaces:**
- Consumes: `ProductPricing`、`EnsureCatalogAsync`
- Produces: `GET /api/public/price` → `{ ok: true, plans: [{ productNo, name, maxDevices, listPrice, salePrice, promoActive, promoEnd, sortOrder }] }`；`CreateAsync` orderType 1 使用现价

- [ ] **Step 1: Write the failing test**

```csharp
[Test]
public async Task Public_Price_Returns_Three_Plans()
{
    await using var factory = new WebAppFactory();
    var client = factory.CreateClient();
    var json = await client.GetFromJsonAsync<PriceDto>("/api/public/price");
    Assert.That(json!.Ok, Is.True);
    Assert.That(json.Plans.Length, Is.EqualTo(3));
    Assert.That(json.Plans.Select(p => p.ProductNo), Is.EquivalentTo(new[] { "PERSONAL", "STANDARD", "PRO" }));
    Assert.That(json.Plans.Single(p => p.ProductNo == "STANDARD").ListPrice, Is.EqualTo(98m));
}
```

另加：设 STANDARD 促销后 `CreateAsync` 金额等于促销价。

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Yxy.Api.Tests --filter FullyQualifiedName~Public_Price_Returns_Three_Plans`  
Expected: FAIL（仍返回单字段 `price`）

- [ ] **Step 3: Implement**

`PortalController.Price` 查询 `Status==1` 产品按 `SortOrder`，映射 `plans`。  
`OrderController` catalog 同步返回 `plans`（门户下单页用）。  
`CreateAsync`：`orderType==1` 时 `amount = ProductPricing.SalePrice(product, now)`。

- [ ] **Step 4: Run tests**

Run: `dotnet test tests/Yxy.Api.Tests`  
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/Yxy.Api/Controllers/PortalController.cs src/Yxy.Api/Controllers/OrderController.cs src/Yxy.Api/Services/OrderService.cs tests/Yxy.Api.Tests/
git commit -m "feat: expose plan list pricing and charge sale price"
```

---

### Task 3: 厂商后台改价 / 促销 / MaxDevices

**Files:**
- Modify: `src/Yxy.Api/Controllers/VendorController.cs`
- Modify: `src/Yxy.Web/src/views/VendorProductsView.vue`
- Create: `tests/Yxy.Api.Tests/VendorProductTests.cs`

**Interfaces:**
- Consumes: `Product`、`OperationLog`
- Produces: `POST /api/vendor/products/{productNo}` body：

```csharp
public sealed record ProductUpdateBody(
    decimal? Price,
    decimal? PromoPrice,
    DateTime? PromoStart,
    DateTime? PromoEnd,
    int? MaxDevices,
    int? SortOrder,
    int? Status);
```

校验：`Price>0`；若设 `PromoPrice` 则起止齐全且 Start&lt;End；`MaxDevices>=1`。写 `operation_log`（module=`product`，action=`update`，前后 JSON 摘要）。保留旧 `POST .../price` 改为内部调同一逻辑或标记委托到新接口。Overview 的 `products` 投影带上新字段。

- [ ] **Step 1: Write the failing test**

```csharp
[Test]
public async Task Vendor_Can_Set_Promo_And_MaxDevices()
{
    // vendor login helper（复用 AdminLoginTests 模式）
    // POST /api/vendor/products/STANDARD { price:98, promoPrice:50, promoStart, promoEnd, maxDevices:12 }
    // GET /api/public/price → STANDARD.salePrice == 50, maxDevices == 12
}
```

- [ ] **Step 2: Run → FAIL**

- [ ] **Step 3: Implement API + VendorProductsView 表单**（原价、促销价、起止、MaxDevices、保存）

- [ ] **Step 4: `dotnet test tests/Yxy.Api.Tests` → PASS**

- [ ] **Step 5: Commit**

```bash
git commit -m "feat: vendor can edit promo window and max devices"
```

---

### Task 4: AccountDevices + DeviceRegistryService

**Files:**
- Modify: `src/Yxy.Api/Data/Entities.cs`（`AccountDevice`）
- Modify: `src/Yxy.Api/Data/WebDb.cs`
- Modify: `src/Yxy.Api/Program.cs`（CREATE TABLE）
- Create: `src/Yxy.Api/Services/DeviceRegistryService.cs`
- Create: `tests/Yxy.Api.Tests/DeviceRegistryTests.cs`
- Modify: `src/Yxy.Api/Program.cs` DI：`AddScoped<DeviceRegistryService>()`

**Interfaces:**
- Consumes: `WebDb`、`TimeProvider`
- Produces:

```csharp
public sealed class DeviceRegistryService
{
    public Task<DeviceOccupyResult> TryOccupyAsync(long accountId, string? deviceId, int? maxDevices, bool enforceLimit, string? displayName = null);
    public Task<IReadOnlyList<DeviceRow>> ListActiveAsync(long accountId);
    public Task<bool> KickAsync(long accountId, string deviceId); // 废该 device 的 AuthTokens
}

public sealed record DeviceOccupyResult(bool Ok, string? ErrorCode, string? Error, int DeviceCount, int? MaxDevices);
// ErrorCode: DEVICE_ID_REQUIRED | DEVICE_LIMIT
public sealed record DeviceRow(string DeviceId, string? DisplayName, DateTime FirstSeenAt, DateTime LastSeenAt);
```

规则：`deviceId` 空白 → `DEVICE_ID_REQUIRED`。`enforceLimit==false`（试用）只 upsert 占用、不数上限。`enforceLimit==true`：已有 Status=1 同 device → 更新 LastSeen；否则若 count>=max → `DEVICE_LIMIT`；否则插入。`KickAsync`：Status=0、KickedAt=now，并将该 AccountId+DeviceId 的 `AuthTokens.Revoked=1`。

- [ ] **Step 1: Write unit/integration tests**（WebAppFactory 或直接 new service + SQLite）覆盖：占满拒绝、踢出后再占、同机重登不增计数

- [ ] **Step 2: Run → FAIL**

- [ ] **Step 3: Implement entity + table + service**

```sql
CREATE TABLE IF NOT EXISTS "AccountDevices" (
  "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
  "AccountId" INTEGER NOT NULL,
  "DeviceId" TEXT NOT NULL,
  "DisplayName" TEXT NULL,
  "FirstSeenAt" TEXT NOT NULL,
  "LastSeenAt" TEXT NOT NULL,
  "Status" INTEGER NOT NULL,
  "KickedAt" TEXT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_AccountDevices_Account_Device" ON "AccountDevices" ("AccountId", "DeviceId");
```

- [ ] **Step 4: Tests PASS**

- [ ] **Step 5: Commit**

```bash
git commit -m "feat: add account device registry with kick revoke"
```

---

### Task 5: ClientAuth 接入限台

**Files:**
- Modify: `src/Yxy.Api/Services/ClientAuthService.cs`
- Modify: `src/Yxy.Api/Controllers/ClientController.cs`（失败时返回 `errorCode`）
- Create: `tests/Yxy.Api.Tests/DeviceLimitAuthTests.cs`
- Modify: 现有 `ClientAuthTests`（登录须带 deviceId；Grant 需绑定带 MaxDevices 的产品）

**Interfaces:**
- Consumes: `DeviceRegistryService`、`SubscriptionAuth`+`Product`+订单类型
- Produces: 扩展 `ClientSession`：

```csharp
public sealed record ClientSession(
    bool Ok, string? Error, bool Valid, string? Token,
    int? AccountStatus, string? AuthStatus, DateTime? AuthEndTime, int? NextCheckDays,
    string? Name = null, string? Nickname = null, string? CompanyName = null,
    string? ErrorCode = null,
    string? ProductNo = null,
    int? MaxDevices = null,
    int? DeviceCount = null);
```

逻辑（在 `IssueAsync` 发 token 前）：
1. `deviceId` 空 → Fail(`DEVICE_ID_REQUIRED`, "缺少设备标识")
2. 取当前有效授权行 + Product；若授权关联订单 `OrderType==2` → `enforceLimit=false`
3. 否则 `enforceLimit=true`，`maxDevices = product.MaxDevices`
4. `TryOccupyAsync`；失败则不发 token，`ErrorCode=DEVICE_LIMIT`
5. 成功则会话带上 `productNo/maxDevices/deviceCount`

`ClientController` 登录失败时：

```csharp
return BadRequest(new { ok = false, error = session.Error, errorCode = session.ErrorCode });
```

满员建议 HTTP 409：

```csharp
if (session.ErrorCode == "DEVICE_LIMIT")
    return Conflict(new { ok = false, error = session.Error, errorCode = "DEVICE_LIMIT", maxDevices = session.MaxDevices, deviceCount = session.DeviceCount });
```

- [ ] **Step 1: Test** — PERSONAL MaxDevices=3，登录 3 台成功，第 4 台 409；试用订单授权第 4 台成功

- [ ] **Step 2: Run → FAIL**

- [ ] **Step 3: Implement**

- [ ] **Step 4: `dotnet test tests/Yxy.Api.Tests` → PASS**

- [ ] **Step 5: Commit**

```bash
git commit -m "feat: enforce device limits on client login"
```

---

### Task 6: 门户与客户端设备列表 / 踢出 API

**Files:**
- Modify: `src/Yxy.Api/Controllers/PortalController.cs`
- Modify: `src/Yxy.Api/Controllers/ClientController.cs`
- Create: `tests/Yxy.Api.Tests/DeviceApiTests.cs`

**Interfaces:**
- `GET /api/portal/devices` → `{ ok, maxDevices, devices: DeviceRow[] }`
- `POST /api/portal/devices/kick` body `{ deviceId }` → `{ ok }`
- `GET /api/client/devices` + `POST /api/client/devices/kick`（客户端 token 鉴权，与现有 client token 机制一致；若目前 client 无 Authorize，则用 body token 查 `AuthTokens` 解析 accountId，与 verify 相同风格）

踢出后：该 device 再 `verify` 失败。

- [ ] **Step 1–4: TDD 实现并跑通**

- [ ] **Step 5: Commit**

```bash
git commit -m "feat: portal and client device list and kick APIs"
```

---

### Task 7: 升级补差订单与续费降档校验

**Files:**
- Modify: `src/Yxy.Api/Services/OrderService.cs`
- Modify: `src/Yxy.Api/Controllers/OrderController.cs`
- Create: `tests/Yxy.Api.Tests/UpgradeOrderTests.cs`

**Interfaces:**
- Consumes: `ProductPricing`、`DeviceRegistryService.ListActiveAsync`
- Produces:
  - `OrderService.PreviewUpgradeAsync(accountId, targetProductNo) => (ok, amount, daysLeft, error)`
  - `CreateAsync` 允许 `orderType==3`；金额=预览差价；佣金按 type 3 也写（与正式相同条件）
  - `MarkPaidAsync`：若 `order.OrderType==3`，找到客户当前有效 `SubscriptionAuth`，`ProductId = order.ProductId`，**不改** `AuthEndTime`，不新增授权行；`NextEndAsync` 不调用
  - 续费 `orderType==1` 且目标 `MaxDevices` &lt; 当前占用数 → Create 拒绝「请先减少设备到 N 台以内」

升级金额：

```csharp
var daysLeft = Math.Max(0, (int)Math.Floor((authEnd - now).TotalDays));
var delta = ProductPricing.SalePrice(newProduct, now) - ProductPricing.SalePrice(oldProduct, now);
var amount = decimal.Round(daysLeft * delta / 365m, 2, MidpointRounding.AwayFromZero);
if (newProduct.MaxDevices <= oldProduct.MaxDevices) reject;
if (amount <= 0) reject;
```

- [ ] **Step 1: Tests** — 剩余 182 天、旧 36 新 98 → amount ≈ `182*(62)/365`；支付后 ProductId 变、End 不变；降档续费超员拒绝

- [ ] **Step 2–4: 实现并 PASS**

- [ ] **Step 5: Commit**

```bash
git commit -m "feat: prorated upgrade orders and renew downgrade device check"
```

---

### Task 8: Web 首页三列、订单选档、门户设备、订阅展示

**Files:**
- Modify: `src/Yxy.Web/src/views/HomeView.vue`
- Modify: `src/Yxy.Web/src/views/OrdersView.vue`
- Modify: `src/Yxy.Web/src/views/PortalView.vue`（订阅显示当前档与设备数）
- Create or Modify: 设备页 + `router.js`
- Create: `src/Yxy.Web/src/plans.test.js`（断言 Home/Orders 源码含 `plans`、不含「不限台数」）

**Interfaces:**
- Consumes: `/api/public/price` 的 `plans`；门户 devices/kick；升级 preview API（Task 7 暴露的 `GET/POST`）

首页：三列卡片（名称、maxDevices、划线 listPrice、salePrice、购买 CTA）。文案去掉「一个账号不限台数」。  
Orders：下拉/卡片选 `productNo`；有正式授权时显示「升级到…」走 orderType 3。  
门户：设备列表 + 踢出确认。

- [ ] **Step 1: Vitest** 读 `HomeView.vue` 文本含 `plans` / `maxDevices`，不含 `不限台数`

- [ ] **Step 2: `npm test` in `src/Yxy.Web` → FAIL**

- [ ] **Step 3: 实现 UI**

- [ ] **Step 4: PASS**

- [ ] **Step 5: Commit**

```bash
git commit -m "feat: marketing three-column plans and portal devices UI"
```

---

### Task 9: 客户端 DEVICE_LIMIT 与设备管理 UI

**Files:**
- Modify: `src/Yxy.Client/SiteLogin.cs`（解析 `errorCode`；满员提示升级或去设备管理）
- Modify: 设置页或新对话框列出设备并踢出（调用 `/api/client/devices`）
- Modify: 登录成功缓存可存 `maxDevices`/`deviceCount`（若已有 license 存储则扩展字段）

**Interfaces:**
- Consumes: Task 5/6 JSON 形状
- Produces: 满员时中文提示；设备列表可踢（含本机确认）

客户端无 NUnit UI 测试时：用手动验收清单（见下）+ 尽量对 JSON 解析抽小函数并在 `Yxy.Core.Tests` 或 Api 契约测试覆盖。

手动验收：
1. 个人版 3 台满，第 4 台提示  
2. 踢一台后再登成功  
3. 退出再登不占新坑  

- [ ] **Step 1–4: 实现与手工点验**

- [ ] **Step 5: Commit**

```bash
git commit -m "feat: client handles device limit and device kick UI"
```

---

### Task 10: 文档口径修订

**Files:**
- Modify: `Doc/用心云备份-后台系统开发需求文档.md`（§2.1 授权形态、验收「不限台数」、介绍主页文案）
- Modify: `Doc/用心云备份-PRD.md`（登录与授权「不限台数」句）
- 可选：部署手册里 `/api/public/price` 说明改为 `plans`

将「不限台数」改为指向三档设备上限，并链到  
`docs/superpowers/specs/2026-10-07-edition-device-limits-design.md`。

- [ ] **Step 1: 全文检索 `不限台` 并按 spec 改口径**

- [ ] **Step 2: Commit**

```bash
git commit -m "docs: replace unlimited-device license with three editions"
```

---

## Spec Coverage Checklist

| Spec 要求 | Task |
| --- | --- |
| 三档种子 3/10/50、36/98/198 | 1 |
| 促销价窗口与现价 | 1–3 |
| 公开 `plans`、首页三列 | 2、8 |
| 厂商改价/促销/MaxDevices | 3 |
| 占坑/踢出/退出不释放 | 4–6、9 |
| 试用不限台、宽限期限台 | 5 |
| 升级补差、到期日不变 | 7 |
| 续费降档先踢设备 | 7 |
| 订单快照 | 2（既有 Price_Change 测试保留） |
| 废止不限台文档 | 10 |
| 客户端 DEVICE_LIMIT UI | 9 |

## Self-Review Notes

- 无 TBD；升级支付路径明确为「改 ProductId、不延到期」。
- 测试中 `STD`/`12m` 必须在 Task 1 清掉，避免全库红。
- `ClientSession` 扩展字段与 Controller JSON 命名保持 camelCase 默认序列化一致。
