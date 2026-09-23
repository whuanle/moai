#!/bin/bash
set -e

# ============================================================
# MoAI 容器入口脚本
# 配置来源：MAI_FILE（默认 /app/configs/system.json）
#   - docker compose：bind-mount ./configs/system.json
#   - docker run    ：-v $(pwd)/configs/system.json:/app/configs/system.json:ro
# 未挂载时回退到镜像内置的 /app/configs/system.json
# ============================================================

mkdir -p /app/configs /app/files

MAI_FILE="${MAI_FILE:-/app/configs/system.json}"
export MAI_FILE

# 常见误用：宿主机缺少 system.json 时，docker 会在挂载点创建同名目录。
if [ -d "$MAI_FILE" ]; then
  echo "[entrypoint] ERROR: $MAI_FILE 是目录而不是文件。" >&2
  echo "[entrypoint] 请确认宿主机存在该配置文件后再启动，例如：- ./configs/system.json:/app/configs/system.json:ro" >&2
  exit 1
fi

if [ ! -f "$MAI_FILE" ]; then
  if [ -f /app/configs/system.json.template ]; then
    echo "[entrypoint] 未找到 $MAI_FILE，使用内置模板初始化。"
    cp /app/configs/system.json.template "$MAI_FILE"
  else
    echo "[entrypoint] ERROR: 找不到配置文件 $MAI_FILE" >&2
    exit 1
  fi
fi

echo "[entrypoint] 使用配置文件: $MAI_FILE"
exec dotnet MoAI.dll
