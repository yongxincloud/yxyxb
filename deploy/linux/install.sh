#!/usr/bin/env bash
# 用心云·循备 Linux 生产安装（Ubuntu / Debian）
# 对应 Doc/用心云·循备-Linux部署手册.md 第 3–5 节。
# 运行前须已安装 nginx 与 aspnetcore-runtime-8.0。不要只装 dotnet-runtime。
#
# 在已含 publish/api 与 src/Yxy.Web/dist 的仓库里：
#   sudo ADMIN_PASSWORD='强密码' bash deploy/linux/install.sh
#
# 发布目录不在默认位置时：
#   sudo ADMIN_PASSWORD='强密码' bash install.sh --api /path/publish/api --web /path/dist
set -euo pipefail

API_DEST=/opt/yxy/api
WEB_DEST=/opt/yxy/web
DATA_DIR=/var/lib/yxy
SERVICE_NAME=yxy-api
NGINX_AVAILABLE=/etc/nginx/sites-available/yxy
NGINX_ENABLED=/etc/nginx/sites-enabled/yxy

SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
REPO_ROOT=$(cd "$SCRIPT_DIR/../.." && pwd)
API_SRC="$REPO_ROOT/publish/api"
WEB_SRC="$REPO_ROOT/src/Yxy.Web/dist"
ADMIN_PASSWORD="${ADMIN_PASSWORD:-}"
FIREWALL=auto

usage() {
  cat <<'EOF'
用法: sudo ADMIN_PASSWORD='强密码' bash install.sh [--api DIR] [--web DIR] [--firewall ufw|firewalld|none]

  --api DIR     接口发布目录，默认 <仓库>/publish/api
  --web DIR     网页 dist 目录，默认 <仓库>/src/Yxy.Web/dist
  --password    管理员密码，也可用环境变量 ADMIN_PASSWORD。仅第一次建库时写入
  --firewall    放行 80。默认：防火墙已经启用才放行，否则跳过。不会放行 5080
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --api) API_SRC=$2; shift 2 ;;
    --web) WEB_SRC=$2; shift 2 ;;
    --password) ADMIN_PASSWORD=$2; shift 2 ;;
    --firewall) FIREWALL=$2; shift 2 ;;
    -h|--help) usage; exit 0 ;;
    *) echo "未知参数: $1"; usage; exit 1 ;;
  esac
done

if [[ "$(id -u)" -ne 0 ]]; then
  echo "请用 root 运行：sudo ADMIN_PASSWORD='强密码' bash $0"
  exit 1
fi

if [[ ! -x /usr/bin/dotnet ]]; then
  echo "未找到 /usr/bin/dotnet。请先安装 aspnetcore-runtime-8.0。"
  exit 1
fi

if ! /usr/bin/dotnet --list-runtimes | grep -q 'Microsoft.AspNetCore.App 8\.'; then
  echo "未找到 ASP.NET Core 8 运行时。请安装 aspnetcore-runtime-8.0，不要只装 dotnet-runtime。"
  exit 1
fi

if ! command -v nginx >/dev/null 2>&1; then
  echo "未找到 nginx。请先安装 Nginx。"
  exit 1
fi

if ! command -v python3 >/dev/null 2>&1; then
  echo "未找到 python3。请先安装 python3，安装程序用它改 appsettings.json。"
  exit 1
fi

if ! id www-data >/dev/null 2>&1; then
  echo "系统里没有 www-data 用户。本安装程序按 Ubuntu / Debian 编写。"
  exit 1
fi

if [[ ! -d /etc/nginx/sites-available || ! -d /etc/nginx/sites-enabled ]]; then
  echo "未找到 Nginx 的 sites-available / sites-enabled。请使用 Ubuntu 或 Debian 的站点目录。"
  exit 1
fi

if [[ ! -f "$API_SRC/Yxy.Api.dll" ]]; then
  echo "接口目录里没有 Yxy.Api.dll：$API_SRC"
  echo "请先在开发机执行：dotnet publish src/Yxy.Api/Yxy.Api.csproj -c Release -o publish/api"
  exit 1
fi

if [[ ! -f "$WEB_SRC/index.html" ]]; then
  echo "网页目录里没有 index.html：$WEB_SRC"
  echo "请先在 src/Yxy.Web 执行 npm install 与 npm run build。"
  exit 1
fi

if [[ -f "$DATA_DIR/yxy-api.db" ]]; then
  db_exists=1
else
  db_exists=0
fi

