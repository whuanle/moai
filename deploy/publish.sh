#!/usr/bin/env bash
# ============================================================
# MoAI 镜像发布脚本（构建并推送到 Docker Hub）
#
# 用法（在仓库根目录或任意位置执行）：
#   bash deploy/publish.sh
#   TAG=v1.0.0 bash deploy/publish.sh
#   PLATFORMS=linux/amd64,linux/arm64 bash deploy/publish.sh
#   PUSH=0 bash deploy/publish.sh          # 只构建到本地，不推送（仅单平台）
#
# 可覆盖环境变量：
#   DOCKERHUB_USER  默认 whuanle
#   IMAGE           默认 ${DOCKERHUB_USER}/moai
#   TAG             默认 <日期>-<时间>
#   PLATFORMS       默认 linux/amd64（多平台用逗号分隔，需 buildx + QEMU）
#   PUSH            默认 1（推送）；0=仅本地构建
#
# 前置：docker 已登录 Docker Hub（免密推送环境无需再 login）
# ============================================================
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"
cd "${ROOT_DIR}"

DOCKERHUB_USER="${DOCKERHUB_USER:-whuanle}"
IMAGE="${IMAGE:-${DOCKERHUB_USER}/moai}"
TAG="${TAG:-$(date +%Y%m%d-%H%M%S)}"
PLATFORMS="${PLATFORMS:-linux/amd64}"
PUSH="${PUSH:-1}"
BUILDER="${BUILDER:-moai-builder}"

if [ "${PUSH}" = "0" ] && [[ "${PLATFORMS}" == *,* ]]; then
  echo "ERROR: PUSH=0 时只能构建单一平台（--load 不支持多平台）。请设置 PLATFORMS=linux/amd64。" >&2
  exit 1
fi

echo "==> 目标镜像: ${IMAGE}:${TAG} (同时更新 :latest)"
echo "==> 构建平台: ${PLATFORMS}"

# 使用独立 builder，避免污染默认构建器
if ! docker buildx inspect "${BUILDER}" >/dev/null 2>&1; then
  docker buildx create --name "${BUILDER}" --use >/dev/null
else
  docker buildx use "${BUILDER}" >/dev/null
fi

if [ "${PUSH}" = "1" ]; then
  docker buildx build \
    --platform "${PLATFORMS}" \
    -f Dockerfile \
    -t "${IMAGE}:${TAG}" \
    -t "${IMAGE}:latest" \
    --push \
    .
  echo "==> 推送完成: ${IMAGE}:${TAG} / ${IMAGE}:latest"
else
  docker buildx build \
    --platform "${PLATFORMS}" \
    -f Dockerfile \
    -t "${IMAGE}:${TAG}" \
    -t "${IMAGE}:latest" \
    --load \
    .
  echo "==> 本地构建完成: ${IMAGE}:${TAG} / ${IMAGE}:latest"
fi
