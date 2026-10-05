# 用心云备份 Windows 部署手册

本文说明如何把网站和接口部署到一台 Windows 服务器。桌面客户端不装在这台服务器上，它装在每台使用备份的 Windows 电脑上。

没有域名、只有 IP、也不上 HTTPS 时，可以按本文部署，供内网试用。支付宝收款要求已备案的域名和 HTTPS。没有域名时不要配置 `appsettings.Local.json`，购买页无法完成真实支付。

大约 90 分钟。只有 IP 时少证书这一步，大约 60 分钟。

## 1. 服务器上要有什么

网站和接口必须用同一个地址。网页里的接口路径是 `/api`，浏览器和客户端都访问这一个地址。

| 组件 | 作用 |
| --- | --- |
| IIS | 对外提供网页，并把 `/api` 转到本机接口 |
| URL Rewrite | 让 `/login`、`/vendor` 这类地址回到首页 |
| Application Request Routing（ARR） | 把 `/api` 反向代理到本机 5080 |
| ASP.NET Core 8 Hosting Bundle | 提供运行接口所需的运行时 |

装完 Hosting Bundle 后执行一次 `iisreset`。在 IIS 管理器里打开服务器节点的 **Application Request Routing Cache**，选 **Server Proxy Settings**，勾选 **Enable proxy**。

防火墙只放行 **80**。若以后上 HTTPS，再放行 **443**。不要放行 **5080**。

## 2. 在开发机上打包

在仓库根目录执行：

```powershell
dotnet publish src/Yxy.Api/Yxy.Api.csproj -c Release -o publish/api
```

网页需要 Node.js 18 或更高版本。在 `src/Yxy.Web` 里执行：

```powershell
npm install
npm run build
```

网页产物在 `src/Yxy.Web/dist`。

## 3. 拷到服务器

在服务器上建立三个目录：

| 目录 | 内容 |
| --- | --- |
| `C:\yxy\api` | `publish/api` 里的全部文件 |
| `C:\yxy\web` | 网页 `dist` 里的全部文件 |
| `C:\yxy\data` | 空目录，用来放数据库 |

编辑 `C:\yxy\api\appsettings.json`：

1. 把 `Admin:Password` 改成强密码。这个密码只在第一次启动、库里还没有管理员时写入。启动后再改文件，不会改掉已经存在的管理员。
2. 把数据库改成绝对路径：`"Web": "Data Source=C:\\yxy\\data\\yxy-api.db"`。接口以 Windows 服务运行时，工作目录是 `System32`，相对路径会把库建错地方。

账号名保持 `admin`。

## 4. 把接口注册成 Windows 服务

用管理员打开 PowerShell：

```powershell
sc.exe create YxyApi binPath= "\"C:\yxy\api\Yxy.Api.exe\" --environment Production --urls http://127.0.0.1:5080" start= auto DisplayName= "用心云网站接口"
sc.exe start YxyApi
```

`binPath=` 后面必须有一个空格。接口只监听 `127.0.0.1:5080`，外网访问不到。

确认本机可打开接口：

```powershell
Invoke-WebRequest -UseBasicParsing http://127.0.0.1:5080/api/public/price
```

能返回价格 JSON 即表示接口已起来。生产环境不会返回开发用的验证码，模拟支付接口也不存在。

以后更新接口：先 `sc.exe stop YxyApi`，覆盖 `C:\yxy\api` 里的程序文件，不要覆盖 `C:\yxy\data`，再 `sc.exe start YxyApi`。

## 5. 建立 IIS 站点

新建站点，物理路径选 `C:\yxy\web`。

只有 IP 时：绑定类型 http，端口 **80**，主机名留空。浏览器使用 `http://服务器IP`。

有域名和证书时：再加一条 https 绑定，端口 **443**，主机名填域名，选好证书。浏览器使用 `https://域名`。

正式官网 https://backup.yongxincloud.cn 当前跑在 Linux Nginx，**443 套件已在服务器上手改**。注意项与套件清单以《Linux部署手册》第 5 节为准。若改用本机 IIS 终结 443，或前面还有云 SLB，必须提供 `ECDHE-RSA-AES256-SHA384` 等 CBC 套件，不能只开 GCM，否则 Windows Server 2012 / 2012 R2 客户端登录握手失败。

在 `C:\yxy\web` 新建 `web.config`：

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <system.webServer>
    <rewrite>
      <rules>
        <rule name="api" stopProcessing="true">
          <match url="^api/(.*)" />
          <action type="Rewrite" url="http://127.0.0.1:5080/api/{R:1}" />
        </rule>
        <rule name="spa" stopProcessing="true">
          <match url=".*" />
          <conditions>
            <add input="{REQUEST_FILENAME}" matchType="IsFile" negate="true" />
          </conditions>
          <action type="Rewrite" url="/index.html" />
        </rule>
      </rules>
    </rewrite>
  </system.webServer>
</configuration>
```

第一条把 `/api` 转到本机接口。第二条让登录、厂商后台这类地址由网页自己路由。更新网页时，覆盖 `C:\yxy\web` 后保留这份 `web.config`。

## 6. 上线后马上做

1. 打开 `http://服务器IP/vendor`（有证书时用 `https://域名/vendor`）。用 `admin` 和刚才设置的密码登录。
2. 左侧进入 **发信服务器**，填入 SMTP 后保存。未填写时，生产环境的注册和找回密码发不出验证码，页面上也不会显示验证码。
3. 可点 **发测试信到发件人**，确认邮箱能收到。

## 7. 客户电脑

客户端只安装在使用备份的 Windows 电脑上。安装包：

```text
http://download.yongxinyun.com/yongxinyunbackup/yongxinyunbackup.zip
```

营销页的「下载」和登录后的「客户端下载」都指向这个地址。开发机重新打包时，客户端在 `publish\desktop\Yxy.Client.exe`，服务在同目录的 `Yxy.Engine.exe`。本机 `dist` 若被正在运行的备份服务锁住，用 `publish\desktop`。

客户端左下角「官网」打开 https://backup.yongxincloud.cn 。

登录默认连 https://backup.yongxincloud.cn 。本机调试可在 `data\site.url` 写一行地址（末尾不要斜杠）覆盖。数据目录在客户端程序旁的 `data` 文件夹。改完后重新打开客户端再登录。

客户机是 Windows Server 2012 / 2012 R2 时：官网 443 已按 Linux 手册打开 CBC 套件；拷贝客户端时必须带上 `Yxy.Client.exe.config`。套件被改丢后，登录会在 TLS 握手失败（本机新系统正常不能当依据）。

网站页脚对外信息：常州用心软件有限公司，电话 400-051-9608，邮箱 market.yongxinyun.com，苏ICP备19043516号-6，苏公网安备32041102001697号。

## 8. 只有 IP 时的限制

可以登录、注册、看订阅和订单，也可以在厂商后台配置发信。密码和登录令牌在网络上是明文，只适合内网试用。

支付宝、百度应用的字段和开放平台对齐项见《用心云备份-配置手册》。没有 `appsettings.Local.json` 时购买页无法真实支付。购买页不提供微信支付。

## 9. 检查

| 检查 | 期望 |
| --- | --- |
| `http://服务器IP` | 打开介绍首页 |
| `http://服务器IP/login` | 打开登录页，刷新不出现 404 |
| `http://服务器IP/api/public/price` | 返回价格 JSON |
| `http://服务器IP/vendor` | 用 admin 进入厂商后台 |
| 客户电脑登录 | `site.url` 指向这台服务器后可以登录 |
