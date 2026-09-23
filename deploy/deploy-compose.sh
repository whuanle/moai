#!/usr/bin/env bash
# ============================================================
# MoAI 一体部署脚本（docker compose）
#
# 用法：
#   bash deploy/deploy-compose.sh              # 生成配置 + 拉镜像 + 启动
#   BUILD=1 bash deploy/deploy-compose.sh      # 本地源码构建 moai 镜像后启动
#
# 前置：
#   1) 已安装 docker + docker compose v2
#   2) cp .env.example .env 并按需修改（MOAI_SERVER_URL 留空会自动探测本机 IP）
#
# 说明：
#   - 以 .env 为唯一配置来源，生成 ${MOAI_CONFIG_FILE}（默认 ./configs/system.json）
#     并挂载进容器 /app/configs/system.json，用户无需手写 system.json。
#   - 会拉取全部依赖镜像：postgres=pgvector/pgvector:pg16、opensandbox/server、
#     opensandbox/code-interpreter（仅拉取不部署）、rustfs/redis/rabbitmq/aws-cli。
# ============================================================
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"
cd "${ROOT_DIR}"

OPENSANDBOX_EXECD_IMAGE="${OPENSANDBOX_EXECD_IMAGE:-opensandbox/execd:v1.1.0}"
OPENSANDBOX_EGRESS_IMAGE="${OPENSANDBOX_EGRESS_IMAGE:-opensandbox/egress:v1.1.7}"

# 探测本机对外 IP（对外地址/存储地址留空时使用）
detect_host_ip() {
  local ip=""
  if command -v ip >/dev/null 2>&1; then
    ip="$(ip route get 1.1.1.1 2>/dev/null | awk '{for (i=1;i<=NF;i++) if ($i=="src") { print $(i+1); exit }}')"
  fi
  if [ -z "$ip" ] && command -v hostname >/dev/null 2>&1; then
    ip="$(hostname -I 2>/dev/null | awk '{print $1}')"
  fi
  if [ -z "$ip" ] && command -v ipconfig >/dev/null 2>&1; then
    ip="$(ipconfig getifaddr en0 2>/dev/null || true)"
  fi
  printf '%s' "$ip"
}

# JSON 字符串转义（反斜杠与双引号）
json_escape() {
  local s="$1"
  s="${s//\\/\\\\}"
  s="${s//\"/\\\"}"
  printf '%s' "$s"
}

if [ ! -f .env ]; then
  echo "==> 未找到 .env，从 .env.example 复制"
  cp .env.example .env
fi

# 载入 .env（作为唯一配置来源）
set -a
# shellcheck disable=SC1091
. ./.env
set +a

MOAI_CONFIG_FILE="${MOAI_CONFIG_FILE:-./configs/system.json}"
MOAI_SERVER_URL="${MOAI_SERVER_URL:-}"
MOAI_PORT="${MOAI_PORT:-8080}"
MOAI_AES_KEY="${MOAI_AES_KEY:-please-change-this-aes-key}"
POSTGRES_USER="${POSTGRES_USER:-postgres}"
POSTGRES_PASSWORD="${POSTGRES_PASSWORD:-moai123456}"
POSTGRES_DB="${POSTGRES_DB:-moai}"
RABBITMQ_USER="${RABBITMQ_USER:-guest}"
RABBITMQ_PASSWORD="${RABBITMQ_PASSWORD:-guest}"
S3_BUCKET="${S3_BUCKET:-moai}"
S3_ACCESS_KEY_ID="${S3_ACCESS_KEY_ID:-moaiadmin}"
S3_ACCESS_KEY_SECRET="${S3_ACCESS_KEY_SECRET:-moaiadmin123}"
S3_ENDPOINT="${S3_ENDPOINT:-}"
RUSTFS_PORT="${RUSTFS_PORT:-9000}"
OPENSANDBOX_SANDBOX_IMAGE="${OPENSANDBOX_SANDBOX_IMAGE:-opensandbox/code-interpreter:v1.1.0}"
OPENSANDBOX_TIMEOUT_SECONDS="${OPENSANDBOX_TIMEOUT_SECONDS:-900}"
OPENSANDBOX_RENEW_THRESHOLD_SECONDS="${OPENSANDBOX_RENEW_THRESHOLD_SECONDS:-300}"
OTLP_TRACE="${OTLP_TRACE:-}"
OTLP_METRICS="${OTLP_METRICS:-}"
OTLP_PROTOCOL="${OTLP_PROTOCOL:-0}"

# 兼容旧变量 MOAI_HOST（无协议时补 http:// 与端口）
if [ -z "${MOAI_SERVER_URL}" ] && [ -n "${MOAI_HOST:-}" ]; then
  if [[ "${MOAI_HOST}" == *"://"* ]]; then
    MOAI_SERVER_URL="${MOAI_HOST}"
  else
    MOAI_SERVER_URL="http://${MOAI_HOST}:${MOAI_PORT}"
  fi
fi

