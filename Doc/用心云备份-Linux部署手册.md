# 用心云备份 Linux 部署手册

本文说明如何把网站和接口部署到一台 Linux 服务器。桌面客户端仍然只装在 Windows 电脑上，不装在这台 Linux 服务器上。

没有域名、只有 IP、也不上 HTTPS 时，可以按本文部署，供内网试用。真实微信、支付宝收款要求域名和 HTTPS，当前版本也还没有接入，生产环境里模拟支付会关闭。

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

接口是 .NET 程序，可以在 Windows 开发机上发布，再把目录拷到 Linux。不要在 Linux 上运行里面的 `Yxy.Api.exe`。

在仓库根目录执行：

```powershell
dotnet publish src/Yxy.Api/Yxy.Api.csproj -c Release -o publish/api
```

网页在 `src/Yxy.Web` 里执行（需要 Node.js 18 或更高版本）：

```powershell
npm install
npm run build
```

产物在 `src/Yxy.Web/dist`。

若 Linux 上启动接口时报找不到 SQLite 本机库，改在 Linux 上进入仓库再执行一次 `dotnet publish src/Yxy.Api/Yxy.Api.csproj -c Release -o /opt/yxy/api`。

## 3. 放到服务器

```bash
sudo mkdir -p /opt/yxy/api /opt/yxy/web /var/lib/yxy
sudo chown -R "$USER":"$USER" /opt/yxy /var/lib/yxy
```

把 `publish/api` 的内容放到 `/opt/yxy/api`，把网页 `dist` 的内容放到 `/opt/yxy/web`。

编辑 `/opt/yxy/api/appsettings.json`：

1. 把 `Admin:Password` 改成强密码。这个密码只在第一次启动、库里还没有管理员时写入。启动后再改文件，不会改掉已经存在的管理员。
2. 数据库使用绝对路径：`"Web": "Data Source=/var/lib/yxy/yxy-api.db"`。

账号名保持 `admin`。

## 4. 用 systemd 启动接口

新建 `/etc/systemd/system/yxy-api.service`：

```ini
[Unit]
Description=用心云网站接口
After=network.target

[Service]
WorkingDirectory=/opt/yxy/api
ExecStart=/usr/bin/dotnet /opt/yxy/api/Yxy.Api.dll --urls http://127.0.0.1:5080
Environment=ASPNETCORE_ENVIRONMENT=Production
Restart=always
RestartSec=5
User=www-data

[Install]
WantedBy=multi-user.target
```

`www-data` 必须能写数据库目录：

```bash
sudo chown -R www-data:www-data /var/lib/yxy /opt/yxy/api
sudo systemctl daemon-reload
sudo systemctl enable --now yxy-api
```

确认本机接口：

```bash
curl -s http://127.0.0.1:5080/api/public/price
```

能看到价格 JSON 即可。生产环境不会返回开发用的验证码，模拟支付接口也不存在。

以后更新接口：`sudo systemctl stop yxy-api`，覆盖 `/opt/yxy/api` 里的程序文件，不要动 `/var/lib/yxy`，再 `sudo systemctl start yxy-api`。

## 5. Nginx

新建 `/etc/nginx/sites-available/yxy`。只有 IP 时用下面这一份，把 `listen 80` 留给这台机器的默认访问：

```nginx
server {
    listen 80 default_server;
    server_name _;
    root /opt/yxy/web;
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

有域名和证书时，把 `server_name _` 改成域名，再用 certbot 或已有证书增加 443。证书配好后，客户端和浏览器都改用 `https://域名`。更新网页时覆盖 `/opt/yxy/web`，不要删掉 Nginx 配置。

## 6. 上线后马上做

1. 打开 `http://服务器IP/vendor`（有证书时用 `https://域名/vendor`）。用 `admin` 和刚才设置的密码登录。
2. 左侧进入 **发信服务器**，填入 SMTP 后保存。未填写时，生产环境的注册和找回密码发不出验证码，页面上也不会显示验证码。
3. 可点 **发测试信到发件人**，确认邮箱能收到。

## 7. 客户电脑

客户端只安装在使用备份的 Windows 电脑上，不装在这台 Linux 服务器上。安装包：

```text
http://download.yongxinyun.com/yongxinyunbackup/yongxinyunbackup.zip
```

营销页的「下载」和登录后的「客户端下载」都指向这个地址。开发机重新打包时，客户端在 `publish\desktop\Yxy.Client.exe`，服务在同目录的 `Yxy.Engine.exe`。

客户端左下角「官网」打开 https://backup.yongxincloud.cn 。

登录用的地址写在 `data\site.url`，只写一行，末尾不要加斜杠：

```text
https://backup.yongxincloud.cn
```

不写这个文件时，客户端会去连 `http://127.0.0.1:5080`。数据目录在客户端程序旁的 `data` 文件夹。改完后重新打开客户端再登录。

网站页脚对外信息：常州用心软件有限公司，电话 400-051-9608，邮箱 market.yongxinyun.com，苏ICP备19043516号-6，苏公网安备32041102001697号。

## 8. 只有 IP 时的限制

可以登录、注册、看订阅和订单，也可以在厂商后台配置发信。密码和登录令牌在网络上是明文，只适合内网试用。

真实收款不能用。微信和支付宝要求已备案的域名和 HTTPS。当前程序的支付回调仍是开发验签，不能当作正式收款。

## 9. 检查

| 检查 | 期望 |
| --- | --- |
| `systemctl status yxy-api` | active (running) |
| `http://服务器IP` | 打开介绍首页 |
| `http://服务器IP/login` | 打开登录页，刷新不出现 404 |
| `http://服务器IP/api/public/price` | 返回价格 JSON |
| `http://服务器IP/vendor` | 用 admin 进入厂商后台 |
| 客户电脑登录 | `site.url` 指向这台服务器后可以登录 |
