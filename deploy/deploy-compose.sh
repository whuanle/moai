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
#   2) 存在 configs/system.json（配置模板，按环境修改；compose 会 bind-mount 进容器）
#   3) 可选：cp .env.example .env 并按需修改基础设施账号/端口
#
# 说明：会先拉取 OpenSandbox 沙箱镜像（仅拉取，不部署），
#       由 opensandbox-server 在运行时按需创建沙箱容器。
# ============================================================
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"
cd "${ROOT_DIR}"

OPENSANDBOX_EXECD_IMAGE="${OPENSANDBOX_EXECD_IMAGE:-opensandbox/execd:v1.1.0}"
OPENSANDBOX_EGRESS_IMAGE="${OPENSANDBOX_EGRESS_IMAGE:-opensandbox/egress:v1.1.7}"

if [ ! -f configs/system.json ]; then
  echo "ERROR: 缺少 configs/system.json（应用配置）。请先按 docs/deployment/docker.md 准备。" >&2
  exit 1
fi

if [ ! -f .env ]; then
  echo "==> 未找到 .env，从 .env.example 复制"
  cp .env.example .env
fi

echo "==> [1/4] 预拉取 OpenSandbox 镜像（沙箱镜像仅拉取，不部署）"
docker compose --profile sandbox-images pull sandbox-image
docker compose pull opensandbox-server
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

echo "==> [3/4] 拉取基础设施镜像"
docker compose pull postgres redis rabbitmq rustfs || true

echo "==> [4/4] 启动服务"
docker compose up -d

echo "==> 当前状态："
docker compose ps