# 对外地址/存储地址留空时自动探测本机 IP
DETECTED_HOST=""
if [ -z "${MOAI_SERVER_URL}" ] || [ -z "${S3_ENDPOINT}" ]; then
  DETECTED_HOST="$(detect_host_ip)"
  [ -z "${DETECTED_HOST}" ] && DETECTED_HOST="localhost"
fi
if [ -z "${MOAI_SERVER_URL}" ]; then
  MOAI_SERVER_URL="http://${DETECTED_HOST}:${MOAI_PORT}"
  echo "==> 未设置 MOAI_SERVER_URL，自动探测为 ${MOAI_SERVER_URL}（域名/HTTPS 请在 .env 显式填写后重跑）"
fi
if [ -z "${S3_ENDPOINT}" ]; then
  S3_ENDPOINT="http://${DETECTED_HOST}:${RUSTFS_PORT}"
fi

# 由 .env 生成应用配置
mkdir -p "$(dirname "${MOAI_CONFIG_FILE}")"
if [ -f "${MOAI_CONFIG_FILE}" ]; then
  cp "${MOAI_CONFIG_FILE}" "${MOAI_CONFIG_FILE}.bak"
fi
cat > "${MOAI_CONFIG_FILE}" <<EOF
{
  "MoAI": {
    "Name": "MoAI",
    "Port": 8080,
    "Server": "$(json_escape "${MOAI_SERVER_URL}")",
    "WebUI": "$(json_escape "${MOAI_SERVER_URL}")",
    "AES": "$(json_escape "${MOAI_AES_KEY}")",
    "Database": "Database=$(json_escape "${POSTGRES_DB}");Host=postgres;Password=$(json_escape "${POSTGRES_PASSWORD}");Port=5432;Username=$(json_escape "${POSTGRES_USER}");Search Path=public",
    "Redis": "redis:6379",
    "RabbitMQ": "amqp://$(json_escape "${RABBITMQ_USER}"):$(json_escape "${RABBITMQ_PASSWORD}")@rabbitmq:5672",
    "OpenSandBox": {
      "Address": "http://opensandbox-server:8090",
      "ApiKey": "",
      "Image": "$(json_escape "${OPENSANDBOX_SANDBOX_IMAGE}")",
      "TimeoutSeconds": ${OPENSANDBOX_TIMEOUT_SECONDS},
      "RenewThresholdSeconds": ${OPENSANDBOX_RENEW_THRESHOLD_SECONDS}
    },
    "Storage": {
      "Endpoint": "$(json_escape "${S3_ENDPOINT}")",
      "ForcePathStyle": true,
      "Bucket": "$(json_escape "${S3_BUCKET}")",
      "AccessKeyId": "$(json_escape "${S3_ACCESS_KEY_ID}")",
      "AccessKeySecret": "$(json_escape "${S3_ACCESS_KEY_SECRET}")"
    },
    "MaxUploadFileSize": 104857600,
    "OTLP": {
      "Trace": "$(json_escape "${OTLP_TRACE}")",
      "Metrics": "$(json_escape "${OTLP_METRICS}")",
      "Protocol": ${OTLP_PROTOCOL}
    }
  },
  "Serilog": {
    "Using": [
      "Serilog.Sinks.Console"
    ],
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft.AspNetCore.HttpLogging": "Information",
        "ProtoBuf.Grpc.Server.ServicesExtensions.CodeFirstServiceMethodProvider": "Warning",
        "Microsoft.EntityFrameworkCore": "Information",
        "Microsoft.AspNetCore": "Warning",
        "System.Net.Http.HttpClient.TenantManagerClient.LogicalHandler": "Warning",
        "Microsoft.EntityFrameworkCore.Database.Command.CommandExecuted": "Warning",
        "System": "Information",
        "Microsoft": "Information",
        "Grpc": "Information",
        "MySqlConnector": "Information"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "{SourceContext} {Scope} {Timestamp:HH:mm} [{Level}]{NewLine}{Properties:j}{NewLine}{Message:lj} {Exception} {NewLine}"
        }
      }
    ],
    "Enrich": [
      "FromLogContext",
      "WithMachineName",
      "WithThreadId"
    ]
  }
}
EOF
echo "==> 已根据 .env 生成应用配置 ${MOAI_CONFIG_FILE}（Server=${MOAI_SERVER_URL}，S3=${S3_ENDPOINT}）"

echo "==> [1/4] 拉取 Compose 全部镜像（postgres=pgvector/pgvector:pg16、opensandbox/server、沙箱镜像等）"
docker compose --profile sandbox-images pull \
  postgres redis rabbitmq rustfs rustfs-init opensandbox-server sandbox-image
docker pull "${OPENSANDBOX_EXECD_IMAGE}"
docker pull "${OPENSANDBOX_EGRESS_IMAGE}"

echo "==> [2/4] 准备 MoAI 镜像"
if [ "${BUILD:-0}" = "1" ]; then
  docker compose build moai
else
  docker compose pull moai || {
    echo "==> 拉取 moai 镜像失败，改为本地源码构建"
    docker compose build moai
  }
fi

echo "==> [3/4] 启动服务"
docker compose up -d

echo "==> [4/4] 当前状态："
docker compose ps
