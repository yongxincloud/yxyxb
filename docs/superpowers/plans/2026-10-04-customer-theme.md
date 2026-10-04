# 客户站墨底红印 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** 给营销站、客户登录和登录后页面换上用心云 logo 与墨底红印配色，并在「我的订阅」写明授权覆盖范围。

**Architecture:** `:root` 继续保存厂商蓝色。`theme.js` 判断路径是否属于客户站，`App.vue` 把 `theme-customer` 加到 `document.body`。客户色写在 `body.theme-customer`。Logo 从 `src/Yxy.img/logo.jpg` 复制到 `src/Yxy.Web/public/logo.jpg`。授权说明是 `PortalView.vue` 里的固定文字。

**Tech Stack:** Vue 3、Vite、Vue Router、Vitest。

## Global Constraints

- 按钮与主操作 `#DC0606`，悬停 `#B00404`。
- 文字链接与当前菜单文字 `#B10505`，当前菜单底 `#F8E8E8`。
- 营销首屏、登录左侧、页脚 `#1C1214`。
- 页面底与次级表面 `#F6F3F1`，卡片 `#FFFFFF`。
- 标题 `#2A211F`，正文 `#5C534F`，次要文字 `#8A817C`，边框 `#E6E0DC`，错误文字 `#7A1F1A`。
- `:root` 的 `--brand-primary: #3171FF` 保持不变。厂商后台与 `/vendor/login` 不添加 `theme-customer`。
- Logo 只放在首页顶栏（高 64px）、登录左侧白卡片（高 64px）、登录后侧栏（高 72px）。替代文字为「用心云」。
- 「我的订阅」在现有说明下增加四条：文件备份；库备份（SQL Server）；离线介质；云、UNC、FTP、邮件。再加一句：「没有有效授权时，文件备份、库备份、云、UNC、FTP、邮件会停用。离线介质仍可使用。操作在客户端里做。」
- 未登录仍只显示「请先登录」。不新增接口。不改桌面客户端，不改其余文案和版式。

---

### Task 1: 客户路径与配色

**Files:**
- Create: `src/Yxy.Web/src/theme.js`
- Create: `src/Yxy.Web/src/theme.test.js`
- Modify: `src/Yxy.Web/src/styles/tokens.css`
- Modify: `src/Yxy.Web/src/customer-theme.test.js`
- Modify: `src/Yxy.Web/src/App.vue`

**Interfaces:**
- Consumes: 路由路径字符串
- Produces: `isCustomerPath(path: string) => boolean`。`body.theme-customer` 覆盖客户色，`.btn-yxy` 在该状态下为 `#DC0606`

- [x] **Step 1: Write the failing test**

`theme.test.js` 断言 `/`、`/login`、`/register`、`/home`、`/forgot`、`/terms` 为客户路径，`/vendor`、`/vendor/login`、`/vendor/customers` 不是。`customer-theme.test.js` 断言 `tokens.css` 仍含 `--brand-primary: #3171FF`，且 `body.theme-customer` 含 `#B10505`、`#DC0606`、`#B00404`、`#1C1214`、`#7A1F1A`、`#F6F3F1`。

- [x] **Step 2: Run test to verify it fails**

Run: `npm test -- src/theme.test.js src/customer-theme.test.js`  
Working directory: `src/Yxy.Web`  
Expected: FAIL because `theme.js` 不存在，且 `tokens.css` 没有 `body.theme-customer`

- [x] **Step 3: Write minimal implementation**

`theme.js` 导出 `isCustomerPath`，对非 `/vendor` 开头的路径返回 true。`App.vue` 用 `watch(route.path, { immediate: true })` 切换 `document.body` 的 `theme-customer`。`tokens.css` 追加 `body.theme-customer` 变量，以及该状态下 `.btn-yxy` 背景 `#DC0606`、悬停 `#B00404`。

- [x] **Step 4: Run test to verify it passes**

Run: `npm test`  
Working directory: `src/Yxy.Web`  
Expected: theme 与 tokens 断言 PASS

- [x] **Step 5: Commit**

```bash
git add src/Yxy.Web/src/theme.js src/Yxy.Web/src/theme.test.js src/Yxy.Web/src/customer-theme.test.js src/Yxy.Web/src/styles/tokens.css src/Yxy.Web/src/App.vue
git commit -m "feat: add the customer ink-and-red theme"
```

### Task 2: Logo 与授权说明

**Files:**
- Create: `src/Yxy.Web/public/logo.jpg`
- Modify: `src/Yxy.Web/src/views/HomeView.vue`
- Modify: `src/Yxy.Web/src/views/LoginView.vue`
- Modify: `src/Yxy.Web/src/views/PortalLayout.vue`
- Modify: `src/Yxy.Web/src/views/PortalView.vue`
- Test: `src/Yxy.Web/src/customer-theme.test.js`

**Interfaces:**
- Consumes: `/logo.jpg`，Task 1 的客户配色
- Produces: 三处 logo，以及「我的订阅」的固定授权列表

- [x] **Step 1: Write the failing test**

`customer-theme.test.js` 断言首页、登录、门户侧栏含 `src="/logo.jpg"`，高度分别为 64px、登录含 `#DC0606`、侧栏 72px 与 `#F8E8E8`。注册页不含 `/logo.jpg`。厂商布局仍含 `#eef3ff` 且不含 `#F8E8E8`。`PortalView.vue` 含四条授权说明、停用说明和「请先登录」。

- [x] **Step 2: Run test to verify it fails**

Run: `npm test -- src/customer-theme.test.js`  
Expected: FAIL，页面还没有 logo 和授权列表

- [x] **Step 3: Write minimal implementation**

复制 `src/Yxy.img/logo.jpg` 到 `src/Yxy.Web/public/logo.jpg`。三处按规格放图。登录选中标签用 `#DC0606`。门户当前菜单底改为 `#F8E8E8`。`PortalView.vue` 在现有说明下、`v-if="info"` 内加入列表和停用说明。

- [x] **Step 4: Run test to verify it passes**

Run: `npm test`  
Expected: PASS

- [x] **Step 5: Commit**

```bash
git add src/Yxy.Web/public/logo.jpg src/Yxy.img/logo.jpg src/Yxy.Web/src/views/HomeView.vue src/Yxy.Web/src/views/LoginView.vue src/Yxy.Web/src/views/PortalLayout.vue src/Yxy.Web/src/views/PortalView.vue src/Yxy.Web/src/customer-theme.test.js
git commit -m "feat: show the logo and license coverage on customer pages"
```

### Task 3: 浏览器核对

**Files:**
- 无新文件

- [x] **Step 1: 打开页面**

启动 `src/Yxy.Web` 的 Vite。核对 `/`、`/login`、`/register`、`/vendor/login`。已登录时再核对 `/home`。

- [x] **Step 2: 对照规格**

首页顶栏是 logo，首屏墨色，购买按钮正红。登录左侧 logo 在白卡片上。注册页按钮正红且没有 logo。厂商登录按钮仍是 `#3171FF`。
