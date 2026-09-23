#!/usr/bin/env bash
# ============================================================
# MoAI 一体部署脚本（docker compose）
#
# 用法：
#   bash deploy/deploy-compose.sh              # 拉镜像 + 启动
#   BUILD=1 bash deploy/deploy-compose.sh      # 本地源码构建 moai 镜像后启动
#
# 前置：
#   1) 已安装 docker + docker compose v2
#   2) 存在 configs/system.json（应用配置；compose 会 bind-mount 进容器）
#   3) 可选：cp .env.example .env 并按需修改基础设施账号/端口
#
# 说明：会拉取全部依赖镜像，包括
#   - postgres 服务 = pgvector/pgvector:pg16（必须，不可用裸 postgres 镜像）
#   - opensandbox/server（OpenSandbox 生命周期服务）
#   - opensandbox/code-interpreter（沙箱运行时，仅拉取、不部署）
#   - rustfs / redis / rabbitmq / aws-cli（建桶）
# 沙箱容器由 opensandbox-server 在运行时按需创建。
# ============================================================
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"
cd "${ROOT_DIR}"

# 沙箱执行/网络镜像不在 compose 中（由 deploy/opensandbox/sandbox.toml 引用），单独预拉
OPENSANDBOX_EXECD_IMAGE="${OPENSANDBOX_EXECD_IMAGE:-opensandbox/execd:v1.1.0}"
OPENSANDBOX_EGRESS_IMAGE="${OPENSANDBOX_EGRESS_IMAGE:-opensandbox/egress:v1.1.7}"

# 探测本机对外 IP（用于 system.json 的 Server/WebUI/Storage.Endpoint，须浏览器可达）
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

if [ ! -f configs/system.json ]; then
  echo "ERROR: 缺少 configs/system.json（应用配置）。请先按 docs/deployment/docker.md 准备。" >&2
  exit 1
fi

if [ ! -f .env ]; then
  echo "==> 未找到 .env，从 .env.example 复制"
  cp .env.example .env
fi

# 若 .env 未设置 MOAI_HOST，则自动探测本机 IP 写入（可用公网域名手动覆盖）
CURRENT_HOST="$(grep -E '^MOAI_HOST=' .env 2>/dev/null | head -n1 | cut -d= -f2-)"
if [ -z "${CURRENT_HOST}" ]; then
  DETECTED_HOST="$(detect_host_ip)"
  [ -z "${DETECTED_HOST}" ] && DETECTED_HOST="localhost"
  if grep -qE '^MOAI_HOST=' .env; then
    sed "s|^MOAI_HOST=.*|MOAI_HOST=${DETECTED_HOST}|" .env > .env.tmp && mv .env.tmp .env
  else
    printf '\nMOAI_HOST=%s\n' "${DETECTED_HOST}" >> .env
  fi
  echo "==> 已自动探测 MOAI_HOST=${DETECTED_HOST}（如需公网域名/其他地址，请修改 .env 后重跑）"
fi

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
