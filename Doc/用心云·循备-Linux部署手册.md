# 用心云·循备 Linux 部署手册

本文说明如何把网站和接口部署到一台 Linux 服务器。桌面客户端仍然只装在 Windows 电脑上，不装在这台 Linux 服务器上。

没有域名、只有 IP、也不上 HTTPS 时，可以按本文部署，供内网试用。支付宝收款要求已备案的域名和 HTTPS。没有域名时不要配置 `appsettings.Local.json`，购买页无法完成真实支付。

大约 90 分钟。只有 IP 时少证书这一步，大约 60 分钟。下面以 Ubuntu 或 Debian 为例。防火墙命令按 firewalld 和 ufw 各写一条，用到哪条做哪条。

## 1. 服务器上要有什么

网站和接口必须用同一个地址。网页里的接口路径是 `/api`。

安装 Nginx 和 ASP.NET Core 8 运行时。运行时按微软当前文档安装 `aspnetcore-runtime-8.0`。不要只装普通的 `dotnet-runtime`，接口需要 ASP.NET Core 那一份。

防火墙只放行 **80**。若以后上 HTTPS，再放行 **443**。不要放行 **5080**。

```bash
ufw allow 80/tcp
```

使用 firewalld 时：

```bash
firewall-cmd --permanent --add-service=http
firewall-cmd --reload
```

## 2. 打包

接口是 .NET 程序，可以在 Windows 开发机上发布，再把目录拷到 Linux。不要在 Linux 上运行里面的 `YxyXb.Api.exe`。

在仓库根目录执行：

```powershell
dotnet publish src/YxyXb.Api/YxyXb.Api.csproj -c Release -o publish/api
```

网页在 `src/YxyXb.Web` 里执行（需要 Node.js 18 或更高版本）：

```powershell
npm install
npm run build
```

产物在 `src/YxyXb.Web/dist`。

若 Linux 上启动接口时报找不到 SQLite 本机库，改在 Linux 上进入仓库再执行一次 `dotnet publish src/YxyXb.Api/YxyXb.Api.csproj -c Release -o /opt/yxyxb/api`。

## 3. 放到服务器

```bash
sudo mkdir -p /opt/yxyxb/api /opt/yxyxb/web /var/lib/yxyxb
sudo chown -R "$USER":"$USER" /opt/yxy /var/lib/yxyxb
```

把 `publish/api` 的内容放到 `/opt/yxyxb/api`，把网页 `dist` 的内容放到 `/opt/yxyxb/web`。

编辑 `/opt/yxyxb/api/appsettings.json`：

1. 把 `Admin:Password` 改成强密码。这个密码只在第一次启动、库里还没有管理员时写入。启动后再改文件，不会改掉已经存在的管理员。
2. 数据库使用绝对路径：`"Web": "Data Source=/var/lib/yxyxb/yxy-api.db"`。

账号名保持 `admin`。

## 4. 用 systemd 启动接口

新建 `/etc/systemd/system/yxy-api.service`：

```ini
[Unit]
Description=用心云网站接口
After=network.target

[Service]
WorkingDirectory=/opt/yxyxb/api
ExecStart=/usr/bin/dotnet /opt/yxyxb/api/YxyXb.Api.dll --urls http://127.0.0.1:5080
Environment=ASPNETCORE_ENVIRONMENT=Production
Restart=always
RestartSec=5
User=www-data

[Install]
WantedBy=multi-user.target
```

`www-data` 必须能写数据库目录：

```bash
sudo chown -R www-data:www-data /var/lib/yxyxb /opt/yxyxb/api
sudo systemctl daemon-reload
sudo systemctl enable --now yxy-api
```

确认本机接口：

```bash
curl -s http://127.0.0.1:5080/api/public/price
```

能看到价格 JSON 即可。生产环境不会返回开发用的验证码，模拟支付接口也不存在。

以后更新接口：`sudo systemctl stop yxyxb-api`，覆盖 `/opt/yxyxb/api` 里的程序文件，不要动 `/var/lib/yxyxb`，再 `sudo systemctl start yxyxb-api`。

## 5. Nginx

新建 `/etc/nginx/sites-available/yxy`。只有 IP 时用下面这一份，把 `listen 80` 留给这台机器的默认访问：

```nginx
server {
    listen 80 default_server;
    server_name _;
    root /opt/yxyxb/web;
    index index.html;

    location /api/ {
        proxy_pass http://127.0.0.1:5080;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    location / {
        try_files $uri $uri/ /index.html;
    }
}
```

`location /api/` 把接口转到本机 5080。`try_files` 让 `/login`、`/vendor` 刷新时仍打开网页。

启用并重载：