if [[ "$db_exists" -eq 0 ]]; then
  if [[ -z "$ADMIN_PASSWORD" ]]; then
    echo "第一次安装必须设置管理员密码：sudo ADMIN_PASSWORD='强密码' bash $0"
    exit 1
  fi
  if [[ ${#ADMIN_PASSWORD} -lt 8 ]]; then
    echo "管理员密码至少 8 位。"
    exit 1
  fi
  case "$ADMIN_PASSWORD" in
    Admin@yxxt123|Admin@yxy123)
      echo "不能使用仓库里的默认密码。请换一个强密码。"
      exit 1
      ;;
  esac
fi

if systemctl is-active --quiet "$SERVICE_NAME"; then
  systemctl stop "$SERVICE_NAME"
fi

install -d -m 755 /opt/yxy "$API_DEST" "$WEB_DEST" "$DATA_DIR"

saved_settings=""
if [[ -f "$API_DEST/appsettings.json" ]]; then
  saved_settings=$(mktemp)
  cp -a "$API_DEST/appsettings.json" "$saved_settings"
fi

cp -a "$API_SRC"/. "$API_DEST"/
cp -a "$WEB_SRC"/. "$WEB_DEST"/
chmod -R a+rX "$WEB_DEST"

if [[ -n "$saved_settings" ]]; then
  cp -a "$saved_settings" "$API_DEST/appsettings.json"
  rm -f "$saved_settings"
fi

python3 - "$API_DEST/appsettings.json" "$ADMIN_PASSWORD" "$db_exists" <<'PY'
import json, sys
path, password, db_exists = sys.argv[1], sys.argv[2], sys.argv[3] == "1"
with open(path, encoding="utf-8") as f:
    cfg = json.load(f)
admin = cfg.setdefault("Admin", {})
admin["Username"] = "admin"
if not db_exists:
    admin["Password"] = password
cfg.setdefault("ConnectionStrings", {})["Web"] = "Data Source=/var/lib/yxy/yxy-api.db"
with open(path, "w", encoding="utf-8") as f:
    json.dump(cfg, f, ensure_ascii=False, indent=2)
    f.write("\n")
PY

chown -R www-data:www-data "$DATA_DIR" "$API_DEST"
chmod 600 "$API_DEST/appsettings.json"

install -m 644 "$SCRIPT_DIR/yxy-api.service" "/etc/systemd/system/${SERVICE_NAME}.service"
systemctl daemon-reload
systemctl enable --now "$SERVICE_NAME"

install -m 644 "$SCRIPT_DIR/yxy.nginx.conf" "$NGINX_AVAILABLE"
ln -sf "$NGINX_AVAILABLE" "$NGINX_ENABLED"

removed_default=0
if [[ -L /etc/nginx/sites-enabled/default ]]; then
  rm -f /etc/nginx/sites-enabled/default
  removed_default=1
fi

if ! nginx -t; then
  if [[ "$removed_default" -eq 1 && -f /etc/nginx/sites-available/default ]]; then
    ln -sf /etc/nginx/sites-available/default /etc/nginx/sites-enabled/default
  fi
  echo "Nginx 配置检查失败，未重载。"
  exit 1
fi

systemctl reload nginx

case "$FIREWALL" in
  ufw)
    ufw allow 80/tcp
    ;;
  firewalld)
    firewall-cmd --permanent --add-service=http
    firewall-cmd --reload
    ;;
  none)
    ;;
  auto)
    if command -v ufw >/dev/null 2>&1 && ufw status 2>/dev/null | grep -q "Status: active"; then
      ufw allow 80/tcp
    elif command -v firewall-cmd >/dev/null 2>&1 && systemctl is-active --quiet firewalld; then
      firewall-cmd --permanent --add-service=http
      firewall-cmd --reload
    else
      echo "防火墙未启用，未改规则。上线前请只放行 80，不要放行 5080。"
    fi
    ;;
  *)
    echo "不认识的防火墙：$FIREWALL（可用 ufw、firewalld、none）"
    exit 1
    ;;
esac

ok=0
for _ in 1 2 3 4 5 6 7 8 9 10; do
  if curl -fsS "http://127.0.0.1:5080/api/public/price"; then
    echo
    ok=1
    break
  fi
  sleep 1
done

if [[ "$ok" -ne 1 ]]; then
  echo "接口未响应。请查看：systemctl status yxy-api"
  exit 1
fi

echo "安装完成。浏览器打开 http://服务器IP/vendor ，用 admin 和所设密码登录，然后到发信服务器填写 SMTP。"
