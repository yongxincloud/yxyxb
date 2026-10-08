# 营销首页美化 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将营销首页改为左文右图 + 红黑主题 + 离线介质差异叙事，并把对外 logo 统一为 `logo.png`。

**Architecture:** 在现有 Vue 3 `HomeView` 内按区块重排；素材从 `YxyXb.img` 拷入 `public/`；客户主题 token 已含红黑，营销首页强制 `theme-customer` 或局部 CSS 变量。登录等壳只改 logo 路径。

**Tech Stack:** Vue 3、Vite、现有 `brand.js` 与 `/api/public/price`。

## Global Constraints

- Spec：`docs/superpowers/specs/2026-10-08-marketing-home-redesign-design.md`
- 正式名：用心云·循备；主差异：离线介质
- 首屏不塞价格；无浮层徽章
- 窄屏：图在文下

---

### Task 1: 素材与 logo 路径

**Files:**
- Copy: `src/YxyXb.img/logo.png`, `product1.png`…`product6.png` → `src/YxyXb.Web/public/`
- Modify: `LoginView.vue`, `RegisterView.vue`, `PortalLayout.vue`, `VendorLayout.vue`, `VendorLoginView.vue`, `HomeView.vue`, `customer-theme.test.js`

- [x] **Step 1:** 拷贝 png 到 public
- [x] **Step 2:** 全部 `/logo.jpg` → `/logo.png`
- [x] **Step 3:** 更新 Vitest 中 logo 路径断言
- [x] **Step 4:** `npm test` 通过后 commit

---

### Task 2: 重写 HomeView 版式与文案

**Files:**
- Modify: `src/YxyXb.Web/src/views/HomeView.vue`
- Modify: `src/YxyXb.Web/src/styles/tokens.css`（若需营销专用字体变量）
- Modify: `src/YxyXb.Web/index.html`（title/description 已是新品牌则微调）

- [x] **Step 1:** 首屏左文右图 + CTA
- [x] **Step 2:** 价格第二屏（沿用 plans API）
- [x] **Step 3:** 价值区 6 点（离线介质置顶）
- [x] **Step 4:** 截图左右交替条
- [x] **Step 5:** 下载 + 页脚；2～3 动效
- [x] **Step 6:** 窄屏样式；`npm test`；commit

---

### Task 3: 本地验收

- [x] **Step 1:** `npm run build` 成功
- [x] **Step 2:** 开发服打开首页目视验收 checklist（spec §6）