```bash
sudo ln -sf /etc/nginx/sites-available/yxy /etc/nginx/sites-enabled/yxy
sudo nginx -t
sudo systemctl reload nginx
```

有域名和证书时，把 `server_name _` 改成域名，再用 certbot 或已有证书增加 443。证书配好后，客户端和浏览器都改用 `https://域名`。更新网页时覆盖 `/opt/yxyxb/web`，不要删掉 Nginx 配置。

### 正式官网注意项（backup.yongxincloud.cn）

生产 443 **已在服务器上手改并 reload**。以后重装 Nginx、换证书、改 SLB 时必须按下面保留，不要缩回「只开 GCM」。

客户端要覆盖 Win7 / 2008 R2（已打 TLS 1.2 补丁）到 2022。2012 / 2012 R2 的 Schannel **没有 ECDHE-GCM**，只开 GCM 时登录会在握手失败（本机 Win10 正常、服务器 2012 R2 不行）。完整示例：`deploy/linux/yxy.nginx.conf`。

在 `listen 443 ssl` 的 server 里保留：

```nginx
ssl_protocols TLSv1.2 TLSv1.3;
ssl_prefer_server_ciphers on;
ssl_ciphers ECDHE-RSA-AES256-GCM-SHA384:ECDHE-RSA-AES128-GCM-SHA256:ECDHE-RSA-AES256-SHA384:ECDHE-RSA-AES128-SHA256:ECDHE-RSA-AES256-SHA:ECDHE-RSA-AES128-SHA:AES256-SHA256:AES128-SHA256:AES256-SHA:AES128-SHA;
```

改完：`sudo nginx -t` 然后 `sudo systemctl reload nginx`。

| 客户系统 | 实际会用的套件 |
| --- | --- |
| Win10 / 2016 / 2019 / 2022 | 前面的 GCM |
| 2012 R2 | `ECDHE-RSA-AES256-SHA384`（CBC） |
| 2012 / 已补丁 Win7·2008 R2 | 后面的 SHA256 / SHA |

不要开 TLS 1.0 / 1.1。证书链保持完整（含中间证）。客户端强制 TLS 1.2，拷到 2012 R2 时必须带 `YxyXb.Client.exe.config`。

若 443 在云负载均衡上终结：到控制台 SSL 策略加入同等套件，不要只留 `ECDHE-RSA-AES*-GCM-*`。只改 Nginx、SLB 仍只开 GCM 时，2012 R2 照样登不上。

## 6. 上线后马上做

1. 打开 `http://服务器IP/vendor`（有证书时用 `https://域名/vendor`）。用 `admin` 和刚才设置的密码登录。
2. 左侧进入 **发信服务器**，填入 SMTP 后保存。未填写时，生产环境的注册和找回密码发不出验证码，页面上也不会显示验证码。
3. 可点 **发测试信到发件人**，确认邮箱能收到。

## 7. 客户电脑

客户端只安装在使用备份的 Windows 电脑上，不装在这台 Linux 服务器上。安装包：

```text
http://download.yongxinyun.com/yxyxb/yxyxb.zip
```

营销页的「下载」和登录后的「客户端下载」都指向这个地址。开发机重新打包时，客户端在 `publish\desktop\YxyXb.Client.exe`，服务在同目录的 `YxyXb.Engine.exe`。

客户端左下角「官网」打开 https://backup.yongxincloud.cn 。

登录默认连 https://backup.yongxincloud.cn 。本机调试可在 `data\site.url` 写一行地址（末尾不要斜杠）覆盖。数据目录在客户端程序旁的 `data` 文件夹。改完后重新打开客户端再登录。

网站页脚对外信息：常州用心软件有限公司，电话 400-051-9608，邮箱 market.yongxinyun.com，苏ICP备19043516号-6，苏公网安备32041102001697号。

## 8. 只有 IP 时的限制

可以登录、注册、看订阅和订单，也可以在厂商后台配置发信。密码和登录令牌在网络上是明文，只适合内网试用。

支付宝、百度应用的字段和开放平台对齐项见《用心云·循备-配置手册》。没有 `appsettings.Local.json` 时购买页无法真实支付。购买页不提供微信支付。

## 9. 检查

| 检查 | 期望 |
| --- | --- |
| `systemctl status yxy-api` | active (running) |
| `http://服务器IP` | 打开介绍首页 |
| `http://服务器IP/login` | 打开登录页，刷新不出现 404 |
| `http://服务器IP/api/public/price` | 返回价格 JSON |
| `http://服务器IP/vendor` | 用 admin 进入厂商后台 |
| 客户电脑登录 | `site.url` 指向这台服务器后可以登录 |
